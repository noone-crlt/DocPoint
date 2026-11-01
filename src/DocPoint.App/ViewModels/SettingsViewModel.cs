using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using DocPoint.App.Services;

namespace DocPoint.App.ViewModels;

public sealed record ThemeOption(AppTheme Value, string Label)
{
    // Trình đọc màn hình đọc ToString() của mục trong ComboBox.
    public override string ToString() => Label;
}

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private AppTheme _theme;

    public SettingsViewModel(AppSettings settings)
    {
        Settings = settings;
        _theme = settings.Theme;
    }

    /// <summary>Các ô bật/tắt gắn thẳng vào đây; lưu khi đóng app.</summary>
    public AppSettings Settings { get; }

    public IReadOnlyList<ThemeOption> Themes { get; } =
    [
        new(AppTheme.Light, "Sáng"),
        new(AppTheme.Dark, "Tối"),
        new(AppTheme.System, "Theo Windows"),
    ];

    public string Version { get; } =
        "Phiên bản " + Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);

    partial void OnThemeChanged(AppTheme value)
    {
        Settings.Theme = value;
        Settings.Save();
        ThemeSwitcher.Apply(value, System.Windows.Application.Current.MainWindow);
    }
}
