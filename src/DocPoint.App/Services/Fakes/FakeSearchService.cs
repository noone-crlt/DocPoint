using DocPoint.Application;
using DocPoint.Domain;

namespace DocPoint.App.Services.Fakes;

public sealed class FakeSearchService : ISearchService
{
    private const string S = "\u0002";
    private const string E = "\u0003";

    private static readonly IReadOnlyList<SearchHit> Hits =
    [
        new("Điểm c, khoản 7, Điều 7",
            "NĐ 168/2024/NĐ-CP › Chương II › Điều 7 › Khoản 7",
            "Nghị định 168/2024/NĐ-CP", "Chính phủ · hiệu lực từ 01/01/2025",
            $"{S}Không chấp hành hiệu lệnh của đèn tín hiệu giao thông{E}",
            [
                "Điều 7. Xử phạt, trừ điểm giấy phép lái xe của người điều khiển xe mô tô, xe gắn máy, các loại xe tương tự xe mô tô và các loại xe tương tự xe gắn máy vi phạm quy tắc giao thông đường bộ",
                "7. Phạt tiền từ 4.000.000 đồng đến 6.000.000 đồng đối với người điều khiển xe thực hiện một trong các hành vi vi phạm sau đây:",
                $"c) {S}Không chấp hành hiệu lệnh của đèn tín hiệu giao thông;{E}",
            ],
            Validity.InForce, MapIndex: 14),
        new("Điểm b, khoản 10, Điều 7",
            "NĐ 168/2024/NĐ-CP › Chương II › Điều 7 › Khoản 10",
            "Nghị định 168/2024/NĐ-CP", "Chính phủ · hiệu lực từ 01/01/2025",
            $"{S}Vi phạm quy định tại điểm c khoản 7 Điều này mà gây tai nạn giao thông{E}",
            [
                "10. Phạt tiền từ 10.000.000 đồng đến 14.000.000 đồng đối với người điều khiển xe thực hiện một trong các hành vi vi phạm sau đây:",
                $"b) {S}Vi phạm quy định tại điểm c khoản 7 Điều này mà gây tai nạn giao thông;{E}",
            ],
            Validity.InForce, MapIndex: 21),
        new("Khoản 13, Điều 7",
            "NĐ 168/2024/NĐ-CP › Chương II › Điều 7 › Khoản 13",
            "Nghị định 168/2024/NĐ-CP", "Chính phủ · hiệu lực từ 01/01/2025",
            $"Thực hiện hành vi quy định tại điểm c khoản 7 Điều này bị {S}trừ điểm giấy phép lái xe 04 điểm{E}",
            [
                $"13. Thực hiện hành vi quy định tại điểm c khoản 7 Điều này bị {S}trừ điểm giấy phép lái xe 04 điểm{E};",
            ],
            Validity.InForce, MapIndex: 27),
        new("Mục 2.4, trang 9",
            "Nội quy an toàn giao thông › Chương 2 › Mục 2.4",
            "Nội quy an toàn giao thông.docx", @"D:\Công ty\Quy chế",
            $"Xe máy công vụ phải chấp hành {S}đèn tín hiệu{E} như mọi phương tiện khác",
            [
                "2.4. Xe máy công vụ",
                $"Xe máy công vụ phải chấp hành {S}đèn tín hiệu{E} như mọi phương tiện khác; không được vượt khi đèn đỏ kể cả khi đang đi công tác.",
            ],
            Validity.NotApplicable, MapIndex: 18),
        new("Sheet “Tháng 10”, dòng 18",
            "Theo dõi vi phạm 2026 › Tháng 10 › dòng 18",
            "Theo dõi vi phạm 2026.xlsx", @"D:\Công ty\Báo cáo",
            $"Lỗi: {S}Vượt đèn đỏ{E} · Xe máy · Kho vận",
            [
                $"Lỗi: {S}Vượt đèn đỏ{E}",
                "Phương tiện: Xe máy",
                "Bộ phận: Kho vận",
            ],
            Validity.NotApplicable, MapIndex: 9),
        new("Điểm a, khoản 5, Điều 6",
            "NĐ 100/2019/NĐ-CP › Chương II › Điều 6 › Khoản 5",
            "Nghị định 100/2019/NĐ-CP", "Chính phủ · hiệu lực từ 01/01/2020",
            $"{S}Không chấp hành hiệu lệnh của đèn tín hiệu giao thông{E}",
            [
                "5. Phạt tiền từ 600.000 đồng đến 800.000 đồng đối với người điều khiển xe thực hiện một trong các hành vi vi phạm sau đây:",
                $"a) {S}Không chấp hành hiệu lệnh của đèn tín hiệu giao thông{E};",
            ],
            Validity.Expired, "Thay bằng NĐ 168/2024", MapIndex: 12),
    ];

    public Task<IReadOnlyList<string>> GetSuggestionsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(
        [
            "vượt đèn đỏ xe máy",
            "nồng độ cồn xe máy",
            "quy trình xin nghỉ phép",
            "Nghị định 168/2024/NĐ-CP",
            "Quy chế nhân sự 2026",
            "Theo dõi nghỉ phép 2026",
        ]);

    public async Task<IReadOnlyList<string>> SuggestAsync(string prefix, CancellationToken ct = default)
    {
        var all = await GetSuggestionsAsync(ct);
        var plain = TextNormalizer.RemoveDiacritics(prefix).ToLowerInvariant();
        return all.Where(s => TextNormalizer.RemoveDiacritics(s).ToLowerInvariant().Contains(plain)).ToList();
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken ct = default)
    {
        await Task.Delay(150, ct);

        var words = TextNormalizer.RemoveDiacritics(query).ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3)
            .ToArray();

        return words.Length == 0
            ? []
            : Hits.Where(h =>
              {
                  var text = TextNormalizer.RemoveDiacritics(string.Concat(h.Body)).ToLowerInvariant();
                  return words.Any(text.Contains);
              }).ToList();
    }
}
