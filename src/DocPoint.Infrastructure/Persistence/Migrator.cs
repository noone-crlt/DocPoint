using System.Reflection;
using Microsoft.Data.Sqlite;

namespace DocPoint.Infrastructure.Persistence;

/// <summary>
/// Chạy các file SQL trong Migrations theo số thứ tự (001_..., 002_...). Số đã chạy ghi trong bảng
/// schema_version; mỗi file chạy trong một transaction.
/// </summary>
public static class Migrator
{
    public static int Run(SqliteConnection conn) => Run(conn, typeof(Migrator).Assembly);

    internal static int Run(SqliteConnection conn, Assembly assembly)
    {
        DocPointDatabase.Exec(conn, "CREATE TABLE IF NOT EXISTS schema_version (version INTEGER PRIMARY KEY, applied_at TEXT NOT NULL)");

        var current = CurrentVersion(conn);
        foreach (var (version, name) in Discover(assembly).Where(m => m.Version > current).OrderBy(m => m.Version))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            var sql = reader.ReadToEnd();

            using var tx = conn.BeginTransaction();
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO schema_version (version, applied_at) VALUES ($v, $t)";
                cmd.Parameters.AddWithValue("$v", version);
                cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
            current = version;
        }

        return current;
    }

    public static int CurrentVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static IEnumerable<(int Version, string ResourceName)> Discover(Assembly assembly)
    {
        const string marker = ".Migrations.";
        foreach (var name in assembly.GetManifestResourceNames())
        {
            var at = name.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0 || !name.EndsWith(".sql", StringComparison.Ordinal))
                continue;

            var file = name[(at + marker.Length)..];
            var digits = new string(file.TakeWhile(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var version))
                yield return (version, name);
        }
    }
}
