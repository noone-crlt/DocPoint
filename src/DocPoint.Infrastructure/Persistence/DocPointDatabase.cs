using Microsoft.Data.Sqlite;

namespace DocPoint.Infrastructure.Persistence;

/// <summary>Nơi mở kết nối tới file SQLite của DocPoint và đưa schema lên bản mới nhất.</summary>
public sealed class DocPointDatabase
{
    private readonly string _connectionString;

    public DocPointDatabase(string databasePath)
    {
        DatabasePath = databasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
        }.ToString();
    }

    public string DatabasePath { get; }

    /// <summary>Mở một kết nối mới; người gọi tự đóng.</summary>
    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        // busy_timeout: chờ thay vì báo lỗi khi luồng khác đang ghi.
        Exec(conn, "PRAGMA busy_timeout = 5000; PRAGMA synchronous = NORMAL;");
        return conn;
    }

    /// <summary>Bật WAL và chạy các migration còn thiếu. Gọi một lần khi mở app.</summary>
    public void Initialize()
    {
        using var conn = Open();
        Exec(conn, "PRAGMA journal_mode = WAL;");
        Migrator.Run(conn);
    }

    internal static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
