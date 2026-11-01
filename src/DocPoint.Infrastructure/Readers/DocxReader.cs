using System.Text;
using DocPoint.Application;
using DocPoint.Domain;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocPoint.Infrastructure.Readers;

/// <summary>
/// Đọc .docx. Ba điểm không hiển nhiên: cấp tiêu đề lấy từ outline level chứ không từ tên style;
/// chữ đánh số tự động ("Điều 1.", "a)") phải tính từ numbering.xml; số trang chỉ ước lượng
/// từ dấu ngắt trang Word ghi lúc lưu.
/// </summary>
public sealed class DocxReader : IDocumentParser
{
    public bool CanParse(string extension) => extension == ".docx";

    public Task<IReadOnlyList<RawBlock>> ReadAsync(string path, CancellationToken ct = default)
    {
        try
        {
            using var stream = ReaderSupport.OpenRead(path);
            if (ReaderSupport.LooksEncryptedOffice(stream))
                throw new DocumentReadException(DocumentStatus.PasswordProtected,
                    $"\"{Path.GetFileName(path)}\" có mật khẩu. Nhập mật khẩu để đọc hoặc bỏ qua file này.");

            using var doc = WordprocessingDocument.Open(stream, isEditable: false);
            var main = doc.MainDocumentPart ?? throw new InvalidDataException("Thiếu phần nội dung chính.");
            var body = main.Document?.Body ?? throw new InvalidDataException("Thiếu thân văn bản.");

            var context = new ReadContext(main, body);
            context.ReadChildren(body, ct);
            return Task.FromResult<IReadOnlyList<RawBlock>>(context.Blocks);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw ReaderSupport.Translate(ex, path);
        }
    }

    private sealed class ReadContext
    {
        private readonly Dictionary<string, Style> _styles;
        private readonly Numbering? _numbering;
        private readonly NumberingState _counters = new();
        private readonly bool _hasRenderedBreaks;
        private int _page = 1;

        public ReadContext(MainDocumentPart main, Body body)
        {
            _styles = main.StyleDefinitionsPart?.Styles?.Elements<Style>()
                .Where(s => s.StyleId?.Value is not null)
                .ToDictionary(s => s.StyleId!.Value!) ?? [];
            _numbering = main.NumberingDefinitionsPart?.Numbering;
            _hasRenderedBreaks = body.Descendants<LastRenderedPageBreak>().Any();
            HasPageInfo = _hasRenderedBreaks || body.Descendants<Break>().Any(b => b.Type?.Value == BreakValues.Page);
        }

        public List<RawBlock> Blocks { get; } = [];

        private bool HasPageInfo { get; }

        public void ReadChildren(OpenXmlElement parent, CancellationToken ct)
        {
            foreach (var child in parent.ChildElements)
            {
                ct.ThrowIfCancellationRequested();
                switch (child)
                {
                    case Paragraph p:
                        ReadParagraph(p);
                        break;
                    case Table t:
                        ReadTable(t);
                        break;
                    case SdtBlock sdt when sdt.SdtContentBlock is { } content:
                        ReadChildren(content, ct);
                        break;
                }
            }
        }

        private void ReadParagraph(Paragraph p)
        {
            var (text, startPage) = ExtractText(p);
            if (text.Length == 0)
                return;

            var level = ResolveOutlineLevel(p);
            var numbering = ResolveNumbering(p);

            Blocks.Add(new RawBlock(
                text,
                level is null ? BlockKind.Paragraph : BlockKind.Heading,
                HeadingLevel: level,
                Numbering: numbering,
                Page: HasPageInfo ? startPage : null,
                PageIsApproximate: HasPageInfo,
                IsBold: IsAllBold(p)));
        }

        private void ReadTable(Table table)
        {
            foreach (var row in table.Elements<TableRow>())
            {
                var cells = row.Elements<TableCell>().Select(CellText).ToList();
                if (cells.All(c => c.Length == 0))
                    continue;

                Blocks.Add(new RawBlock(
                    string.Join(" | ", cells),
                    BlockKind.TableRow,
                    Page: HasPageInfo ? _page : null,
                    PageIsApproximate: HasPageInfo,
                    Cells: cells));
            }
        }

        private string CellText(TableCell cell)
        {
            var parts = cell.Descendants<Paragraph>().Select(p => ExtractText(p, advancePage: false).Text).Where(t => t.Length > 0);
            return string.Join(" ", parts);
        }

        /// <summary>Chữ của đoạn và trang nơi đoạn bắt đầu; mỗi dấu ngắt trang gặp trên đường đi tăng bộ đếm trang.</summary>
        private (string Text, int StartPage) ExtractText(Paragraph p, bool advancePage = true)
        {
            var sb = new StringBuilder();
            var startPage = _page;
            var page = _page;

            foreach (var run in p.Descendants<Run>())
            {
                foreach (var node in run.ChildElements)
                {
                    switch (node)
                    {
                        case LastRenderedPageBreak:
                            page++;
                            if (sb.ToString().Trim().Length == 0)
                                startPage = page;
                            break;
                        case Break { Type.Value: var type } when type == BreakValues.Page && !_hasRenderedBreaks:
                            page++;
                            if (sb.ToString().Trim().Length == 0)
                                startPage = page;
                            break;
                        case Text t:
                            sb.Append(t.Text);
                            break;
                        case TabChar:
                            sb.Append(' ');
                            break;
                        case Break:
                        case CarriageReturn:
                            sb.Append(' ');
                            break;
                    }
                }
            }

            if (advancePage)
                _page = page;

            var text = TextNormalizer.Normalize(sb.ToString()).Replace(' ', ' ').Trim();
            return (text, startPage);
        }

