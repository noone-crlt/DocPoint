using System.Text;
using DocPoint.Application;
using DocPoint.Domain;
using DocPoint.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace DocPoint.Infrastructure.Search;

/// <summary>
/// Tìm bằng FTS5 trên bản không dấu, rồi xếp lại: chỗ khớp liền cụm, khớp đúng dấu, khớp tiêu đề và
/// khớp tên file được cộng điểm. Vị trí khớp lấy từ highlight() của FTS5 và dùng lại được trên chữ có dấu
/// vì bản không dấu cùng độ dài với bản gốc.
/// </summary>
public sealed class SqliteSearchIndex(DocPointDatabase db) : ISearchIndex
{
    private const char Start = '\u0002';
    private const char End = '\u0003';

    private const string NodeColumns =
        "n.id, n.document_id, n.parent_id, n.kind, n.ordinal, n.number, n.title, n.content, n.label, n.page, n.confidence, n.needs_review";

    // 11 cột của documents đi sau 12 cột của nodes.
    private const int DocumentOffset = 12;

    public Task<IReadOnlyList<NodeMatch>> SearchAsync(SearchQuery query, CancellationToken ct = default) =>
        Task.Run<IReadOnlyList<NodeMatch>>(() => Search(query, ct), ct);

    public Task<IReadOnlyList<string>> SuggestAsync(string prefix, int max = 8, CancellationToken ct = default) =>
        Task.Run<IReadOnlyList<string>>(() => Suggest(prefix, max, ct), ct);

