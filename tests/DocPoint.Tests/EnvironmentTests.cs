using Microsoft.Data.Sqlite;

namespace DocPoint.Tests;

// Kiểm tra nhanh môi trường: SQLite đi kèm có FTS5, và cách bỏ dấu tiếng Việt chạy đúng.
public class EnvironmentTests
{
    [Fact]
    public void Sqlite_HasFts5_WithBm25AndHighlight()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        Exec(conn, "CREATE VIRTUAL TABLE t USING fts5(noi_dung, noi_dung_khong_dau)");
        Exec(conn, "INSERT INTO t VALUES ('Không chấp hành hiệu lệnh của đèn tín hiệu giao thông', 'khong chap hanh hieu lenh cua den tin hieu giao thong')");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT highlight(t, 1, '[', ']'), bm25(t) FROM t WHERE t MATCH 'noi_dung_khong_dau: \"den tin hieu\"'";
        using var r = cmd.ExecuteReader();

        Assert.True(r.Read());
        Assert.Contains("[den tin hieu]", r.GetString(0));
    }

    [Theory]
    [InlineData("Vượt đèn đỏ", "Vuot den do")]
    [InlineData("Điểm a khoản 2 Điều 6", "Diem a khoan 2 Dieu 6")]
    public void RemoveDiacritics_Vietnamese(string input, string expected)
    {
        Assert.Equal(expected, DocPoint.Domain.TextNormalizer.RemoveDiacritics(input));
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
