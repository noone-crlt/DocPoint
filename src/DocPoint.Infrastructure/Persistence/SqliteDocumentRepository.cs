using System.Globalization;
using DocPoint.Application;
using DocPoint.Domain;
using Microsoft.Data.Sqlite;

namespace DocPoint.Infrastructure.Persistence;

/// <summary>
/// Lưu tài liệu và cây node. Mỗi lần ghi một tài liệu là một transaction: xoá node cũ, ghi node mới,
/// cập nhật bảng tìm kiếm. Id của tài liệu được giữ khi đọc lại.
/// </summary>
public sealed class SqliteDocumentRepository(DocPointDatabase db, IClock clock) : IDocumentRepository
{
    public Task<Document> ReplaceAsync(Document document, IReadOnlyList<Node> nodes, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            using var conn = db.Open();
            using var tx = conn.BeginTransaction();
            var saved = Replace(conn, tx, document, nodes, ct);
            tx.Commit();
            return saved;
        }, ct);

    public Task ReplaceBatchAsync(IReadOnlyList<(Document Document, IReadOnlyList<Node> Nodes)> items, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            using var conn = db.Open();
            using var tx = conn.BeginTransaction();
            foreach (var (document, nodes) in items)
                Replace(conn, tx, document, nodes, ct);
            tx.Commit();
        }, ct);

    public Task<Document?> GetByPathAsync(string path, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            using var conn = db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = DocumentSelect + " WHERE path = $path";
            cmd.Parameters.AddWithValue("$path", path);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadDocument(reader) : null;
        }, ct);

    public Task<IReadOnlyList<Document>> ListByFolderAsync(long folderId, CancellationToken ct = default) =>
        Task.Run<IReadOnlyList<Document>>(() =>
        {
            using var conn = db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = DocumentSelect + " WHERE folder_id = $folder ORDER BY file_name COLLATE NOCASE";
            cmd.Parameters.AddWithValue("$folder", folderId);

            var list = new List<Document>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadDocument(reader));
            return list;
        }, ct);

    public Task<IReadOnlyList<Node>> GetNodesAsync(long documentId, CancellationToken ct = default) =>
        Task.Run<IReadOnlyList<Node>>(() =>
        {
            using var conn = db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT n.id, n.document_id, n.parent_id, p.ordinal, n.kind, n.ordinal, n.number, n.title, n.content,
                       n.label, n.page, n.confidence, n.needs_review, n.review_reason
                FROM nodes n LEFT JOIN nodes p ON p.id = n.parent_id
                WHERE n.document_id = $doc
                ORDER BY n.ordinal
                """;
            cmd.Parameters.AddWithValue("$doc", documentId);

            var list = new List<Node>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Node
                {
                    Id = r.GetInt64(0),
                    DocumentId = r.GetInt64(1),
                    ParentId = r.IsDBNull(2) ? null : r.GetInt64(2),
                    ParentOrdinal = r.IsDBNull(3) ? null : r.GetInt32(3),
                    Kind = (NodeKind)r.GetInt32(4),
                    Ordinal = r.GetInt32(5),
                    Number = r.GetString(6),
                    Title = r.GetString(7),
                    Content = r.GetString(8),
                    Label = r.GetString(9),
                    Page = r.IsDBNull(10) ? null : r.GetInt32(10),
                    Confidence = r.GetDouble(11),
                    NeedsReview = r.GetInt32(12) != 0,
                    ReviewReason = r.IsDBNull(13) ? null : r.GetString(13),
                });
            }

            return list;
        }, ct);

    public Task DeleteAsync(long documentId, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            using var conn = db.Open();
            using var cmd = conn.CreateCommand();
            // Node và dòng tìm kiếm đi theo: khoá ngoại xoá dây chuyền, trigger xoá nodes_fts.
            cmd.CommandText = "DELETE FROM documents WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", documentId);
            cmd.ExecuteNonQuery();
        }, ct);

    private Document Replace(SqliteConnection conn, SqliteTransaction tx, Document document, IReadOnlyList<Node> nodes, CancellationToken ct)
    {
        var id = document.Id != 0 ? document.Id : FindIdByPath(conn, tx, document.Path);
        if (document.Status is DocumentStatus.Indexed or DocumentStatus.NeedsReview)
            document.IndexedAt ??= clock.Now.ToUniversalTime();

        if (id is { } existing)
        {
            Execute(conn, tx, """
                UPDATE documents SET folder_id = $folder, path = $path, file_name = $name, ext = $ext, modified_at = $modified,
                    size = $size, hash = $hash, status = $status, error = $error, indexed_at = $indexed
                WHERE id = $id
                """, Bind(document, existing));
            Execute(conn, tx, "DELETE FROM nodes WHERE document_id = $id", ("$id", existing));
        }
        else
        {
            using var insert = conn.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
                INSERT INTO documents (folder_id, path, file_name, ext, modified_at, size, hash, status, error, indexed_at)
                VALUES ($folder, $path, $name, $ext, $modified, $size, $hash, $status, $error, $indexed)
                RETURNING id
                """;
            foreach (var (name, value) in Bind(document, 0))
                insert.Parameters.AddWithValue(name, value);
            id = (long)insert.ExecuteScalar()!;
        }

        document.Id = id.Value;
        InsertNodes(conn, tx, document, nodes, ct);
        return document;
    }

    private static void InsertNodes(SqliteConnection conn, SqliteTransaction tx, Document document, IReadOnlyList<Node> nodes, CancellationToken ct)
    {
        if (nodes.Count == 0)
            return;

        using var insertNode = conn.CreateCommand();
        insertNode.Transaction = tx;
        insertNode.CommandText = """
            INSERT INTO nodes (document_id, parent_id, kind, ordinal, number, title, content, label, page, confidence, needs_review, review_reason)
            VALUES ($doc, $parent, $kind, $ordinal, $number, $title, $content, $label, $page, $confidence, $review, $reason)
            RETURNING id
            """;
        var pDoc = insertNode.Parameters.Add("$doc", SqliteType.Integer);
        var pParent = insertNode.Parameters.Add("$parent", SqliteType.Integer);
        var pKind = insertNode.Parameters.Add("$kind", SqliteType.Integer);
        var pOrdinal = insertNode.Parameters.Add("$ordinal", SqliteType.Integer);
        var pNumber = insertNode.Parameters.Add("$number", SqliteType.Text);
        var pTitle = insertNode.Parameters.Add("$title", SqliteType.Text);
        var pContent = insertNode.Parameters.Add("$content", SqliteType.Text);
        var pLabel = insertNode.Parameters.Add("$label", SqliteType.Text);
        var pPage = insertNode.Parameters.Add("$page", SqliteType.Integer);
        var pConfidence = insertNode.Parameters.Add("$confidence", SqliteType.Real);
        var pReview = insertNode.Parameters.Add("$review", SqliteType.Integer);
        var pReason = insertNode.Parameters.Add("$reason", SqliteType.Text);

        using var insertFts = conn.CreateCommand();
        insertFts.Transaction = tx;
        insertFts.CommandText = "INSERT INTO nodes_fts (rowid, file_name, title, content, content_plain) VALUES ($id, $file, $title, $content, $plain)";
        var fId = insertFts.Parameters.Add("$id", SqliteType.Integer);
        var fFile = insertFts.Parameters.Add("$file", SqliteType.Text);
        var fTitle = insertFts.Parameters.Add("$title", SqliteType.Text);
        var fContent = insertFts.Parameters.Add("$content", SqliteType.Text);
        var fPlain = insertFts.Parameters.Add("$plain", SqliteType.Text);

        // Tên file và tiêu đề vào bảng tìm kiếm ở dạng không dấu để chữ đ cũng khớp được.
        var plainFileName = TextNormalizer.RemoveDiacritics(document.FileName);
        var ids = new long[nodes.Count];

        for (var i = 0; i < nodes.Count; i++)
        {
            if (i % 256 == 0)
                ct.ThrowIfCancellationRequested();

            var n = nodes[i];
            pDoc.Value = document.Id;
            pParent.Value = n.ParentOrdinal is { } po ? ids[po] : DBNull.Value;
            pKind.Value = (int)n.Kind;
            pOrdinal.Value = n.Ordinal;
            pNumber.Value = n.Number;
            pTitle.Value = n.Title;
            pContent.Value = n.Content;
            pLabel.Value = n.Label;
            pPage.Value = n.Page is { } page ? page : DBNull.Value;
            pConfidence.Value = n.Confidence;
            pReview.Value = n.NeedsReview ? 1 : 0;
            pReason.Value = n.ReviewReason is { } reason ? reason : DBNull.Value;

            ids[i] = (long)insertNode.ExecuteScalar()!;
            n.Id = ids[i];
            n.DocumentId = document.Id;
            n.ParentId = n.ParentOrdinal is { } p ? ids[p] : null;

            if (n.Title.Length == 0 && n.Content.Length == 0)
                continue;

            fId.Value = ids[i];
            fFile.Value = plainFileName;
            // Nhãn vị trí đi sau tiêu đề trong cột tìm kiếm, để gõ "điều 4" hay "khoản 2" cũng tìm ra.
            fTitle.Value = TextNormalizer.RemoveDiacritics(n.Title + " " + n.Label);
            fContent.Value = n.Content;
            fPlain.Value = TextNormalizer.RemoveDiacritics(n.Content);
            insertFts.ExecuteNonQuery();
        }
    }

    private static long? FindIdByPath(SqliteConnection conn, SqliteTransaction tx, string path)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM documents WHERE path = $path";
        cmd.Parameters.AddWithValue("$path", path);
        return cmd.ExecuteScalar() is long id ? id : null;
    }

    private static (string, object)[] Bind(Document d, long id) =>
    [
        ("$id", id),
        ("$folder", d.FolderId),
        ("$path", d.Path),
        ("$name", d.FileName),
        ("$ext", d.Extension),
        ("$modified", Format(d.ModifiedAt)),
        ("$size", d.Size),
        ("$hash", d.Hash),
        ("$status", (int)d.Status),
        ("$error", (object?)d.Error ?? DBNull.Value),
        ("$indexed", d.IndexedAt is { } at ? Format(at) : DBNull.Value),
    ];

    private static void Execute(SqliteConnection conn, SqliteTransaction tx, string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }

    // Giờ lưu ở dạng UTC để so sánh được với ngày sửa file đọc từ ổ đĩa.
    private static string Format(DateTime value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    internal static DateTime Parse(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    internal const string DocumentColumns = "d.id, d.folder_id, d.path, d.file_name, d.ext, d.modified_at, d.size, d.hash, d.status, d.error, d.indexed_at";

    private const string DocumentSelect =
        "SELECT id, folder_id, path, file_name, ext, modified_at, size, hash, status, error, indexed_at FROM documents";

    /// <summary>Đọc một dòng bảng documents bắt đầu từ cột <paramref name="o"/> (thứ tự cột như <see cref="DocumentColumns"/>).</summary>
    internal static Document ReadDocument(SqliteDataReader r, int o = 0) => new()
    {
        Id = r.GetInt64(o),
        FolderId = r.GetInt64(o + 1),
        Path = r.GetString(o + 2),
        FileName = r.GetString(o + 3),
        Extension = r.GetString(o + 4),
        ModifiedAt = Parse(r.GetString(o + 5)),
        Size = r.GetInt64(o + 6),
        Hash = r.GetString(o + 7),
        Status = (DocumentStatus)r.GetInt32(o + 8),
        Error = r.IsDBNull(o + 9) ? null : r.GetString(o + 9),
        IndexedAt = r.IsDBNull(o + 10) ? null : Parse(r.GetString(o + 10)),
    };
}