    private List<NodeMatch> Search(SearchQuery query, CancellationToken ct)
    {
        var fts = FtsQuery.Parse(query.Text);
        if (fts.Text is null)
            return [];

        using var conn = db.Open();
        var (filterSql, bind) = Filters(query);

        var candidates = new List<Candidate>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"""
                SELECT {NodeColumns}, {DocumentColumnsSql},
                       bm25(nodes_fts, 5.0, 10.0, 1.0, 1.0),
                       highlight(nodes_fts, 3, char(2), char(3)),
                       highlight(nodes_fts, 1, char(2), char(3))
                FROM nodes_fts
                JOIN nodes n ON n.id = nodes_fts.rowid
                JOIN documents d ON d.id = n.document_id
                WHERE nodes_fts MATCH $match {filterSql}
                ORDER BY bm25(nodes_fts, 5.0, 10.0, 1.0, 1.0)
                LIMIT $limit
                """;
            cmd.Parameters.AddWithValue("$match", fts.Text);
            cmd.Parameters.AddWithValue("$limit", query.Limit);
            bind(cmd);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                ct.ThrowIfCancellationRequested();
                var node = ReadNode(r);
                var doc = SqliteDocumentRepository.ReadDocument(r, DocumentOffset);
                var o = DocumentOffset + 11;
                candidates.Add(new Candidate(node, doc, -r.GetDouble(o), r.GetString(o + 1), r.GetString(o + 2)));
            }
        }

        var phraseIds = fts.Phrase is null ? [] : QueryIds(conn, "SELECT rowid FROM nodes_fts WHERE nodes_fts MATCH $m LIMIT 20000", fts.Phrase);
        var fileDocs = QueryIds(conn, $"""
            SELECT DISTINCT n.document_id FROM nodes_fts
            JOIN nodes n ON n.id = nodes_fts.rowid
            JOIN documents d ON d.id = n.document_id
            WHERE nodes_fts MATCH $m {filterSql}
            """, fts.FileName!, bind);

        var results = Rank(candidates, phraseIds, fileDocs, fts);

        // Tài liệu chỉ khớp tên file: đưa node đầu tiên của nó vào để vẫn mở được.
        var present = results.Select(m => m.Document.Id).ToHashSet();
        foreach (var docId in fileDocs.Where(d => !present.Contains(d)))
            results.Add(FirstNodeOf(conn, docId));

        return results.OrderByDescending(m => m.Score).Take(query.Limit).ToList();
    }

    private static List<NodeMatch> Rank(List<Candidate> candidates, HashSet<long> phraseIds, HashSet<long> fileDocs, FtsQuery fts)
    {
        var top = candidates.Count == 0 ? 0 : candidates.Max(c => c.Bm25);
        var results = new List<NodeMatch>(candidates.Count);

        foreach (var c in candidates)
        {
            var contentRanges = Ranges(c.ContentHighlight);
            // Cột tiêu đề trong chỉ mục còn chứa nhãn vị trí ở đuôi: chỉ giữ đoạn nằm trong tiêu đề thật.
            var titleRanges = Ranges(c.TitleHighlight).Where(r => r.Start + r.Length <= c.Node.Title.Length).ToList();

            var score = top > 0 ? c.Bm25 / top : 1;
            if (phraseIds.Contains(c.Node.Id)) score += 0.5;
            if (fileDocs.Contains(c.Document.Id)) score += 0.5;
            if (titleRanges.Count > 0) score += 0.25;
            if (HasAccentedTerm(c.Node, fts.AccentedTerms)) score += 0.25;

            results.Add(new NodeMatch(
                c.Node, c.Document, score,
                Snippet(c.Node, contentRanges, titleRanges),
                contentRanges, titleRanges));
        }

        return results;
    }

    private static bool HasAccentedTerm(Node node, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
            return false;

        var text = (node.Title + " " + node.Content).ToLowerInvariant();
        return terms.Any(t => text.Contains(t, StringComparison.Ordinal));
    }

    private static NodeMatch FirstNodeOf(SqliteConnection conn, long documentId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT {NodeColumns}, {DocumentColumnsSql}
            FROM nodes n JOIN documents d ON d.id = n.document_id
            WHERE n.document_id = $doc AND (n.title <> '' OR n.content <> '')
            ORDER BY n.ordinal LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$doc", documentId);
        using var r = cmd.ExecuteReader();
        r.Read();
        var node = ReadNode(r);
        var doc = SqliteDocumentRepository.ReadDocument(r, DocumentOffset);
        return new NodeMatch(node, doc, 0.5, Snippet(node, [], []), [], []);
    }

    private List<string> Suggest(string prefix, int max, CancellationToken ct)
    {
        var fts = FtsQuery.Parse(prefix);
        if (fts.Text is null)
            return [];

        using var conn = db.Open();
        var suggestions = new List<string>();

        void Collect(string sql, string match)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("$m", match);
            cmd.Parameters.AddWithValue("$limit", max * 4);
            using var r = cmd.ExecuteReader();
            while (r.Read() && suggestions.Count < max)
            {
                ct.ThrowIfCancellationRequested();
                var value = r.GetString(0).Trim();
                if (value.Length > 0 && !suggestions.Contains(value, StringComparer.OrdinalIgnoreCase))
                    suggestions.Add(value);
            }
        }

        Collect("""
            SELECT DISTINCT n.title FROM nodes_fts JOIN nodes n ON n.id = nodes_fts.rowid
            WHERE nodes_fts MATCH $m AND n.title <> '' LIMIT $limit
            """, "{title} : " + fts.Text[(fts.Text.IndexOf(':') + 1)..].Trim());
        Collect("""
            SELECT DISTINCT d.file_name FROM nodes_fts JOIN nodes n ON n.id = nodes_fts.rowid JOIN documents d ON d.id = n.document_id
            WHERE nodes_fts MATCH $m LIMIT $limit
            """, fts.FileName!);

        return suggestions;
    }

    // ----- Bộ lọc -----

    private static (string Sql, Action<SqliteCommand> Bind) Filters(SearchQuery q)
    {
        var sql = new StringBuilder();
        if (q.FolderId is not null) sql.Append(" AND d.folder_id = $folder");
        if (q.ModifiedFrom is not null) sql.Append(" AND d.modified_at >= $from");
        if (q.ModifiedTo is not null) sql.Append(" AND d.modified_at <= $to");

        var extensions = q.Extensions?.Select(e => e.ToLowerInvariant()).ToList();
        if (extensions is { Count: > 0 })
            sql.Append(" AND d.ext IN (" + string.Join(',', extensions.Select((_, i) => "$ext" + i)) + ")");

        return (sql.ToString(), cmd =>
        {
            if (q.FolderId is { } folder) cmd.Parameters.AddWithValue("$folder", folder);
            if (q.ModifiedFrom is { } from) cmd.Parameters.AddWithValue("$from", from.ToUniversalTime().ToString("O"));
            if (q.ModifiedTo is { } to) cmd.Parameters.AddWithValue("$to", to.ToUniversalTime().ToString("O"));
            for (var i = 0; i < (extensions?.Count ?? 0); i++)
                cmd.Parameters.AddWithValue("$ext" + i, extensions![i]);
        });
    }

    private static HashSet<long> QueryIds(SqliteConnection conn, string sql, string match, Action<SqliteCommand>? bind = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$m", match);
        bind?.Invoke(cmd);

        var ids = new HashSet<long>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            ids.Add(r.GetInt64(0));
        return ids;
    }

    // ----- Đọc dòng kết quả -----

    private const string DocumentColumnsSql = SqliteDocumentRepository.DocumentColumns;

    private static Node ReadNode(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        DocumentId = r.GetInt64(1),
        ParentId = r.IsDBNull(2) ? null : r.GetInt64(2),
        Kind = (NodeKind)r.GetInt32(3),
        Ordinal = r.GetInt32(4),
        Number = r.GetString(5),
        Title = r.GetString(6),
        Content = r.GetString(7),
        Label = r.GetString(8),
        Page = r.IsDBNull(9) ? null : r.GetInt32(9),
        Confidence = r.GetDouble(10),
        NeedsReview = r.GetInt32(11) != 0,
    };

    private sealed record Candidate(Node Node, Document Document, double Bm25, string ContentHighlight, string TitleHighlight);

    // ----- Vị trí khớp và đoạn trích -----

    /// <summary>Đọc các đoạn nằm giữa cặp ký tự đánh dấu; vị trí tính trên chuỗi đã bỏ ký tự đánh dấu.</summary>
    internal static List<TextRange> Ranges(string highlighted)
    {
        var ranges = new List<TextRange>();
        var position = 0;
        var open = -1;
        foreach (var c in highlighted)
        {
            if (c == Start) open = position;
            else if (c == End && open >= 0)
            {
                ranges.Add(new TextRange(open, position - open));
                open = -1;
            }
            else position++;
        }

        return ranges;
    }

    private static string Snippet(Node node, IReadOnlyList<TextRange> content, IReadOnlyList<TextRange> title)
    {
        const int before = 60, window = 200;

        if (content.Count > 0)
        {
            var text = node.Content;
            var from = Math.Max(0, content[0].Start - before);
            if (from > 0)
                from = Math.Max(0, text.LastIndexOf(' ', from) + 1);
            var to = Math.Min(text.Length, Math.Max(content[0].Start + content[0].Length, from + window));
            if (to < text.Length)
            {
                var space = text.IndexOf(' ', to);
                to = space < 0 ? text.Length : space;
            }

            return (from > 0 ? "…" : "") + Mark(text, from, to, content) + (to < text.Length ? "…" : "");
        }

        if (title.Count > 0)
            return Mark(node.Title, 0, node.Title.Length, title);

        var plain = node.Content.Length > 0 ? node.Content : node.Title;
        return plain.Length <= window ? plain : plain[..window] + "…";
    }

    private static string Mark(string text, int from, int to, IReadOnlyList<TextRange> ranges)
    {
        var sb = new StringBuilder();
        var cursor = from;
        foreach (var range in ranges.Where(r => r.Start >= from && r.Start + r.Length <= to))
        {
            sb.Append(text, cursor, range.Start - cursor).Append(Start).Append(text, range.Start, range.Length).Append(End);
            cursor = range.Start + range.Length;
        }

        return sb.Append(text, cursor, to - cursor).ToString();
    }
}
