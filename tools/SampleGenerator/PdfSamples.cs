using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace SampleGenerator;

internal static class PdfSamples
{
    private const string Regular = @"C:\Windows\Fonts\arial.ttf";
    private const string Bold = @"C:\Windows\Fonts\arialbd.ttf";

    public static void WriteAll(string dir)
    {
        ExportedFromWord(Path.Combine(dir, "bao-cao-an-toan-lao-dong.pdf"));
        Unstructured(Path.Combine(dir, "thong-bao-di-doi-kho.pdf"));
        Scanned(Path.Combine(dir, "tai-lieu-scan.pdf"));
    }

    // Có tiêu đề cỡ lớn và đậm, đầu trang lặp lại ở mọi trang, chân trang có số trang.
    private static void ExportedFromWord(string path)
    {
        var builder = new PdfDocumentBuilder();
        var regular = builder.AddTrueTypeFont(File.ReadAllBytes(Regular));
        var bold = builder.AddTrueTypeFont(File.ReadAllBytes(Bold));

        var pages = new (string Heading, string? Sub, string[] Body)[]
        {
            ("1. Mục đích", null,
            [
                "Báo cáo này tổng hợp kết quả kiểm tra an toàn lao động tại các xưởng",
                "sản xuất trong năm 2026.",
            ]),
            ("2. Kết quả kiểm tra", "2.1 Phòng cháy chữa cháy",
            [
                "Đã kiểm tra 8 xưởng, phát hiện 14 vị trí chưa có biển cảnh báo và 3 bình",
                "chữa cháy hết hạn kiểm định.",
                "---",
                "Các bình chữa cháy hết hạn đã được thay thế trong vòng 7 ngày kể từ ngày",
                "phát hiện.",
            ]),
            ("3. Kiến nghị", null,
            [
                "Đề nghị tổ chức huấn luyện an toàn lao động định kỳ mỗi 6 tháng cho toàn",
                "bộ công nhân.",
            ]),
        };

        for (var i = 0; i < pages.Length; i++)
        {
            var page = builder.AddPage(PageSize.A4);
            page.AddText("Công ty ABC - Báo cáo an toàn lao động 2026", 9, new PdfPoint(60, 800), regular);

            var y = 740.0;
            page.AddText(pages[i].Heading, 16, new PdfPoint(60, y), bold);
            y -= 34;

            foreach (var line in pages[i].Body)
            {
                if (line == "---")
                {
                    // Tiêu đề phụ chen giữa trang 2.
                    y -= 10;
                    page.AddText(pages[i].Sub!, 13, new PdfPoint(60, y), bold);
                    y -= 28;
                    continue;
                }

                page.AddText(line, 12, new PdfPoint(60, y), regular);
                y -= 20;
            }

            page.AddText($"Trang {i + 1}", 9, new PdfPoint(280, 40), regular);
        }

        File.WriteAllBytes(path, builder.Build());
    }

    // Một cỡ chữ duy nhất, không tiêu đề: đoạn chỉ phân biệt bằng khoảng cách dòng.
    private static void Unstructured(string path)
    {
        var builder = new PdfDocumentBuilder();
        var regular = builder.AddTrueTypeFont(File.ReadAllBytes(Regular));

        string[][][] pages =
        [
            [
                ["Thông báo về việc di dời kho hàng sang cơ sở mới"],
                [
                    "Kính gửi toàn thể khách hàng và đối tác, Công ty xin thông báo kho hàng tại số 12",
                    "đường Láng sẽ được di dời sang khu công nghiệp Quang Minh từ ngày 01 tháng 11",
                    "năm 2026.",
                ],
                [
                    "Trong thời gian di dời, việc giao nhận hàng tại kho cũ sẽ tạm dừng từ ngày 25 đến",
                    "ngày 31 tháng 10. Các đơn hàng phát sinh trong thời gian này sẽ được xử lý ngay",
                    "sau khi kho mới đi vào hoạt động.",
                ],
            ],
            [
                ["Địa chỉ kho mới là lô C5, khu công nghiệp Quang Minh, huyện Mê Linh, thành phố Hà Nội."],
                ["Mọi thắc mắc xin liên hệ bộ phận chăm sóc khách hàng theo số điện thoại 0243 555 0199."],
            ],
        ];

        foreach (var paragraphs in pages)
        {
            var page = builder.AddPage(PageSize.A4);
            var y = 780.0;
            foreach (var lines in paragraphs)
            {
                foreach (var line in lines)
                {
                    page.AddText(line, 12, new PdfPoint(60, y), regular);
                    y -= 18;
                }

                y -= 18;
            }
        }

        File.WriteAllBytes(path, builder.Build());
    }

    // Chỉ có ảnh, không có lớp chữ: giống file scan.
    private static void Scanned(string path)
    {
        const int width = 1240, height = 1754;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            string[] lines =
            [
                "BIÊN BẢN BÀN GIAO TÀI SẢN",
                "Hôm nay, ngày 20 tháng 9 năm 2026, hai bên đã bàn giao 5 máy in",
                "và 12 màn hình cho phòng hành chính.",
            ];
            for (var i = 0; i < lines.Length; i++)
            {
                var text = new FormattedText(lines[i], CultureInfo.GetCultureInfo("vi-VN"), FlowDirection.LeftToRight,
                    new Typeface("Arial"), 32, Brushes.Black, 1.0);
                dc.DrawText(text, new Point(100, 160 + i * 60));
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var png = new MemoryStream();
        encoder.Save(png);

        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        page.AddPng(png.ToArray(), new PdfRectangle(0, 0, 595, 842));
        File.WriteAllBytes(path, builder.Build());
    }
}
