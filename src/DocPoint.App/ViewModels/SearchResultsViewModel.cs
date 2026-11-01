using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocPoint.Application;
using DocPoint.Domain;

namespace DocPoint.App.ViewModels;

/// <summary>Một chỗ khớp trong cột kết quả bên trái.</summary>
public partial class HitRow(SearchHit hit) : ObservableObject
{
    public SearchHit Hit { get; } = hit;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>"Điểm c · khoản 7": bỏ phần Điều vì nó đã nằm ở dòng cha.</summary>
    public string ShortPosition
    {
        get
        {
            var parts = Hit.Position.Split(", ");
            return parts.Length > 1 && parts[^1].StartsWith("Điều", StringComparison.Ordinal)
                ? string.Join(" · ", parts[..^1])
                : string.Join(" · ", parts);
        }
    }

    /// <summary>"Chương II › Điều 7": đường dẫn từ tên tài liệu tới chỗ khớp, bỏ phần đầu và phần cuối.</summary>
    public string ParentPath
    {
        get
        {
            var parts = Hit.Breadcrumb.Split(" › ");
            return parts.Length > 2 ? string.Join(" › ", parts[1..^1]) : "";
        }
    }

    /// <summary>Đoạn trích không có ký tự đánh dấu, để hiện ở cột bên trái.</summary>
    public string PlainSnippet => Hit.Snippet
        .Replace(SearchHit.HighlightStart.ToString(), "")
        .Replace(SearchHit.HighlightEnd.ToString(), "");

    public IReadOnlyList<Paragraph> Paragraphs =>
        Hit.Body.Select(t => new Paragraph(t, t.StartsWith("Điều", StringComparison.Ordinal))).ToList();

    public string ValidityText => Hit.Validity switch
    {
        Validity.InForce => "Còn hiệu lực",
        Validity.Expired => "Hết hiệu lực",
        _ => "",
    };
}

/// <summary>Một đoạn trong khung đọc; dòng tiêu đề của Điều in đậm.</summary>
public sealed record Paragraph(string Text, bool IsHeading);

public sealed record PathGroup(string Path, IReadOnlyList<HitRow> Rows);

/// <summary>Các chỗ khớp của cùng một tài liệu.</summary>
public partial class DocGroup : ObservableObject
{
    public DocGroup(SearchHit first, IReadOnlyList<HitRow> rows)
    {
        Name = first.DocumentName;
        // Cột trái chật: chỉ lấy phần đầu ("Chính phủ"), phần còn lại (ngày hiệu lực) hiện ở trang đọc.
        Info = first.DocumentInfo.Split(" · ")[0];
        Validity = first.Validity;
        Note = first.ValidityNote;
        Count = rows.Count;
        PathGroups = rows.GroupBy(r => r.ParentPath).Select(g => new PathGroup(g.Key, g.ToList())).ToList();

        // Văn bản hết hiệu lực thu gọn, vẫn hiện tên và cảnh báo.
        _isExpanded = first.Validity != Validity.Expired;
    }

    public string Name { get; }
    public string Info { get; }
    public Validity Validity { get; }
    public string? Note { get; }
    public int Count { get; }
    public IReadOnlyList<PathGroup> PathGroups { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleText))]
    private bool _isExpanded;

    public string ToggleText => IsExpanded ? "Ẩn" : $"Hiện {Count} chỗ";

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}

/// <summary>Một vạch của bản đồ văn bản (cột hẹp bên phải).</summary>
public sealed record MapBar(double Length, bool IsHit, bool IsSelected, HitRow? Row);

public partial class SearchResultsViewModel : ObservableObject
{
    // Độ dài tương đối của 48 vạch, lấy từ mockup; sau này tính từ độ dài từng phần của văn bản.
    private static readonly int[] BarWidths =
    [
        60, 100, 90, 95, 70, 100, 85, 40, 100, 92, 98, 66, 100, 100, 88, 100, 72, 96, 100, 50, 90, 100, 84, 100,
        62, 100, 94, 78, 100, 88, 56, 100, 96, 90, 100, 70, 100, 82, 100, 64, 100, 92, 86, 100, 74, 98, 100, 58,
    ];

