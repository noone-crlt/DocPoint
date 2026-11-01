using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace SampleGenerator;

/// <summary>Các file DOCX mẫu. Tiêu đề dùng style tiếng Việt có outline level, không đặt tên "Heading".</summary>
internal static class DocxSamples
{
    public static void WriteAll(string dir)
    {
        Report(Path.Combine(dir, "bao-cao-quy-3.docx"));
        Textbook(Path.Combine(dir, "giao-trinh-co-so-du-lieu.docx"));
        Letter(Path.Combine(dir, "cong-van-so-125.docx"));
        Contract(Path.Combine(dir, "hop-dong-thue-van-phong.docx"));
        Regulations(Path.Combine(dir, "quy-che-lam-viec.docx"));
    }

    private static void Report(string path) => Build(path, withNumbering: false, body =>
    {
        body.Append(P("BÁO CÁO KẾT QUẢ KINH DOANH QUÝ 3 NĂM 2026", bold: true));
        body.Append(P("Chương 1. Tổng quan", style: "TieuDe1"));
        body.Append(P("1.1 Tình hình chung", style: "TieuDe2"));
        body.Append(P("Trong quý 3, doanh thu toàn công ty đạt 48,6 tỷ đồng, tăng 12% so với cùng kỳ năm trước. Lợi nhuận sau thuế đạt 6,2 tỷ đồng."));
        body.Append(P("1.2 Nhân sự", style: "TieuDe2"));
        body.Append(P("Tổng số cán bộ nhân viên đến hết tháng 9 là 214 người, tuyển mới 18 người, nghỉ việc 7 người."));
        body.Append(P("Chương 2. Kết quả theo mảng kinh doanh", style: "TieuDe1"));
        body.Append(P("2.1 Mảng bán lẻ", style: "TieuDe2"));
        body.Append(P("Doanh thu bán lẻ đạt 21,3 tỷ đồng nhờ chương trình khuyến mãi mùa tựu trường."));
        body.Append(P("2.2 Mảng doanh nghiệp", style: "TieuDe2"));
        body.Append(P("Ký mới 9 hợp đồng cung cấp thiết bị văn phòng, giá trị lớn nhất là hợp đồng với Sở Giáo dục."));
        // Word ghi dấu ngắt trang lúc lưu; từ đây là trang 2.
        body.Append(P("2.3 Mảng dịch vụ bảo trì", style: "TieuDe2", pageBreakMark: true));
        body.Append(P("Tỷ lệ gia hạn hợp đồng bảo trì đạt 91%, khách hàng phản hồi tốt về thời gian xử lý sự cố."));
        body.Append(P("Chương 3. Kế hoạch quý 4", style: "TieuDe1"));
        body.Append(P("3.1 Mục tiêu doanh thu", style: "TieuDe2"));
        body.Append(P("Mục tiêu doanh thu quý 4 là 55 tỷ đồng, tập trung vào mùa mua sắm cuối năm."));
        body.Append(P("3.2 Rủi ro cần theo dõi", style: "TieuDe2"));
        body.Append(P("Giá linh kiện nhập khẩu biến động theo tỷ giá, có thể ảnh hưởng đến biên lợi nhuận."));
    });

    private static void Textbook(string path) => Build(path, withNumbering: false, body =>
    {
        body.Append(P("Giáo trình Cơ sở dữ liệu", style: "TieuDe1"));
        body.Append(P("Chương 1. Khái niệm cơ bản", style: "TieuDe1"));
        body.Append(P("1.1 Cơ sở dữ liệu là gì", style: "TieuDe2"));
        body.Append(P("1.1.1 Dữ liệu và thông tin", style: "TieuDe3"));
        body.Append(P("Dữ liệu là các sự kiện thô chưa qua xử lý; thông tin là dữ liệu đã được đặt vào ngữ cảnh để có ý nghĩa."));
        body.Append(P("1.1.2 Hệ quản trị cơ sở dữ liệu", style: "TieuDe3"));
        body.Append(P("Hệ quản trị cơ sở dữ liệu là phần mềm cho phép tạo, truy vấn và quản lý dữ liệu, ví dụ SQL Server hoặc SQLite."));
        body.Append(P("Chương 2. Mô hình quan hệ", style: "TieuDe1"));
        body.Append(P("2.1 Khoá chính và khoá ngoại", style: "TieuDe2"));
        body.Append(P("Khoá chính là thuộc tính hoặc tập thuộc tính xác định duy nhất một bản ghi trong bảng. Khoá ngoại tham chiếu tới khoá chính của bảng khác để duy trì toàn vẹn tham chiếu."));
        body.Append(P("2.2 Chuẩn hoá", style: "TieuDe2"));
        body.Append(P("Chuẩn hoá là quá trình tổ chức lại bảng để giảm dư thừa dữ liệu. Dạng chuẩn ba (3NF) yêu cầu mọi thuộc tính không khoá phụ thuộc trực tiếp vào khoá chính."));
    });