        private Style? StyleOf(Paragraph p)
        {
            var id = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            return id is not null && _styles.TryGetValue(id, out var style) ? style : null;
        }

        private IEnumerable<Style> StyleChain(Paragraph p)
        {
            var style = StyleOf(p);
            for (var depth = 0; style is not null && depth < 10; depth++)
            {
                yield return style;
                var parent = style.BasedOn?.Val?.Value;
                style = parent is not null && _styles.TryGetValue(parent, out var next) ? next : null;
            }
        }

        private int? ResolveOutlineLevel(Paragraph p)
        {
            var direct = p.ParagraphProperties?.OutlineLevel?.Val?.Value;
            if (direct is { } d)
                return d < 9 ? d + 1 : null;

            foreach (var style in StyleChain(p))
            {
                if (style.StyleParagraphProperties?.OutlineLevel?.Val?.Value is { } s)
                    return s < 9 ? s + 1 : null;
            }

            // Tài liệu không khai báo outline: dùng tên style dựng sẵn của Word làm phương án cuối.
            var name = StyleOf(p)?.StyleName?.Val?.Value ?? p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            if (name is not null)
            {
                var lower = name.ToLowerInvariant().Replace(" ", "");
                if (lower.StartsWith("heading", StringComparison.Ordinal) && int.TryParse(lower["heading".Length..], out var n))
                    return n;
            }

            return null;
        }

        private string? ResolveNumbering(Paragraph p)
        {
            if (_numbering is null)
                return null;

            var direct = p.ParagraphProperties?.NumberingProperties;
            var numId = direct?.NumberingId?.Val?.Value;
            var ilvl = direct?.NumberingLevelReference?.Val?.Value;

            foreach (var style in StyleChain(p))
            {
                var props = style.StyleParagraphProperties?.NumberingProperties;
                numId ??= props?.NumberingId?.Val?.Value;
                ilvl ??= props?.NumberingLevelReference?.Val?.Value;
            }

            return numId is null or 0 ? null : _counters.Next(_numbering, numId.Value, ilvl ?? 0);
        }

        private bool IsAllBold(Paragraph p)
        {
            var styleBold = StyleChain(p).Any(s => IsOn(s.StyleRunProperties?.Bold));
            var runs = p.Descendants<Run>().Where(r => r.Descendants<Text>().Any(t => !string.IsNullOrWhiteSpace(t.Text))).ToList();
            return runs.Count > 0 && runs.All(r => r.RunProperties?.Bold is { } b ? IsOn(b) : styleBold);
        }

        private static bool IsOn(OnOffType? flag) => flag is not null && (flag.Val is null || flag.Val.Value);
    }

    /// <summary>Bộ đếm danh sách đánh số: cùng một danh sách trừu tượng thì dùng chung bộ đếm, như Word.</summary>
    private sealed class NumberingState
    {
        private readonly Dictionary<int, int[]> _counters = [];

        public string? Next(Numbering numbering, int numId, int ilvl)
        {
            var instance = numbering.Elements<NumberingInstance>().FirstOrDefault(n => n.NumberID?.Value == numId);
            var abstractId = instance?.AbstractNumId?.Val?.Value;
            var abstractNum = numbering.Elements<AbstractNum>().FirstOrDefault(a => a.AbstractNumberId?.Value == abstractId);
            if (abstractNum is null)
                return null;

            ilvl = Math.Clamp(ilvl, 0, 8);
            var levels = abstractNum.Elements<Level>().ToDictionary(l => l.LevelIndex?.Value ?? 0);
            if (!levels.TryGetValue(ilvl, out var level))
                return null;

            var counters = _counters.TryGetValue(abstractId!.Value, out var existing) ? existing : _counters[abstractId.Value] = new int[9];
            counters[ilvl] = counters[ilvl] == 0 ? Start(level) : counters[ilvl] + 1;
            for (var j = ilvl + 1; j < counters.Length; j++)
                counters[j] = 0;

            var format = level.NumberingFormat?.Val?.Value;
            if (format == NumberFormatValues.Bullet)
                return "•";

            var template = level.LevelText?.Val?.Value;
            if (string.IsNullOrEmpty(template))
                return null;

            var sb = new StringBuilder(template);
            for (var k = 1; k <= 9; k++)
            {
                var token = "%" + k;
                if (!template.Contains(token, StringComparison.Ordinal))
                    continue;

                levels.TryGetValue(k - 1, out var source);
                var value = counters[k - 1] == 0 && source is not null ? Start(source) : counters[k - 1];
                sb.Replace(token, Format(value, source?.NumberingFormat?.Val?.Value));
            }

            return sb.ToString();
        }

        private static int Start(Level level) => level.StartNumberingValue?.Val?.Value ?? 1;

        private static string Format(int n, NumberFormatValues? format)
        {
            if (format == NumberFormatValues.UpperRoman) return Roman(n);
            if (format == NumberFormatValues.LowerRoman) return Roman(n).ToLowerInvariant();
            if (format == NumberFormatValues.UpperLetter) return Letters(n);
            if (format == NumberFormatValues.LowerLetter) return Letters(n).ToLowerInvariant();
            if (format == NumberFormatValues.DecimalZero) return n.ToString("00");
            return n.ToString();
        }

        private static string Roman(int n)
        {
            (int Value, string Symbol)[] table =
                [(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")];
            var sb = new StringBuilder();
            foreach (var (value, symbol) in table)
            {
                while (n >= value)
                {
                    sb.Append(symbol);
                    n -= value;
                }
            }

            return sb.ToString();
        }

        // a, b, ..., z, aa, bb, ... (kiểu của Word)
        private static string Letters(int n) =>
            new string((char)('A' + (n - 1) % 26), (n - 1) / 26 + 1);
    }
}
