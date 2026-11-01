using System.Text.RegularExpressions;
using DocPoint.Application;
using DocPoint.Domain;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Exceptions;

namespace DocPoint.Infrastructure.Readers;

/// <summary>
/// Đọc PDF bằng PdfPig: ghép từ thành dòng, dòng thành đoạn theo khoảng cách dòng, nhận tiêu đề theo
/// cỡ chữ hoặc chữ đậm, và bỏ đầu trang, chân trang lặp lại.
/// </summary>
public sealed partial class PdfReader : IDocumentParser
{
    // Dòng bắt đầu bằng số hiệu thì luôn mở đoạn mới, kể cả khi khoảng cách dòng không đổi.
    [GeneratedRegex(@"^(\d+(\.\d+)*[.)]\s|[a-zđ]\)\s|(Điều|Chương|Mục|Phần)\s+\S+|[IVXL]+\.\s|[-•+]\s)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NumberedStart();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();

    public bool CanParse(string extension) => extension == ".pdf";

    public Task<IReadOnlyList<RawBlock>> ReadAsync(string path, CancellationToken ct = default)
    {
        try
        {
            using var stream = ReaderSupport.OpenRead(path);
            using var pdf = PdfDocument.Open(stream, new ParsingOptions { UseLenientParsing = true, SkipMissingFonts = true });

            var pages = new List<List<Line>>();
            foreach (var page in pdf.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                pages.Add(ReadLines(page));
            }

            var letters = pages.Sum(p => p.Sum(l => l.Text.Count(c => !char.IsWhiteSpace(c))));
            if (pages.Count > 0 && letters < 10 * pages.Count)
                throw new DocumentReadException(DocumentStatus.NoText,
                    $"\"{Path.GetFileName(path)}\" là bản scan, không có chữ để đọc. Tính năng nhận dạng chữ sẽ có ở bản sau.");

            RemoveRepeatedHeadersAndFooters(pages);
            return Task.FromResult<IReadOnlyList<RawBlock>>(BuildBlocks(pages));
        }
        catch (PdfDocumentEncryptedException ex)
        {
            throw new DocumentReadException(DocumentStatus.PasswordProtected,
                $"\"{Path.GetFileName(path)}\" có mật khẩu. Nhập mật khẩu để đọc hoặc bỏ qua file này.", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw ReaderSupport.Translate(ex, path);
        }
    }

    private sealed record Line(int Page, string Text, double Y, double Size, bool Bold, double PageHeight);

    private static List<Line> ReadLines(Page page)
    {
        var words = page.GetWords()
            .Where(w => w.Letters.Count > 0)
            .Select(w => (Word: w, Y: w.Letters[0].StartBaseLine.Y, Size: w.Letters.Average(l => l.PointSize)))
            .OrderByDescending(w => w.Y)
            .ToList();

        var lines = new List<Line>();
        var current = new List<(Word Word, double Y, double Size)>();

        void Flush()
        {
            if (current.Count == 0)
                return;

            var text = string.Join(" ", current.OrderBy(w => w.Word.BoundingBox.Left).Select(w => w.Word.Text));
            text = TextNormalizer.Normalize(text).Replace(' ', ' ').Trim();
            if (text.Length > 0)
            {
                var letters = current.SelectMany(w => w.Word.Letters).Where(l => !string.IsNullOrWhiteSpace(l.Value)).ToList();
                var bold = letters.Count > 0 && letters.All(IsBold);
                lines.Add(new Line(page.Number, text, current.Average(w => w.Y), current.Average(w => w.Size), bold, page.Height));
            }

            current.Clear();
        }

        foreach (var w in words)
        {
            if (current.Count > 0 && Math.Abs(current[0].Y - w.Y) > 0.5 * Math.Max(current[0].Size, w.Size))
                Flush();
            current.Add(w);
        }

        Flush();
        return lines;
    }

    private static bool IsBold(Letter letter)
    {
        if (letter.Font?.IsBold == true)
            return true;
        var name = letter.FontName ?? "";
        return name.Contains("Bold", StringComparison.OrdinalIgnoreCase) || name.EndsWith("-BD", StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveRepeatedHeadersAndFooters(List<List<Line>> pages)
    {
        if (pages.Count < 2)
            return;

        string Key(Line l) => Digits().Replace(l.Text, "#");
        bool InMargin(Line l) => l.Y > l.PageHeight * 0.92 || l.Y < l.PageHeight * 0.08;

        var needed = Math.Max(2, (int)Math.Ceiling(pages.Count * 0.5));
        var repeated = pages
            .SelectMany(p => p.Where(InMargin).Select(Key).Distinct())
            .GroupBy(k => k)
            .Where(g => g.Count() >= needed)
            .Select(g => g.Key)
            .ToHashSet();

        foreach (var page in pages)
            page.RemoveAll(l => InMargin(l) && repeated.Contains(Key(l)));
    }

    private static List<RawBlock> BuildBlocks(List<List<Line>> pages)
    {
        var all = pages.SelectMany(p => p).ToList();
        if (all.Count == 0)
            return [];

        // Cỡ chữ thân bài: cỡ chiếm nhiều chữ nhất.
        var bodySize = all.GroupBy(l => Math.Round(l.Size * 2) / 2)
            .OrderByDescending(g => g.Sum(l => l.Text.Length)).First().Key;
        var bodyBold = all.Where(l => Math.Abs(l.Size - bodySize) < 0.6).Sum(l => l.Bold ? l.Text.Length : 0)
            > all.Where(l => Math.Abs(l.Size - bodySize) < 0.6).Sum(l => l.Text.Length) / 2;

        bool IsHeading(Line l) =>
            l.Size >= bodySize * 1.1 || (!bodyBold && l.Bold && l.Text.Length <= 120);

        // Cấp tiêu đề: cỡ lớn hơn thì cấp cao hơn; chữ đậm cùng cỡ thân bài là cấp thấp nhất.
        var headingKeys = all.Where(IsHeading).Select(HeadingKey).Distinct().OrderByDescending(k => k).ToList();
        double HeadingKey(Line l) => Math.Round(l.Size * 2) / 2 + (l.Bold ? 0.1 : 0);

        // Khoảng cách dòng điển hình, đo giữa các dòng thân bài liền nhau trong cùng trang.
        var pitches = new List<double>();
        foreach (var page in pages)
        {
            for (var i = 1; i < page.Count; i++)
            {
                if (!IsHeading(page[i]) && !IsHeading(page[i - 1]))
                    pitches.Add(page[i - 1].Y - page[i].Y);
            }
        }

        var typicalPitch = pitches.Count > 0 ? pitches.OrderBy(p => p).ElementAt(pitches.Count / 2) : bodySize * 1.2;

        var blocks = new List<RawBlock>();
        foreach (var page in pages)
        {
            Line? previous = null;
            var text = new System.Text.StringBuilder();
            var startPage = 0;

            void FlushParagraph()
            {
                if (text.Length > 0)
                    blocks.Add(new RawBlock(text.ToString().Trim(), Page: startPage));
                text.Clear();
            }

            foreach (var line in page)
            {
                if (IsHeading(line))
                {
                    var key = HeadingKey(line);
                    var sameHeading = previous is not null && IsHeading(previous) && Math.Abs(HeadingKey(previous) - key) < 0.01
                        && previous.Y - line.Y < line.Size * 1.8 && !NumberedStart().IsMatch(line.Text);
                    if (sameHeading && blocks.Count > 0 && blocks[^1].Kind == BlockKind.Heading)
                    {
                        blocks[^1] = blocks[^1] with { Text = blocks[^1].Text + " " + line.Text };
                    }
                    else
                    {
                        FlushParagraph();
                        blocks.Add(new RawBlock(line.Text, BlockKind.Heading, HeadingLevel: headingKeys.IndexOf(key) + 1,
                            Page: line.Page, IsBold: line.Bold));
                    }
                }
                else
                {
                    var gap = previous is null ? 0 : previous.Y - line.Y;
                    var newParagraph = text.Length == 0 || previous is null || IsHeading(previous)
                        || gap > typicalPitch * 1.5 || NumberedStart().IsMatch(line.Text);
                    if (newParagraph)
                    {
                        FlushParagraph();
                        startPage = line.Page;
                    }
                    else
                    {
                        text.Append(' ');
                    }

                    text.Append(line.Text);
                }

                previous = line;
            }

            FlushParagraph();
        }

        return blocks;
    }
}
