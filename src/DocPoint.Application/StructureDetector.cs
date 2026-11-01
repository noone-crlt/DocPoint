using System.Text.RegularExpressions;
using DocPoint.Domain;

namespace DocPoint.Application;

/// <summary>
/// Biến chuỗi <see cref="RawBlock"/> thành cây <see cref="Node"/> có nhãn vị trí, cho mọi loại tài liệu.
/// Tiêu đề thật và chữ đánh số đầu dòng cùng mở node; khối không có gì thì thành nội dung của node đang mở.
/// Tài liệu gần như không có mẫu nào thì tách theo trang và đoạn.
/// </summary>
public static partial class StructureDetector
{
    /// <summary>Node có độ tin cậy dưới ngưỡng này được đánh dấu "cần xem".</summary>
    public const double ReviewThreshold = 0.75;

    private const int MaxWordsPerNode = 1500;
    private const int ChunkWords = 450;
    private const int LongTextLimit = 150;

    // Thứ hạng càng nhỏ càng cao trong cây.
    private const int RankPart = 1, RankChapter = 2, RankSection = 3, RankArticle = 4;
    private const int RankHeadingBase = 10, RankClause = 20, RankPoint = 30;

    private const string Sep = @"\s*[.:\-–]?\s*";

    [GeneratedRegex(@"^Phần\s+(?<n>[IVXLC]+|\d+)" + Sep + "(?<t>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PartPattern();

    [GeneratedRegex(@"^Chương\s+(?<n>[IVXLC]+|\d+)" + Sep + "(?<t>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ChapterPattern();

    [GeneratedRegex(@"^Mục\s+(?<n>\d+(?:\.\d+)*|[IVXLC]+)" + Sep + "(?<t>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SectionPattern();

    [GeneratedRegex(@"^Điều\s+(?<n>\d+[a-zđ]?)" + @"\s*[.:]?\s*(?<t>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ArticlePattern();

    // Mỗi tầng tối đa 3 chữ số, để "1.250.000.000 đồng" không bị nhầm với số thứ tự.
    [GeneratedRegex(@"^(?<n>\d{1,3}(?:\.\d{1,3})+)\.?\s+(?<t>\S.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex MultiLevelPattern();

    [GeneratedRegex(@"^(?<n>[IVX]{1,4})[.)]\s+(?<t>\S.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex RomanPattern();

    [GeneratedRegex(@"^(?<n>\d{1,3})[.)]\s+(?<t>\S.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^(?<n>[a-zđ])\)\s+(?<t>\S.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex LetterPattern();

    [GeneratedRegex(@"(?<=[.!?;])\s+")]
    private static partial Regex SentenceBreak();

    public static IReadOnlyList<Node> Detect(IReadOnlyList<RawBlock> blocks)
    {
        var builder = new TreeBuilder();
        var markers = blocks.Count(b => b.Kind != BlockKind.TableRow && IsMarkerCandidate(b));

        // Quá ít mẫu thì coi như không có cấu trúc: tách theo trang và đoạn.
        if (markers < 2)
            builder.BuildFlat(blocks);
        else
            builder.BuildStructured(blocks);

        return builder.Flatten();
    }

    private static bool IsMarkerCandidate(RawBlock b) =>
        b.Kind == BlockKind.Heading || (b.Numbering is not null && b.Numbering != "•") || StartsWithPattern(JoinNumbering(b));

    private static bool StartsWithPattern(string s) =>
        PartPattern().IsMatch(s) || ChapterPattern().IsMatch(s) || ArticlePattern().IsMatch(s) || SectionPattern().IsMatch(s)
        || MultiLevelPattern().IsMatch(s) || RomanPattern().IsMatch(s) || NumberPattern().IsMatch(s) || LetterPattern().IsMatch(s);

    private static string JoinNumbering(RawBlock b) =>
        b.Numbering is null or "•" ? b.Text : b.Numbering + " " + b.Text;

    private sealed class N(Node data, int rank, bool textIsContent)
    {
        public Node Data { get; } = data;
        public int Rank { get; } = rank;
        public bool TextIsContent { get; } = textIsContent;
        public N? Parent { get; set; }
        public List<N> Children { get; } = [];
        public List<string> Lines { get; } = [];
        public bool ExactPage { get; set; }
        public int Depth { get; init; }
    }

    private sealed class TreeBuilder
    {
        private readonly List<N> _roots = [];
        private readonly List<N> _stack = [];
        private int _loose;
        private N? _page;
        private int _paragraphOnPage;
        private string[]? _tableHeader;
        private int _tableRow;
        private bool _inTable;

        // ----- Tách theo trang và đoạn -----

        public void BuildFlat(IReadOnlyList<RawBlock> blocks)
        {
            var exactPages = blocks.Any(b => b.Page is not null && !b.PageIsApproximate);
            for (var i = 0; i < blocks.Count; i++)
            {
                var b = blocks[i];
                if (b.Kind == BlockKind.TableRow)
                {
                    AddTableRow(blocks, i, current: _page);
                    continue;
                }

                _inTable = false;
                N? parent = null;
                string number;
                if (exactPages && b.Page is { } page)
                {
                    if (_page is null || _page.Data.Page != page)
                    {
                        _page = New(NodeKind.Page, page.ToString(), "", page, RankPart, false, exact: true);
                        _page.Parent = null;
                        _roots.Add(_page);
                        _paragraphOnPage = 0;
                    }

                    parent = _page;
                    number = (++_paragraphOnPage).ToString();
                }
                else
                {
                    number = (++_loose).ToString();
                }

                var node = New(NodeKind.Paragraph, number, "", b.Page, RankPart + 1, true, exact: exactPages && !b.PageIsApproximate);
                node.Lines.Add(b.Text);
                Attach(node, parent);
            }
        }

        // ----- Tách theo tiêu đề và đánh số -----

        public void BuildStructured(IReadOnlyList<RawBlock> blocks)
        {
            for (var i = 0; i < blocks.Count; i++)
            {
                var b = blocks[i];
                if (b.Kind == BlockKind.TableRow)
                {
                    AddTableRow(blocks, i, current: _stack.LastOrDefault());
                    continue;
                }

                _inTable = false;
                var marker = Classify(b);
                if (marker is null)
                {
                    AddText(b, b.Numbering == "•" ? "- " + b.Text : b.Text);
                    continue;
                }

                while (_stack.Count > 0 && _stack[^1].Rank >= marker.Rank)
                    _stack.RemoveAt(_stack.Count - 1);

                // Node dạng nội dung (khoản, điểm...) giữ chữ ở Content, không lặp lại ở Title.
                var node = New(marker.Kind, marker.Number, marker.TextIsContent ? "" : marker.Title, b.Page, marker.Rank, marker.TextIsContent,
                    exact: b.Page is not null && !b.PageIsApproximate, depth: marker.Depth);
                if (marker.TextIsContent)
                    node.Lines.Add(marker.Title);

                Attach(node, _stack.LastOrDefault());
                _stack.Add(node);
            }
        }

        private void AddText(RawBlock b, string text)
        {
            if (_stack.Count > 0)
            {
                _stack[^1].Lines.Add(text);
                return;
            }

            // Phần mở đầu trước tiêu đề đầu tiên: mỗi khối là một đoạn riêng.
            var node = New(NodeKind.Paragraph, (++_loose).ToString(), "", b.Page, RankPart + 1, true,
                exact: b.Page is not null && !b.PageIsApproximate);
            node.Lines.Add(text);
            Attach(node, null);
        }

        private Marker? Classify(RawBlock b)
        {
            var s = JoinNumbering(b);
            var isRealHeading = b.Kind == BlockKind.Heading;

            if (Match(PartPattern(), s) is { } part)
                return new(NodeKind.Part, part.n, part.t, RankPart, false, 1);
            if (Match(ChapterPattern(), s) is { } chapter)
                return new(NodeKind.Chapter, chapter.n, chapter.t, RankChapter, false, 1);
            if (Match(ArticlePattern(), s) is { } article)
                return new(NodeKind.Article, article.n, article.t, RankArticle, false, 1);
            if (Match(SectionPattern(), s) is { } section)
                return new(NodeKind.Section, section.n, section.t, RankSection, false, 1);

            var enclosingArticle = _stack.LastOrDefault(n => n.Data.Kind == NodeKind.Article);

            if (Match(MultiLevelPattern(), s) is { } multi)
            {
                var first = multi.n.Split('.')[0];
                if (enclosingArticle is not null && enclosingArticle.Data.Number == first)
                    return new(NodeKind.Clause, multi.n, multi.t, RankClause, true, 2);

                var depth = multi.n.Count(c => c == '.') + 1;
                return new(NodeKind.Heading, multi.n, multi.t, RankHeadingBase + depth, !isRealHeading && multi.t.Length > LongTextLimit, depth);
            }

            if (Match(RomanPattern(), s) is { } roman)
                return new(NodeKind.Heading, roman.n, roman.t, RankHeadingBase + 1, !isRealHeading && roman.t.Length > LongTextLimit, 1);

            if (Match(NumberPattern(), s) is { } number)
            {
                if (enclosingArticle is not null)
                    return new(NodeKind.Clause, number.n, number.t, RankClause, true, 2);
                return new(NodeKind.Heading, number.n, number.t, RankHeadingBase + 1, !isRealHeading && number.t.Length > LongTextLimit, 1);
            }

            if (Match(LetterPattern(), s) is { } letter)
                return new(NodeKind.Point, letter.n, letter.t, RankPoint, true, 3);

            if (isRealHeading)
            {
                var level = b.HeadingLevel ?? 1;
                return new(NodeKind.Heading, "", b.Text, RankHeadingBase + level, false, level);
            }

            return null;
        }

        private static (string n, string t)? Match(Regex pattern, string s)
        {
            var m = pattern.Match(s);
            return m.Success ? (m.Groups["n"].Value, m.Groups["t"].Value.Trim()) : null;
        }

        // ----- Bảng -----

        private void AddTableRow(IReadOnlyList<RawBlock> blocks, int index, N? current)
        {
            var row = blocks[index];
            var cells = row.Cells ?? [row.Text];

            if (!_inTable)
            {
                _inTable = true;
                _tableRow = 0;
                _tableHeader = null;

                var nextIsRow = index + 1 < blocks.Count && blocks[index + 1].Kind == BlockKind.TableRow;
                if (nextIsRow && cells.Count >= 2 && cells.All(c => c.Length > 0))
                {
                    // Dòng đầu là tiêu đề cột: giữ chữ của nó ở node chứa bảng để vẫn tìm được.
                    _tableHeader = cells.ToArray();
                    (current ?? EnsureHolder(row)).Lines.Add(row.Text);
                    return;
                }
            }

            var parts = new List<string>();
            for (var c = 0; c < cells.Count; c++)
            {
                if (cells[c].Length == 0)
                    continue;
                parts.Add(_tableHeader is not null && c < _tableHeader.Length ? $"{_tableHeader[c]}: {cells[c]}" : cells[c]);
            }

            var node = New(NodeKind.Row, (++_tableRow).ToString(), "", row.Page, RankPart + 2, true,
                exact: row.Page is not null && !row.PageIsApproximate);
            node.Lines.Add(string.Join("; ", parts));
            Attach(node, current);
        }

        private N EnsureHolder(RawBlock b)
        {
            var node = New(NodeKind.Paragraph, (++_loose).ToString(), "", b.Page, RankPart + 1, true,
                exact: b.Page is not null && !b.PageIsApproximate);
            Attach(node, null);
            return node;
        }

        // ----- Dựng cây -----

        private static N New(NodeKind kind, string number, string title, int? page, int rank, bool textIsContent, bool exact, int depth = 0) =>
            new(new Node { Kind = kind, Number = number, Title = title, Page = page }, rank, textIsContent) { ExactPage = exact, Depth = depth };

        private void Attach(N node, N? parent)
        {
            node.Parent = parent;
            (parent?.Children ?? _roots).Add(node);
            Review.Check(node);
        }

        public IReadOnlyList<Node> Flatten()
        {
            var result = new List<Node>();
            foreach (var root in _roots)
                Emit(root, null, result);
            return result;
        }

        private static void Emit(N n, int? parentOrdinal, List<Node> result)
        {
            var data = n.Data;
            data.Ordinal = result.Count;
            data.ParentOrdinal = parentOrdinal;
            data.Content = TextNormalizer.Normalize(string.Join("\n", n.Lines)).Trim();
            data.Label = LabelOf(n);
            result.Add(data);

            var children = n.Children;
            if (CountWords(data.Content) > MaxWordsPerNode)
                children = SplitLongContent(n);

            foreach (var child in children)
                Emit(child, data.Ordinal, result);
        }

        // Nội dung quá dài chia thành các đoạn con để kết quả tìm vẫn chỉ ra đúng chỗ.
        private static List<N> SplitLongContent(N n)
        {
            var chunks = Chunk(n.Data.Content);
            n.Data.Content = "";

            var pieces = new List<N>();
            for (var i = 0; i < chunks.Count; i++)
            {
                var piece = new N(new Node { Kind = NodeKind.Paragraph, Number = (i + 1).ToString(), Page = n.Data.Page }, n.Rank + 1, true)
                {
                    Parent = n,
                    ExactPage = n.ExactPage,
                };
                piece.Lines.Add(chunks[i]);
                pieces.Add(piece);
            }

            pieces.AddRange(n.Children);
            return pieces;
        }

        private static List<string> Chunk(string text)
        {
            var chunks = new List<string>();
            var current = new List<string>();
            var words = 0;

            foreach (var sentence in SentenceBreak().Split(text.Replace('\n', ' ')))
            {
                var w = CountWords(sentence);
                if (words > 0 && words + w > ChunkWords)
                {
                    chunks.Add(string.Join(" ", current));
                    current.Clear();
                    words = 0;
                }

                current.Add(sentence);
                words += w;
            }

            if (current.Count > 0)
                chunks.Add(string.Join(" ", current));
            return chunks;
        }

        private static int CountWords(string s) => s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

        // ----- Nhãn vị trí -----

        private static string LabelOf(N n)
        {
            var chain = new List<N>();
            for (var cur = n; cur is not null; cur = cur.Parent)
                chain.Add(cur);
            chain.Reverse();

            // Từ Điều trở xuống, nhãn không cần Chương, Mục bên trên: số Điều đã đủ để tra.
            var article = chain.FindLastIndex(c => c.Data.Kind == NodeKind.Article);
            if (article > 0)
                chain.RemoveRange(0, article);

            // Mục 1.1.2 đã bao hàm Mục 1.1: bỏ tiêu đề cha có số là phần đầu của số con.
            chain.RemoveAll(c => c != n && c.Data.Kind == NodeKind.Heading && c.Data.Number.Length > 0
                && n.Data.Number.StartsWith(c.Data.Number + ".", StringComparison.Ordinal));

            var parts = chain.Select(c => c.Data.ToLabelPart()).ToList();
            var useNodePage = n.ExactPage && n.Data.Page is not null;
            return LocationLabel.Build(parts, useNodePage ? n.Data.Page : null);
        }
    }

    private sealed record Marker(NodeKind Kind, string Number, string Title, int Rank, bool TextIsContent, int Depth);

    // ----- Độ tin cậy -----

    private static class Review
    {
        public static void Check(N node)
        {
            var d = node.Data;
            var siblings = node.Parent?.Children;
            var previous = siblings?.LastOrDefault(s => s != node && s.Data.Kind == d.Kind);

            string? reason = null;
            if (previous is not null && d.Number.Length > 0)
                reason = SequenceProblem(previous.Data.Number, d.Number);

            if (reason is null && d.Kind == NodeKind.Heading && d.Number.Length > 0 && node.Parent is { Data: { Kind: NodeKind.Heading } parent } && parent.Number.Length > 0)
            {
                var gap = node.Depth - parent.Number.Count(c => c == '.') - 1;
                if (gap > 1)
                    reason = $"Cấp mục nhảy từ {parent.Number} sang {d.Number}";
            }

            if (reason is null)
                return;

            d.Confidence = 0.6;
            d.NeedsReview = true;
            d.ReviewReason = reason;
        }

        private static string? SequenceProblem(string previous, string current)
        {
            var p = previous.Split('.');
            var c = current.Split('.');
            if (p.Length != c.Length || !p[..^1].SequenceEqual(c[..^1]))
                return null;

            if (!TryOrder(p[^1], out var before) || !TryOrder(c[^1], out var after))
                return null;

            if (after == before + 1)
                return null;
            if (after == before)
                return $"Số thứ tự {current} bị lặp";
            return after > before
                ? $"Số thứ tự nhảy từ {previous} sang {current}"
                : $"Số thứ tự lùi từ {previous} về {current}";
        }

        private static bool TryOrder(string s, out int value)
        {
            if (int.TryParse(s, out value))
                return true;
            if (s.Length == 1 && char.IsLetter(s[0]))
            {
                value = char.ToLowerInvariant(s[0]);
                return true;
            }

            if (s.All(ch => "IVXLC".Contains(ch)))
            {
                value = RomanValue(s);
                return true;
            }

            value = 0;
            return false;
        }

        private static int RomanValue(string s)
        {
            int Of(char c) => c switch { 'I' => 1, 'V' => 5, 'X' => 10, 'L' => 50, 'C' => 100, _ => 0 };
            var total = 0;
            for (var i = 0; i < s.Length; i++)
                total += i + 1 < s.Length && Of(s[i]) < Of(s[i + 1]) ? -Of(s[i]) : Of(s[i]);
            return total;
        }
    }
}
