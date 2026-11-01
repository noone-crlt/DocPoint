using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace DocPoint.App.Services;

public static class ThemeSwitcher
{
    private static bool _applying;

    public static void Apply(AppTheme theme, Window window)
    {
        SystemThemeWatcher.UnWatch(window);

        if (theme == AppTheme.System)
        {
            ApplicationThemeManager.ApplySystemTheme(true);
            SystemThemeWatcher.Watch(window, WindowBackdropType.None, true);
        }
        else
        {
            ApplicationThemeManager.Apply(
                theme == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light, WindowBackdropType.None, true);
        }

        ApplyPalette();
    }

    /// <summary>Đổi bảng màu theo sáng/tối/High Contrast, kể cả khi Windows tự đổi.</summary>
    public static void Track() => ApplicationThemeManager.Changed += (_, _) => ApplyPalette();

    private static void ApplyPalette()
    {
        if (_applying)
            return;

        _applying = true;
        try
        {
            var theme = ApplicationThemeManager.GetAppTheme();
            var file = theme switch
            {
                ApplicationTheme.Dark => "Palette.Dark.xaml",
                ApplicationTheme.HighContrast => "Palette.HighContrast.xaml",
                _ => "Palette.Light.xaml",
            };

            var app = System.Windows.Application.Current;
            var palette = app.Resources.MergedDictionaries
                .First(d => d.Source?.OriginalString.Contains("Palette.") == true);
            palette.Source = new Uri($"Resources/{file}", UriKind.Relative);

            // Màu nhấn của app là xanh rêu của mockup; High Contrast giữ màu hệ thống.
            if (theme != ApplicationTheme.HighContrast)
                ApplicationAccentColorManager.Apply((Color)app.Resources["DpActColor"], theme);
        }
        finally
        {
            _applying = false;
        }
    }
}