    // Không có tiêu đề, không đánh số: kiểm tra nhánh "tách theo đoạn".
    private static void Letter(string path) => Build(path, withNumbering: false, body =>
    {
        body.Append(P("CÔNG TY CỔ PHẦN THIẾT BỊ VĂN PHÒNG ABC", bold: true));
        body.Append(P("Số: 125/CV-ABC"));
        body.Append(P("V/v: Đề nghị thanh toán đợt 2 hợp đồng cung cấp thiết bị"));
        body.Append(P("Kính gửi: Sở Giáo dục và Đào tạo"));
        body.Append(P("Căn cứ hợp đồng số 18/2026/HĐ-ABC ký ngày 05 tháng 6 năm 2026, Công ty chúng tôi đã bàn giao đầy đủ 120 bộ máy tính để bàn tại 12 trường học."));
        body.Append(P("Đề nghị Quý Sở xem xét thanh toán đợt 2 với số tiền 1.250.000.000 đồng trong thời hạn 15 ngày kể từ ngày nhận công văn này."));
        body.Append(P("Trân trọng cảm ơn."));
        body.Append(P("Hà Nội, ngày 14 tháng 9 năm 2026"));
    });

    // Số hiệu gõ thẳng vào chữ: "Điều 1.", "1.1", "a)".
    private static void Contract(string path) => Build(path, withNumbering: false, body =>
    {
        body.Append(P("HỢP ĐỒNG THUÊ VĂN PHÒNG", bold: true));
        body.Append(P("Điều 1. Đối tượng hợp đồng", bold: true));
        body.Append(P("1.1 Bên A cho Bên B thuê văn phòng diện tích 85 m2 tại tầng 7, toà nhà Sao Mai."));
        body.Append(P("1.2 Văn phòng được bàn giao kèm hệ thống điều hoà và thang máy dùng chung."));
        body.Append(P("Điều 2. Giá thuê và thanh toán", bold: true));
        body.Append(P("2.1 Giá thuê là 28.000.000 đồng mỗi tháng, chưa bao gồm thuế giá trị gia tăng."));
        body.Append(P("2.2 Bên B thanh toán trước ngày 05 hằng tháng bằng chuyển khoản."));
        body.Append(P("Điều 3. Chấm dứt hợp đồng", bold: true));
        body.Append(P("3.1 Mỗi bên có quyền chấm dứt hợp đồng khi bên kia vi phạm nghĩa vụ và không khắc phục sau 30 ngày kể từ ngày nhận thông báo."));
        body.Append(P("3.2 Bên B phải báo trước 60 ngày nếu muốn chấm dứt hợp đồng trước hạn, nếu không sẽ mất khoản tiền đặt cọc."));
    });

