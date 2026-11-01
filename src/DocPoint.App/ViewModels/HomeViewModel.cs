using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocPoint.App.Views.Pages;
using DocPoint.Application;
using DocPoint.App.Services;
using DocPoint.Domain;

namespace DocPoint.App.ViewModels;

public enum RecentFilter { All, Legal, Word, Pdf, Excel }

public partial class RecentRow(RecentDocument document, string groupTitle, string timeText) : ObservableObject
{
    public RecentDocument Document { get; } = document;

    public string GroupTitle { get; } = groupTitle;

    public string TimeText { get; } = timeText;

    [ObservableProperty]
    private bool _isPinned = document.IsPinned;
}

public partial class HomeViewModel(IRecentFilesService recent, IPageNavigator navigator) : ObservableObject
{
    private IReadOnlyList<RecentRow> _all = [];

    public ObservableCollection<RecentRow> Rows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAll), nameof(IsLegal), nameof(IsWord), nameof(IsPdf), nameof(IsExcel))]
    private RecentFilter _filter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFirstRun), nameof(IsNoMatch))]
    private bool _hasAny;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNoMatch))]
    private bool _hasVisible;

    public bool IsAll => Filter == RecentFilter.All;
    public bool IsLegal => Filter == RecentFilter.Legal;
    public bool IsWord => Filter == RecentFilter.Word;
    public bool IsPdf => Filter == RecentFilter.Pdf;
    public bool IsExcel => Filter == RecentFilter.Excel;

    /// <summary>Chưa mở tài liệu nào: trang dạy cách bắt đầu thay vì để trống.</summary>
    public bool IsFirstRun => !HasAny;

    public bool IsNoMatch => HasAny && !HasVisible;

    public async Task LoadAsync()
    {
        var docs = await recent.GetRecentAsync();
        _all = docs
            .OrderByDescending(d => d.OpenedAt)
            .Select(d => new RecentRow(d, GroupTitle(d.OpenedAt), FormatTime(d.OpenedAt)))
            .ToList();
        HasAny = _all.Count > 0;
        Rebuild();
    }

    partial void OnFilterChanged(RecentFilter value) => Rebuild();

    [RelayCommand]
    private void SetFilter(RecentFilter filter) => Filter = filter;

    [RelayCommand]
    private void TogglePin(RecentRow row) => row.IsPinned = !row.IsPinned;

    [RelayCommand]
    private void AddFolder() => navigator.Navigate(typeof(FoldersPage));

    private void Rebuild()
    {
        Rows.Clear();
        foreach (var row in _all.Where(Matches))
            Rows.Add(row);

        HasVisible = Rows.Count > 0;
    }

    private bool Matches(RecentRow row) => Filter switch
    {
        RecentFilter.Legal => row.Document.Kind == DocumentKind.Legal,
        RecentFilter.Word => row.Document.Kind == DocumentKind.Word,
        RecentFilter.Pdf => row.Document.Kind == DocumentKind.Pdf,
        RecentFilter.Excel => row.Document.Kind == DocumentKind.Excel,
        _ => true,
    };

    private static string GroupTitle(DateTime openedAt) => openedAt.Date == DateTime.Today ? "Hôm nay"
        : openedAt.Date == DateTime.Today.AddDays(-1) ? "Hôm qua"
        : openedAt.Date > DateTime.Today.AddDays(-8) ? "Tuần trước"
        : "Cũ hơn";

    // Hôm nay và hôm qua chỉ ghi giờ; xa hơn ghi thứ: "T5 · 15:12", "CN · 09:04".
    private static string FormatTime(DateTime openedAt)
    {
        if (openedAt.Date >= DateTime.Today.AddDays(-1))
            return openedAt.ToString("HH:mm");

        var day = openedAt.DayOfWeek == DayOfWeek.Sunday ? "CN" : $"T{(int)openedAt.DayOfWeek + 1}";
        return $"{day} · {openedAt:HH:mm}";
    }
}
