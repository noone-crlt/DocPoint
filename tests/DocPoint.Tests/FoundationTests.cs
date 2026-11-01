using DocPoint.Domain;
using DocPoint.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace DocPoint.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("Đường Điện Biên Phủ", "Duong Dien Bien Phu")]
    [InlineData("nghỉ hằng năm", "nghi hang nam")]
    [InlineData("ABC xyz 123", "ABC xyz 123")]
    public void RemoveDiacritics_MapsToBaseLetters(string input, string expected) =>
        Assert.Equal(expected, TextNormalizer.RemoveDiacritics(input));

    [Fact]
    public void RemoveDiacritics_KeepsLength_EvenFromDecomposedInput()
    {
        // "ệ" gõ kiểu rời (e + dấu mũ + dấu chấm) phải ra cùng độ dài với bản dựng sẵn.
        var decomposed = "nghiệp".Normalize(System.Text.NormalizationForm.FormD);
        var plain = TextNormalizer.RemoveDiacritics(decomposed);

        Assert.Equal("nghiep", plain);
        Assert.Equal(TextNormalizer.Normalize(decomposed).Length, plain.Length);
    }
}

public class LocationLabelTests
{
    [Fact]
    public void Build_LegalChain_LowersMinorUnits()
    {
        var chain = new[]
        {
            new LabelPart(NodeKind.Article, "Điều 5"),
            new LabelPart(NodeKind.Clause, "Khoản 2"),
            new LabelPart(NodeKind.Point, "Điểm a"),
        };

        Assert.Equal("Điểm a, khoản 2, Điều 5", LocationLabel.Build(chain));
    }

    [Fact]
    public void Build_ReportChain_AppendsPage()
    {
        var chain = new[]
        {
            new LabelPart(NodeKind.Chapter, "Chương 2"),
            new LabelPart(NodeKind.Heading, "Mục 2.3"),
        };

        Assert.Equal("Mục 2.3, Chương 2, trang 15", LocationLabel.Build(chain, page: 15));
    }

    [Fact]
    public void Build_PageNode_DoesNotRepeatPage()
    {
        var chain = new[]
        {
            new LabelPart(NodeKind.Page, "Trang 7"),
            new LabelPart(NodeKind.Paragraph, "Đoạn 3"),
        };

        Assert.Equal("Đoạn 3, trang 7", LocationLabel.Build(chain, page: 7));
    }
}

public class MigrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "docpoint-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Initialize_CreatesAllTables_AndIsIdempotent()
    {
        var db = new DocPointDatabase(Path.Combine(_dir, "docpoint.db"));
        db.Initialize();
        db.Initialize();

        using var conn = db.Open();
        var tables = Scalar<long>(conn,
            "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('schema_version','folders','documents','nodes','nodes_fts')");

        Assert.Equal(5, tables);
        Assert.Equal(1, Migrator.CurrentVersion(conn));
        Assert.Equal("wal", Scalar<string>(conn, "PRAGMA journal_mode"));
    }

    [Fact]
    public void DeletingDocument_RemovesNodesAndSearchRows()
    {
        var db = new DocPointDatabase(Path.Combine(_dir, "docpoint.db"));
        db.Initialize();

        using var conn = db.Open();
        Exec(conn, "INSERT INTO folders (id, path) VALUES (1, 'C:\\x')");
        Exec(conn, "INSERT INTO documents (id, folder_id, path, file_name, ext, modified_at, size) VALUES (1, 1, 'C:\\x\\a.txt', 'a.txt', '.txt', '2026-01-01', 1)");
        Exec(conn, "INSERT INTO nodes (id, document_id, kind, ordinal, content) VALUES (1, 1, 8, 0, 'xin chao')");
        Exec(conn, "INSERT INTO nodes_fts (rowid, file_name, title, content, content_plain) VALUES (1, 'a.txt', '', 'xin chao', 'xin chao')");

        Exec(conn, "DELETE FROM documents WHERE id = 1");

        Assert.Equal(0, Scalar<long>(conn, "SELECT COUNT(*) FROM nodes"));
        Assert.Equal(0, Scalar<long>(conn, "SELECT COUNT(*) FROM nodes_fts"));
    }

    private static T Scalar<T>(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (T)Convert.ChangeType(cmd.ExecuteScalar()!, typeof(T));
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
