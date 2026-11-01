namespace DocPoint.Tests.Support;

/// <summary>Tìm thư mục samples/ ở gốc repo từ thư mục chạy test.</summary>
internal static class Samples
{
    public static string Dir { get; } = Locate();

    public static string File(string name) => Path.Combine(Dir, name);

    private static string Locate()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "samples");
            if (Directory.Exists(candidate))
                return candidate;
        }

        throw new DirectoryNotFoundException("Không thấy thư mục samples/.");
    }
}
