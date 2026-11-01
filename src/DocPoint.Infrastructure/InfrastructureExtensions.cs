using DocPoint.Application;
using DocPoint.Infrastructure.Logging;
using DocPoint.Infrastructure.Persistence;
using DocPoint.Infrastructure.Readers;
using DocPoint.Infrastructure.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocPoint.Infrastructure;

public static class InfrastructureExtensions
{
    /// <summary>Đăng ký các bản cài đặt thật. Người gọi phải gọi <see cref="DocPointDatabase.Initialize"/> một lần khi mở app.</summary>
    public static IServiceCollection AddDocPointInfrastructure(this IServiceCollection services, AppPaths? paths = null)
    {
        paths ??= AppPaths.Default();
        services.AddSingleton(paths);
        services.AddSingleton(new DocPointDatabase(paths.DatabasePath));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IDocumentRepository, SqliteDocumentRepository>();
        services.AddSingleton<ISearchIndex, SqliteSearchIndex>();
        services.AddSingleton<IDocumentParser, DocxReader>();
        services.AddSingleton<IDocumentParser, PdfReader>();
        services.AddSingleton<IDocumentParser, TextFileReader>();
        services.AddSingleton<DocumentIndexer>();
        services.AddSingleton<IFolderRepository, SqliteFolderRepository>();
        services.AddSingleton(new ScanOptions());
        services.AddSingleton<IFolderScanner, FolderScanner>();
        services.AddSingleton<FolderWatchService>();
        services.AddSingleton<SqliteLibraryService>();
        return services;
    }

    public static ILoggingBuilder AddDocPointFileLogger(this ILoggingBuilder logging, AppPaths? paths = null)
    {
        logging.Services.AddSingleton<ILoggerProvider>(new FileLoggerProvider((paths ?? AppPaths.Default()).LogsDirectory));
        return logging;
    }
}
