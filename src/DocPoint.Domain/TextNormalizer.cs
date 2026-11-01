using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace DocPoint.Domain;

/// <summary>
/// Chuẩn hoá chữ để lưu và tìm. Bản không dấu giữ nguyên độ dài của bản dựng sẵn (NFC), nên vị trí
/// khớp tìm được trên bản không dấu dùng lại được trên bản có dấu.
/// </summary>
public static class TextNormalizer
{
    private static readonly ConcurrentDictionary<char, char> BaseLetters = new();

    /// <summary>Dạng dựng sẵn (NFC): mọi chữ có dấu là đúng một ký tự.</summary>
    public static string Normalize(string text) =>
        text.IsNormalized(NormalizationForm.FormC) ? text : text.Normalize(NormalizationForm.FormC);

    /// <summary>Bỏ dấu, đổi đ thành d. Kết quả cùng độ dài với <see cref="Normalize"/> của đầu vào.</summary>
    public static string RemoveDiacritics(string text)
    {
        var composed = Normalize(text);
        return string.Create(composed.Length, composed, static (span, src) =>
        {
            for (var i = 0; i < src.Length; i++)
                span[i] = BaseLetter(src[i]);
        });
    }

    private static char BaseLetter(char c)
    {
        if (c < 0x80)
            return c;

        return BaseLetters.GetOrAdd(c, static ch =>
        {
            if (ch == 'đ') return 'd';
            if (ch == 'Đ') return 'D';

            var decomposed = ch.ToString().Normalize(NormalizationForm.FormD);
            return decomposed.Length > 1 && CharUnicodeInfo.GetUnicodeCategory(decomposed[0]) != UnicodeCategory.NonSpacingMark
                ? decomposed[0]
                : ch;
        });
    }
}
