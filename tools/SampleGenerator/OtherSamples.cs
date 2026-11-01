using System.Text;
using ClosedXML.Excel;

namespace SampleGenerator;

internal static class OtherSamples
{
    public static void WriteAll(string dir)
    {
        Notes(dir);
        Scores(Path.Combine(dir, "bang-diem.xlsx"));
    }

    private static void Notes(string dir)
    {
        // UTF-8 có BOM.
        File.WriteAllText(Path.Combine(dir, "ghi-chu-hop.txt"),
            string.Join("\r\n",
                "Biên bản họp phòng kinh doanh ngày 02/10/2026",
                "",
                "1. Báo cáo doanh số tháng 9",
                "- Chị Lan báo cáo doanh số bán lẻ tăng nhờ khuyến mãi.",
                "",
                "2. Kế hoạch tuyển dụng",
                "- Cần tuyển thêm 3 nhân viên tư vấn trước tháng 11.",
                "",
                "3. Việc cần làm",
                "- Anh Minh gửi báo giá cho trường Nguyễn Du trước thứ Sáu.",
                ""),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        // Windows-1258, không BOM: kiểu file cũ từ máy dùng bộ gõ cũ.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = string.Join("\r\n",
            "Nhắc việc cuối tuần",
            "- Gọi lại cho khách hàng về lịch bảo trì máy in.",
            "- Đặt phòng họp cho buổi đào tạo an toàn lao động.",
            "");
        File.WriteAllBytes(Path.Combine(dir, "ghi-chu-cu.txt"), EncodeWindows1258(text));
    }

    // Windows-1258 chỉ có một số chữ dựng sẵn; phần còn lại là chữ cái gốc cộng dấu thanh rời.
    private static byte[] EncodeWindows1258(string text)
    {
        var enc = Encoding.GetEncoding(1258, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var sb = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormC))
        {
            if (CanEncode(enc, c.ToString()))
            {
                sb.Append(c);
                continue;
            }

            var d = c.ToString().Normalize(NormalizationForm.FormD);
            var baseChar = d[0].ToString();
            // Dấu rời xếp theo thứ tự chuẩn Unicode (dấu chấm dưới trước dấu mũ), nên thử ghép từng dấu một.
            var marks = d[1..].ToList();
            for (var found = true; found;)
            {
                found = false;
                foreach (var m in marks)
                {
                    var composed = (baseChar + m).Normalize(NormalizationForm.FormC);
                    if (composed.Length == 1 && CanEncode(enc, composed))
                    {
                        baseChar = composed;
                        marks.Remove(m);
                        found = true;
                        break;
                    }
                }
            }

            sb.Append(baseChar).Append(string.Concat(marks));
        }

        return enc.GetBytes(sb.ToString());
    }

    private static bool CanEncode(Encoding enc, string s)
    {
        try
        {
            enc.GetBytes(s);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static void Scores(string path)
    {
        using var wb = new XLWorkbook();

        var scores = wb.AddWorksheet("Điểm thi");
        string[] headers = ["Họ tên", "Lớp", "Toán", "Văn", "Điểm TB"];
        for (var c = 0; c < headers.Length; c++)
            scores.Cell(1, c + 1).Value = headers[c];

        string[] surnames = ["Trần", "Lê", "Phạm", "Hoàng", "Vũ", "Đặng", "Bùi", "Đỗ", "Ngô", "Dương"];
        string[] middles = ["Văn", "Thị", "Minh", "Hữu", "Ngọc", "Quốc", "Thanh", "Đức", "Gia", "Anh"];
        string[] givens = ["Bình", "Cường", "Dũng", "Hà", "Hùng", "Lan", "Linh", "Nam", "Phúc", "Quân", "Trang", "Tuấn"];
        string[] classes = ["12A1", "12A2", "11A1", "11A2"];

        // Dòng 2 đến 46: 45 học sinh, "Nguyễn Văn A" nằm ở dòng 42.
        for (var row = 2; row <= 46; row++)
        {
            var n = row - 2;
            var name = row == 42
                ? "Nguyễn Văn A"
                : $"{surnames[n % surnames.Length]} {middles[n / 4 % middles.Length]} {givens[(n * 7 + 3) % givens.Length]}";
            scores.Cell(row, 1).Value = name;
            scores.Cell(row, 2).Value = row == 42 ? "12A1" : classes[n % classes.Length];
            scores.Cell(row, 3).Value = row == 42 ? 8 : 5 + n % 5;
            scores.Cell(row, 4).Value = row == 42 ? 9 : 6 + n % 4;
            scores.Cell(row, 5).FormulaA1 = $"AVERAGE(C{row}:D{row})";
        }

        var classesSheet = wb.AddWorksheet("Danh sách lớp");
        string[] classHeaders = ["Lớp", "Giáo viên chủ nhiệm", "Sĩ số"];
        for (var c = 0; c < classHeaders.Length; c++)
            classesSheet.Cell(1, c + 1).Value = classHeaders[c];
        (string, string, int)[] rows =
        [
            ("12A1", "Cô Lê Thu Hương", 45),
            ("12A2", "Cô Phạm Thu Hà", 44),
            ("11A1", "Thầy Đỗ Quang Vinh", 43),
        ];
        for (var i = 0; i < rows.Length; i++)
        {
            classesSheet.Cell(i + 2, 1).Value = rows[i].Item1;
            classesSheet.Cell(i + 2, 2).Value = rows[i].Item2;
            classesSheet.Cell(i + 2, 3).Value = rows[i].Item3;
        }

        wb.SaveAs(path);
    }
}
