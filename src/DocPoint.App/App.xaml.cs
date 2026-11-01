using System.Windows;
using DocPoint.App.Services;
using DocPoint.App.Services.Fakes;
using DocPoint.App.ViewModels;
using DocPoint.App.Views;
using DocPoint.App.Views.Pages;
using DocPoint.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DocPoint.App;

public partial class App
{
    private readonly IHost _host = Host.CreateDefaultBuilder()
        .ConfigureServices(services =>
        {
            services.AddSingleton(AppSettings.Load());

            // Dữ liệu mẫu; thay bằng bản thật ở Infrastructure khi lõi xong.
            services.AddSingleton<ISearchService, FakeSearchService>();
            services.AddSingleton<ILibraryService, FakeLibraryService>();
            services.AddSingleton<IFolderWatchService, FakeFolderWatchService>();
            services.AddSingleton<IRecentFilesService, FakeRecentFilesService>();

            services.AddSingleton<IPageNavigator, PageNavigator>();

            services.AddSingleton<MainWindow>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<SearchResultsViewModel>();
            services.AddSingleton<HomeViewModel>();
            services.AddSingleton<SettingsViewModel>();

            services.AddSingleton<HomePage>();
            services.AddSingleton<LibraryPage>();
            services.AddSingleton<FoldersPage>();
            services.AddSingleton<PinnedPage>();
            services.AddSingleton<BackupPage>();
            services.AddSingleton<SettingsPage>();
            services.AddSingleton<SearchResultsPage>();
        })
        .Build();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _host.Start();
        ThemeSwitcher.Track();
        _host.Services.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.Dispose();
        base.OnExit(e);
    }
}
