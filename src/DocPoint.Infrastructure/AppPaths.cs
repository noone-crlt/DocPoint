namespace DocPoint.Infrastructure;

/// <summary>Các đường dẫn dữ liệu của app, mặc định ở %LocalAppData%\DocPoint.</summary>
public sealed class AppPaths(string dataDirectory)
{
    public string DataDirectory { get; } = dataDirectory;
    public string DatabasePath => Path.Combine(DataDirectory, "docpoint.db");
    public string LogsDirectory => Path.Combine(DataDirectory, "logs");
    public string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    /// <summary>Biến môi trường DOCPOINT_DATA đổi thư mục dữ liệu (dùng khi thử, không đụng tới dữ liệu thật).</summary>
    public static AppPaths Default() => new(Environment.GetEnvironmentVariable("DOCPOINT_DATA") is { Length: > 0 } custom ? custom : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DocPoint"));
}
