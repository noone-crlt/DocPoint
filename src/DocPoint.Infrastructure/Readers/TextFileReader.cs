using System.Text;
using DocPoint.Application;
using DocPoint.Domain;

namespace DocPoint.Infrastructure.Readers;

/// <summary>Đọc .txt và .md. Dò bảng mã: BOM trước, rồi UTF-8, cuối cùng Windows-1258.</summary>
public sealed class TextFileReader : IDocumentParser
{
    public bool CanParse(string extension) => extension is ".txt" or ".md";

    public async Task<IReadOnlyList<RawBlock>> ReadAsync(string path, CancellationToken ct = default)
    {
        try
        {
            byte[] bytes;
            await using (var stream = ReaderSupport.OpenRead(path))
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            var text = TextNormalizer.Normalize(Decode(bytes));
            return Split(text, isMarkdown: Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw ReaderSupport.Translate(ex, path);
        }
    }

    internal static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1258).GetString(bytes);
        }
    }

    private static List<RawBlock> Split(string text, bool isMarkdown)
    {
        var blocks = new List<RawBlock>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0)
                continue;

            if (isMarkdown && line[0] == '#')
            {
                var level = line.TakeWhile(c => c == '#').Count();
                var title = line[level..].Trim();
                if (level <= 6 && title.Length > 0)
                {
                    blocks.Add(new RawBlock(title, BlockKind.Heading, HeadingLevel: level));
                    continue;
                }
            }

            blocks.Add(new RawBlock(line));
        }

        return blocks;
    }
}
