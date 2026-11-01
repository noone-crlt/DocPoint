using System.Windows;
using DocPoint.App.Services;
using DocPoint.App.ViewModels;
using DocPoint.App.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace DocPoint.App.Views;

public partial class MainWindow : FluentWindow
{
    private readonly AppSettings _settings;

    public MainWindow(MainWindowViewModel viewModel, IPageNavigator navigator, IServiceProvider services, AppSettings settings)
    {
        _settings = settings;
        DataContext = viewModel;
        InitializeComponent();

        navigator.Navigated += page => PageHost.Content = services.GetRequiredService(page);
        RestorePlacement();

        SearchBox.QuerySubmitted += (_, e) => viewModel.SearchCommand.Execute(e.QueryText);
        SearchBox.SuggestionChosen += (_, e) => viewModel.SearchCommand.Execute(e.SelectedItem?.ToString());

        Loaded += async (_, _) =>
        {
            ThemeSwitcher.Apply(settings.Theme, this);
            navigator.Navigate(typeof(HomePage));
            await viewModel.LoadAsync();
        };
        Closing += (_, _) => SavePlacement();
    }

    private void RestorePlacement()
    {
        if (_settings.WindowLeft is not { } left || _settings.WindowTop is not { } top)
            return;

        // Bỏ qua vị trí cũ nếu màn hình đó không còn (rút màn hình phụ).
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var saved = new Rect(left, top, _settings.WindowWidth, _settings.WindowHeight);
        if (!screen.IntersectsWith(saved))
            return;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;
        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;
        if (_settings.WindowMaximized)
            WindowState = WindowState.Maximized;
    }

    private void SavePlacement()
    {
        var bounds = RestoreBounds;
        _settings.WindowLeft = bounds.Left;
        _settings.WindowTop = bounds.Top;
        _settings.WindowWidth = bounds.Width;
        _settings.WindowHeight = bounds.Height;
        _settings.WindowMaximized = WindowState == WindowState.Maximized;
        _settings.Save();
    }
}
