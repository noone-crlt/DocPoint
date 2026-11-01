using System.Windows;
using DocPoint.App.Services;
using DocPoint.App.Services.Fakes;
using DocPoint.App.ViewModels;
using DocPoint.App.Views;
using DocPoint.App.Views.Pages;
using DocPoint.Application;
using DocPoint.Infrastructure;
using DocPoint.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocPoint.App;

public partial class App
{
    private readonly IHost _host = Host.CreateDefaultBuilder()
        .UseContentRoot(AppContext.BaseDirectory)
        .ConfigureLogging(logging => logging.AddDocPointFileLogger())
        .ConfigureServices((context, services) =>
        {
            services.AddSingleton(AppSettings.Load());
            services.AddDocPointInfrastructure();

            // Services:UseFake trong appsettings.json chọn dữ liệu mẫu hay bản thật.
            // Mỗi service thật thay fake tương ứng khi chức năng của nó đạt tiêu chí "Xong khi".
            services.AddSingleton<ISearchService, FakeSearchService>();
            services.AddSingleton<ILibraryService, FakeLibraryService>();
            services.AddSingleton<IFolderWatchService, FakeFolderWatchService>();
            services.AddSingleton<IRecentFilesService, FakeRecentFilesService>();

            if (!context.Configuration.GetValue("Services:UseFake", true))
                RealServices.Register(services);

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

        var config = _host.Services.GetRequiredService<IConfiguration>();
        if (!config.GetValue("Services:UseFake", true))
            StartRealServices(config);

        ThemeSwitcher.Track();
        _host.Services.GetRequiredService<MainWindow>().Show();
    }

    private void StartRealServices(IConfiguration config)
    {
        _host.Services.GetRequiredService<DocPointDatabase>().Initialize();
        var watch = _host.Services.GetRequiredService<IFolderWatchService>();

        // Dev:IndexFolder: thư mục để thử khi trang Thư mục chưa có giao diện thêm thư mục.
        var devFolder = config["Dev:IndexFolder"];
        if (!string.IsNullOrWhiteSpace(devFolder))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await watch.AddFolderAsync(devFolder);
                }
                catch (FolderException)
                {
                    // Đã theo dõi từ lần chạy trước.
                }
            });
        }

        watch.ScanAll();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.Dispose();
        base.OnExit(e);
    }
}