    private const double BarArea = 28;
    private const double BarStep = 7;

    private readonly ISearchService _search;
    private CancellationTokenSource? _cts;
    private List<HitRow> _rows = [];

    public SearchResultsViewModel(ISearchService search) => _search = search;

    public ObservableCollection<DocGroup> Groups { get; } = [];

    public ObservableCollection<MapBar> MapBars { get; } = [];

    [ObservableProperty]
    private string _query = "";

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private HitRow? _selected;

    [ObservableProperty]
    private int _hitCount;

    [ObservableProperty]
    private int _documentCount;

    [ObservableProperty]
    private bool _hasRows;

    [ObservableProperty]
    private Thickness _viewportMargin;

    [ObservableProperty]
    private string _copyFeedback = "";

    public bool HasSelection => Selected is not null;

    public bool HasQuery => !string.IsNullOrWhiteSpace(Query);

    public bool IsEmptyResult => HasQuery && !IsSearching && !HasRows;

    partial void OnQueryChanged(string value)
    {
        OnPropertyChanged(nameof(HasQuery));
        OnPropertyChanged(nameof(IsEmptyResult));
        _ = SearchAsync(value);
    }

    partial void OnIsSearchingChanged(bool value) => OnPropertyChanged(nameof(IsEmptyResult));

    partial void OnHasRowsChanged(bool value) => OnPropertyChanged(nameof(IsEmptyResult));

    partial void OnSelectedChanged(HitRow? value)
    {
        foreach (var row in _rows)
            row.IsSelected = row == value;

        RebuildMap();
    }

    private async Task SearchAsync(string query)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        IsSearching = true;
        try
        {
            var hits = await _search.SearchAsync(query, ct);
            if (ct.IsCancellationRequested)
                return;

            _rows = hits.Select(h => new HitRow(h)).ToList();
            Groups.Clear();
            foreach (var doc in _rows.GroupBy(r => r.Hit.DocumentName))
                Groups.Add(new DocGroup(doc.First().Hit, doc.ToList()));

            HitCount = _rows.Count;
            DocumentCount = Groups.Count;
            HasRows = _rows.Count > 0;
            Selected = _rows.FirstOrDefault();
        }
        catch (OperationCanceledException)
        {
            // Một lần tìm mới đã thay thế lần này.
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                IsSearching = false;
        }
    }

    private void RebuildMap()
    {
        MapBars.Clear();
        if (Selected is null)
            return;

        var sameDocument = _rows.Where(r => r.Hit.DocumentName == Selected.Hit.DocumentName)
            .GroupBy(r => r.Hit.MapIndex).ToDictionary(g => g.Key, g => g.First());

        for (var i = 0; i < BarWidths.Length; i++)
        {
            sameDocument.TryGetValue(i, out var row);
            MapBars.Add(new MapBar(BarWidths[i] * BarArea / 100, row is not null, row == Selected, row));
        }

        // Khung viền: đoạn đang xem, bao quanh chỗ khớp đang chọn.
        var top = Math.Max(0, Selected.Hit.MapIndex - 2) * BarStep - 4;
        ViewportMargin = new Thickness(-5, top, -5, 0);
    }

    [RelayCommand]
    private void Select(HitRow? row)
    {
        if (row is not null)
            Selected = row;
    }

    [RelayCommand]
    private async Task CopyCitationAsync()
    {
        if (Selected is null)
            return;

        try
        {
            Clipboard.SetText(Selected.Hit.Citation);
            CopyFeedback = "Đã sao chép";
        }
        catch (COMException)
        {
            // Clipboard đang bị app khác giữ.
            CopyFeedback = "Chưa sao chép được, thử lại";
        }

        await Task.Delay(2000);
        CopyFeedback = "";
    }
}
