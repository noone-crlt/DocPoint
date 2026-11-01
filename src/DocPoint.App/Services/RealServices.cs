using DocPoint.Application;
using DocPoint.Infrastructure.Persistence;
using DocPoint.Infrastructure.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocPoint.App.Services;

/// <summary>
/// Thay các fake bằng bản thật khi Services:UseFake = false. Mỗi chức năng backend thêm dòng của mình
/// vào đây khi đạt tiêu chí "Xong khi" (services.Replace thế đúng service đang có).
/// </summary>
internal static class RealServices
{
    public static void Register(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<ISearchService, SqliteSearchService>());
        services.Replace(ServiceDescriptor.Singleton<IFolderWatchService>(sp => sp.GetRequiredService<FolderWatchService>()));
        services.Replace(ServiceDescriptor.Singleton<ILibraryService>(sp => sp.GetRequiredService<SqliteLibraryService>()));
    }
}
