using System.Text;
using DocPoint.Application;
using DocPoint.Domain;

namespace DocPoint.Infrastructure.Search;

/// <summary>
/// Bản thật của <see cref="ISearchService"/>: tìm bằng <see cref="ISearchIndex"/> rồi đổi từng kết quả sang
/// <see cref="SearchHit"/> mà màn Kết quả cần (nhãn vị trí, đường dẫn cha, đoạn trích có đánh dấu, bản đồ).
/// </summary>
public sealed class SqliteSearchService(ISearchIndex index, IDocumentRepository repository) : ISearchService
{
    private const int MapSlots = 48;
    private const char Start = '\u0002';
    private const char End = '\u0003';

    public async Task<IReadOnlyList<string>> GetSuggestionsAsync(CancellationToken ct = default) =>
        await index.SuggestAsync("", 8, ct);

    public Task<IReadOnlyList<string>> SuggestAsync(string prefix, CancellationToken ct = default) =>
        index.SuggestAsync(prefix, 8, ct);

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken ct = default)
    {
        var matches = await index.SearchAsync(new SearchQuery(query), ct);
        var trees = new Dictionary<long, IReadOnlyList<Node>>();
        var hits = new List<SearchHit>(matches.Count);

        foreach (var match in matches)
        {
            ct.ThrowIfCancellationRequested();
            var docId = match.Document.Id;
            if (!trees.TryGetValue(docId, out var nodes))
                trees[docId] = nodes = await repository.GetNodesAsync(docId, ct);

            hits.Add(ToHit(match, nodes));
        }

        return hits;
    }

    private static SearchHit ToHit(NodeMatch match, IReadOnlyList<Node> nodes)
    {
        var node = match.Node;
        var document = match.Document;

        // Đường dẫn: tên tài liệu › các cấp cha từ trên xuống › chính node này.
        var ancestors = new List<string>();
        for (var parent = nodes[node.Ordinal].ParentOrdinal; parent is { } p; parent = nodes[p].ParentOrdinal)
            ancestors.Add(nodes[p].ToLabelPart().Text);
        ancestors.Reverse();
        var breadcrumb = string.Join(" › ", new[] { document.FileName }.Concat(ancestors).Append(node.ToLabelPart().Text));

        var folder = Path.GetFileName(Path.GetDirectoryName(document.Path)) ?? "";
        var info = $"{folder} · sửa {document.ModifiedAt.ToLocalTime():dd/MM/yyyy}";

        var mapIndex = nodes.Count == 0 ? 0 : Math.Min(MapSlots - 1, (int)((double)node.Ordinal / nodes.Count * MapSlots));

        return new SearchHit(
            node.Label, breadcrumb, document.FileName, info, match.Snippet, Body(match),
            Validity.NotApplicable, MapIndex: mapIndex);
    }

    // Tiêu đề (nếu có) rồi từng dòng nội dung, chỗ khớp đánh dấu bằng ký tự của SearchHit.
    private static IReadOnlyList<string> Body(NodeMatch match)
    {
        var node = match.Node;
        var lines = new List<string>();

        if (node.Title.Length > 0)
        {
            var heading = $"{node.Kind.DisplayName()} {node.Number}. ";
            lines.Add(heading + Mark(node.Title, match.TitleMatches ?? [], 0));
        }

        var offset = 0;
        foreach (var line in node.Content.Split('\n'))
        {
            lines.Add(Mark(line, match.Matches, offset));
            offset += line.Length + 1;
        }

        return lines.Where(l => l.Length > 0).ToList();
    }

    private static string Mark(string text, IReadOnlyList<TextRange> ranges, int offset)
    {
        var sb = new StringBuilder();
        var cursor = 0;
        foreach (var r in ranges.Where(r => r.Start >= offset && r.Start + r.Length <= offset + text.Length))
        {
            var start = r.Start - offset;
            sb.Append(text, cursor, start - cursor).Append(Start).Append(text, start, r.Length).Append(End);
            cursor = start + r.Length;
        }

        return sb.Append(text, cursor, text.Length - cursor).ToString();
    }
}