    // Điều, khoản, điểm đánh số tự động của Word: chữ "Điều 1." không nằm trong đoạn, chỉ có trong numbering.xml.
    private static void Regulations(string path) => Build(path, withNumbering: true, body =>
    {
        body.Append(P("QUY CHẾ LÀM VIỆC", bold: true));
        body.Append(P("Chương I. Quy định chung", style: "TieuDe1"));
        body.Append(P("Phạm vi áp dụng", level: 0, bold: true));
        body.Append(P("Quy chế này áp dụng cho toàn bộ cán bộ, nhân viên ký hợp đồng lao động với công ty.", level: 1));
        body.Append(P("Giờ làm việc", level: 0, bold: true));
        body.Append(P("Giờ làm việc từ 8 giờ đến 17 giờ, nghỉ trưa từ 12 giờ đến 13 giờ, từ thứ Hai đến thứ Sáu.", level: 1));
        body.Append(P("Nhân viên đi làm muộn quá 15 phút phải báo với quản lý trực tiếp.", level: 1));
        body.Append(P("Chương II. Nghỉ phép", style: "TieuDe1"));
        body.Append(P("Nghỉ hằng năm", level: 0, bold: true));
        body.Append(P("Nhân viên có đủ 12 tháng làm việc được nghỉ hằng năm 12 ngày làm việc hưởng nguyên lương.", level: 1));
        body.Append(P("Nhân viên làm việc chưa đủ 12 tháng được tính số ngày nghỉ theo tỷ lệ thời gian làm việc.", level: 1));
        body.Append(P("Nghỉ không hưởng lương", level: 0, bold: true));
        body.Append(P("Nhân viên được nghỉ không hưởng lương trong các trường hợp sau:", level: 1));
        body.Append(P("Có việc riêng cần giải quyết, báo trước ít nhất 3 ngày làm việc;", level: 2));
        body.Append(P("Nghỉ chăm sóc con ốm có xác nhận của cơ sở y tế.", level: 2));
        body.Append(P("Thời gian nghỉ không hưởng lương tối đa là 30 ngày trong một năm.", level: 1));
    });

    private static void Build(string path, bool withNumbering, Action<Body> fill)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        var body = new Body();
        main.Document = new Document(body);

        var styles = main.AddNewPart<StyleDefinitionsPart>();
        styles.Styles = new Styles(
            new Style(new StyleName { Val = "Normal" }) { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true },
            Heading("TieuDe1", "Tiêu đề 1", 0, 32),
            Heading("TieuDe2", "Tiêu đề 2", 1, 28),
            Heading("TieuDe3", "Tiêu đề 3", 2, 26));

        if (withNumbering)
        {
            var numbering = main.AddNewPart<NumberingDefinitionsPart>();
            numbering.Numbering = new Numbering(
                new AbstractNum(
                    new MultiLevelType { Val = MultiLevelValues.Multilevel },
                    Level(0, NumberFormatValues.Decimal, "Điều %1."),
                    Level(1, NumberFormatValues.Decimal, "%2."),
                    Level(2, NumberFormatValues.LowerLetter, "%3)"))
                { AbstractNumberId = 1 },
                new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = 1 });
        }

        fill(body);
        body.Append(new SectionProperties(new PageSize { Width = 11906, Height = 16838 }));
        main.Document.Save();
    }

    private static Style Heading(string id, string name, int outline, int halfPoints) =>
        new(new StyleName { Val = name },
            new BasedOn { Val = "Normal" },
            new NextParagraphStyle { Val = "Normal" },
            new StyleParagraphProperties(new KeepNext(), new OutlineLevel { Val = outline }),
            new StyleRunProperties(new Bold(), new FontSize { Val = halfPoints.ToString() }))
        { Type = StyleValues.Paragraph, StyleId = id };

    private static Level Level(int index, NumberFormatValues format, string text) =>
        new(new StartNumberingValue { Val = 1 },
            new NumberingFormat { Val = format },
            new LevelText { Val = text },
            new LevelJustification { Val = LevelJustificationValues.Left })
        { LevelIndex = index };

    private static Paragraph P(string text, string? style = null, bool bold = false, int? level = null, bool pageBreakMark = false)
    {
        var props = new ParagraphProperties();
        if (style is not null)
            props.Append(new ParagraphStyleId { Val = style });
        if (level is { } l)
            props.Append(new NumberingProperties(new NumberingLevelReference { Val = l }, new NumberingId { Val = 1 }));

        var p = new Paragraph(props);
        if (pageBreakMark)
            p.Append(new Run(new LastRenderedPageBreak()));

        var run = new Run();
        if (bold)
            run.Append(new RunProperties(new Bold()));
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        p.Append(run);
        return p;
    }
}
