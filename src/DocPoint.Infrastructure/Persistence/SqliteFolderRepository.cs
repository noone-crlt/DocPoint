using System.Globalization;
using DocPoint.Application;
using DocPoint.Domain;

namespace DocPoint.Infrastructure.Persistence;

public sealed class SqliteFolderRepository(DocPointDatabase db) : IFolderRepository
{
    private const string Columns = "id, path, include_subfolders, ignore_patterns, auto_index, last_scan_at, status";

    public Task<IReadOnlyList<WatchedFolder>> ListAsync(CancellationToken ct = default) =>
        Task.Run<IReadOnlyList<WatchedFolder>>(() => List(), ct);

    public Task<WatchedFolder> AddAsync(string path, bool includeSubfolders = true, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var full = Normalize(path);
            if (!Directory.Exists(full))
                throw new FolderException($"Không thấy thư mục \"{full}\". Hãy kiểm tra đường dẫn hoặc ổ đĩa.");

            foreach (var other in List())
            {
                if (IsSameOrInside(full, other.Path))
                    throw new FolderException(string.Equals(full, other.Path, StringComparison.OrdinalIgnoreCase)
                        ? $"Thư mục \"{full}\" đã được theo dõi."
                        : $"Thư mục này nằm trong \"{other.Path}\", đang được theo dõi nên không cần thêm.");
                if (IsSameOrInside(other.Path, full))
                    throw new FolderException($"Thư mục này chứa \"{other.Path}\", đang được theo dõi. Hãy bỏ \"{other.Path}\" trước rồi thêm thư mục lớn hơn.");
            }

            using var conn = db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO folders (path, include_subfolders) VALUES ($path, $sub) RETURNING id";
            cmd.Parameters.AddWithValue("$path", full);
            cmd.Parameters.AddWithValue("$sub", includeSubfolders ? 1 : 0);
            var id = (long)cmd.ExecuteScalar()!;
            return new WatchedFolder { Id = id, Path = full, IncludeSubfolders = includeSubfolders };
        }, ct);

    public Task RemoveAsync(long folderId, CancellationToken ct = default) =>
        Task.Run(() => Execute("DELETE FROM folders WHERE id = $id", ("$id", folderId)), ct);

    public Task UpdateAsync(WatchedFolder folder, CancellationToken ct = default) =>
        Task.Run(() => Execute(
            "UPDATE folders SET include_subfolders = $sub, ignore_patterns = $ignore, auto_index = $auto WHERE id = $id",
            ("$id", folder.Id), ("$sub", folder.IncludeSubfolders ? 1 : 0),
            ("$ignore", string.Join('\n', folder.IgnorePatterns)), ("$auto", folder.AutoIndex ? 1 : 0)), ct);

    public Task SetStatusAsync(long folderId, string status, DateTime? scannedAt, CancellationToken ct = default) =>
        Task.Run(() => Execute(
            "UPDATE folders SET status = $status, last_scan_at = COALESCE($at, last_scan_at) WHERE id = $id",
            ("$id", folderId), ("$status", status),
            ("$at", scannedAt is { } at ? at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) : DBNull.Value)), ct);

    private List<WatchedFolder> List()
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM folders ORDER BY path COLLATE NOCASE";
        var list = new List<WatchedFolder>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new WatchedFolder
            {
                Id = r.GetInt64(0),
                Path = r.GetString(1),
                IncludeSubfolders = r.GetInt32(2) != 0,
                IgnorePatterns = r.GetString(3).Split('\n', StringSplitOptions.RemoveEmptyEntries),
                AutoIndex = r.GetInt32(4) != 0,
                LastScanAt = r.IsDBNull(5) ? null : SqliteDocumentRepository.Parse(r.GetString(5)),
                Status = r.GetString(6),
            });
        }

        return list;
    }

    private void Execute(string sql, params (string Name, object Value)[] parameters)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }

    private static string Normalize(string path)
    {
        var full = System.IO.Path.GetFullPath(path.Trim());
        var root = System.IO.Path.GetPathRoot(full);
        return full == root ? full : full.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
    }

    private static bool IsSameOrInside(string path, string parent)
    {
        if (string.Equals(path, parent, StringComparison.OrdinalIgnoreCase))
            return true;

        var prefix = parent.EndsWith(System.IO.Path.DirectorySeparatorChar) ? parent : parent + System.IO.Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
