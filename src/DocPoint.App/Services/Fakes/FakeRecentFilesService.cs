using DocPoint.Application;
using DocPoint.Domain;

namespace DocPoint.App.Services.Fakes;

public sealed class FakeRecentFilesService : IRecentFilesService
{
    public Task<IReadOnlyList<RecentDocument>> GetRecentAsync(CancellationToken ct = default)
    {
        var today = DateTime.Today;
        return Task.FromResult<IReadOnlyList<RecentDocument>>(
        [
            new("Nghị định 168/2024/NĐ-CP", @"D:\Tài liệu\Văn bản pháp luật", "Điểm c, khoản 7, Điều 7", DocumentKind.Legal, today.AddHours(10).AddMinutes(41), false),
            new("Quy chế nhân sự 2026.docx", @"D:\Công ty\Quy chế", "Mục 3.2, Chương 3 · trang 12", DocumentKind.Word, today.AddHours(10).AddMinutes(12), false),
            new("Theo dõi nghỉ phép 2026.xlsx", @"D:\Công ty\Nhân sự", "Sheet “Hướng dẫn” · dòng 4", DocumentKind.Excel, today.AddHours(9).AddMinutes(30), false),
            new("Bộ luật Lao động 45/2019/QH14", @"D:\Tài liệu\Văn bản pháp luật", "Khoản 1, Điều 113", DocumentKind.Legal, today.AddDays(-1).AddHours(16).AddMinutes(5), true),
            new("Công văn 85/HCNS về lịch nghỉ Tết.pdf", @"D:\Công ty\Công văn đến", "Đoạn 3 · trang 1", DocumentKind.Pdf, today.AddDays(-1).AddHours(14).AddMinutes(20), false),
            new("Báo cáo tổng kết 2025.pdf", @"D:\Công ty\Báo cáo", "Trang 31 / 48", DocumentKind.Pdf, today.AddDays(-1).AddHours(8).AddMinutes(50), false),
            new("Nội quy an toàn giao thông.docx", @"D:\Công ty\Quy chế", "Mục 2.4, Chương 2 · trang 9", DocumentKind.Word, today.AddDays(-4).AddHours(15).AddMinutes(12), false),
            new("Luật Trật tự, an toàn giao thông đường bộ", @"D:\Tài liệu\Văn bản pháp luật", "Chương II", DocumentKind.Legal, today.AddDays(-6).AddHours(9).AddMinutes(4), false),
        ]);
    }
}
