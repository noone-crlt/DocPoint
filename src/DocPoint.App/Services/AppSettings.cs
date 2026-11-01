using System.IO;
using System.Text.Json;

namespace DocPoint.App.Services;

public enum AppTheme { System, Light, Dark }

/// <summary>Cài đặt của người dùng, lưu ở %LOCALAPPDATA%\DocPoint\settings.json.</summary>
public sealed class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DocPoint", "settings.json");

    public AppTheme Theme { get; set; } = AppTheme.System;

    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 800;
    public bool WindowMaximized { get; set; }

    public bool ReadPdf { get; set; } = true;
    public bool ReadWord { get; set; } = true;
    public bool ReadExcel { get; set; } = true;
    public bool ReadText { get; set; } = true;
    public double MaxFileSizeMb { get; set; } = 100;

    public static AppSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
    }
}
