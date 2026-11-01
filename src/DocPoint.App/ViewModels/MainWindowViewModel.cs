using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocPoint.App.Services;
using DocPoint.App.Views.Pages;
using DocPoint.Application;

namespace DocPoint.App.ViewModels;

public enum Section { None, Home, Library, Folders, Pinned, Backup, Settings }

public partial class MainWindowViewModel : ObservableObject
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private readonly ISearchService _search;
    private readonly ILibraryService _library;
    private readonly IFolderWatchService _folderWatch;
    private readonly IPageNavigator _navigator;
    private readonly SearchResultsViewModel _results;

    [ObservableProperty]
    private string _searchPlaceholder = "Tìm trong tài liệu…";

    [ObservableProperty]
    private IReadOnlyList<string> _suggestions = [];

    [ObservableProperty]
    private bool _isReading;

    [ObservableProperty]
    private Section _activeSection;

    public MainWindowViewModel(ISearchService search, ILibraryService library, IFolderWatchService folderWatch,
        IPageNavigator navigator, SearchResultsViewModel results)
    {
        _search = search;
        _library = library;
        _folderWatch = folderWatch;
        _navigator = navigator;
        _results = results;

        _isReading = folderWatch.IsReading;
        folderWatch.StatusChanged += (_, _) =>
            System.Windows.Application.Current.Dispatcher.Invoke(() => IsReading = _folderWatch.IsReading);

        navigator.Navigated += page => ActiveSection = SectionOf(page);
    }

    public async Task LoadAsync()
    {
        var count = await _library.CountDocumentsAsync();
        SearchPlaceholder = $"Tìm trong {count.ToString("N0", Vietnamese)} tài liệu…";
        Suggestions = await _search.GetSuggestionsAsync();
    }

    [RelayCommand]
    private void Navigate(Type page) => _navigator.Navigate(page);

    [RelayCommand]
    private void Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return;

        _results.Query = query.Trim();
        _navigator.Navigate(typeof(SearchResultsPage));
    }

    private static Section SectionOf(Type page) =>
        page == typeof(HomePage) ? Section.Home
        : page == typeof(LibraryPage) ? Section.Library
        : page == typeof(FoldersPage) ? Section.Folders
        : page == typeof(PinnedPage) ? Section.Pinned
        : page == typeof(BackupPage) ? Section.Backup
        : page == typeof(SettingsPage) ? Section.Settings
        : Section.None;
}
