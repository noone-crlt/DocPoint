using System.Globalization;
using System.Text;

namespace DocPoint.Domain;

public static class Vietnamese
{
    /// <summary>Bỏ dấu tiếng Việt và đổi đ thành d, để gõ "vuot den do" vẫn khớp "vượt đèn đỏ".</summary>
    public static string RemoveDiacritics(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
    }
}
