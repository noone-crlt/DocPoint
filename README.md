# DocPoint

**DocPoint** là phần mềm Windows chạy offline giúp tìm nội dung bên trong tài liệu trên máy bạn. Chỉ cần chọn thư mục, DocPoint tự đọc mọi file bên trong (kể cả thư mục con) và theo dõi khi file thay đổi. Điểm khác biệt: DocPoint không chỉ tìm ra *file nào* chứa nội dung bạn cần, mà còn chỉ ra *nằm ở đâu* trong file đó.

- Văn bản pháp luật → **Điểm a, khoản 2, Điều 6 — Nghị định .../NĐ-CP**
- Giáo trình, báo cáo → **Mục 2.3, Chương 2, trang 15 — GiaoTrinh.docx**
- Bảng tính → **Sheet "Điểm thi", dòng 42 — Bangdiem.xlsx**

> 🚧 Dự án đang trong giai đoạn phát triển.

---

## Tính năng

| Tính năng | Mô tả |
| --- | --- |
| Quét thư mục | Chọn một hoặc nhiều thư mục; app tự đọc mọi file PDF, DOCX, Excel, TXT bên trong, kể cả thư mục con. Không chép file, chỉ lưu đường dẫn |
| Theo dõi thay đổi | File mới, sửa, xoá, đổi tên được cập nhật tự động; mỗi lần mở app quét đối chiếu lại |
| Tìm tên và nội dung | Tìm được theo cả tên file lẫn nội dung; mở file, mở thư mục chứa file, nhảy tới trang PDF |
| Tách cấu trúc | Cắt tài liệu thành phần nhỏ theo cấu trúc riêng (Điều/Khoản/Điểm, Chương/Mục, trang, dòng Excel) |
| Full-text search | Tìm trong toàn bộ nội dung, gõ có dấu hay không dấu đều được, xếp hạng theo độ liên quan |
| Tìm theo nghĩa | Gõ bằng lời thường vẫn ra đúng chỗ dù khác chữ (ví dụ "vượt đèn đỏ" → "không chấp hành hiệu lệnh của đèn tín hiệu") |
| Hiển thị vị trí | Kết quả ghi rõ vị trí và tên tài liệu, tô sáng từ khóa |
| Lọc và xem | Lọc theo loại, cơ quan, năm; xem PDF gốc ngay trong app |
| Văn bản pháp luật | Tình trạng hiệu lực, văn bản hướng dẫn, sửa đổi, thay thế |
| Đánh dấu, ghi chú | Ghim phần hay dùng, ghi chú riêng |
| Sao lưu, khôi phục | Lưu chỉ mục, ghi chú, đánh dấu và cài đặt ra một file; file gốc vẫn nằm nguyên trong thư mục của bạn |
| 100% offline | Không cloud, không tài khoản, dữ liệu nằm trên máy bạn |

### Định dạng hỗ trợ

| Loại tài liệu | Cách tách | Ví dụ nhãn vị trí |
| --- | --- | --- |
| Văn bản pháp luật | Chương, Mục, Điều, Khoản, Điểm | Điểm a khoản 2 Điều 6 |
| Quy chế, quy định nội bộ | Theo Điều hoặc đề mục | Điều 5, Quy chế nhân sự |
| Công văn, tờ trình | Số hiệu, trích yếu, đoạn đánh số | Đoạn 3, Công văn 123/... |
| Giáo trình, báo cáo (DOCX) | Theo Heading hoặc số mục 1., 1.1, 1.1.1 | Mục 2.3, Chương 2 |
| PDF không rõ cấu trúc | Theo trang và đoạn | Trang 15, đoạn 2 |
| Excel (XLSX, XLS, CSV) | Theo sheet và dòng | Sheet "Điểm thi", dòng 42 |
| Text (TXT, MD) | Theo đoạn; tự dò bảng mã (UTF-8, UTF-16, Windows-1258) | Đoạn 4 |

**Chưa hỗ trợ:** PDF scan (cần OCR), PowerPoint, file Office định dạng cũ (.doc, .ppt), chữ nằm trong ảnh. Dự kiến làm sau.

---

## Cách hoạt động

```
Câu tìm ──► Chuẩn hoá (bỏ dấu, từ đồng nghĩa) ──┬──► Full-text (SQLite FTS5, BM25) ──┐
                                                 │                                   ├──► Gộp (RRF) ──► Vị trí + đoạn trích
                                                 └──► Theo nghĩa (vector embedding) ─┘
```

1. **Khi quét thư mục**, mỗi file được đọc nền và cắt thành các phần nhỏ (Điều, Khoản, Mục, trang, dòng Excel). Mỗi phần nhớ cha của nó và có sẵn nhãn vị trí. `FileSystemWatcher` giữ chỉ mục khớp với file thật; full-text search sẵn sàng trước, vector cho tìm theo nghĩa tạo sau khi máy rảnh.
2. **Khi tìm**, câu tìm chạy song song qua hai đường: tìm theo chữ (FTS5) và tìm theo nghĩa (embedding chạy offline bằng ONNX Runtime).
3. **Hai danh sách được gộp** bằng Reciprocal Rank Fusion; phần nào xếp cao ở cả hai thì lên đầu.

---

## Hiệu năng

> Số liệu dưới đây là **ước tính** cho laptop 4–8 nhân, ổ SSD. Con số thật sẽ được đo trên bộ tài liệu mẫu ở giai đoạn 2 và cập nhật lại.

### Tốc độ từng khâu

| Khâu | Ước tính | Ghi chú |
| --- | --- | --- |
| Liệt kê file trong thư mục | Hàng chục nghìn file/giây | Ổ HDD chậm hơn nhiều |
| Đọc TXT, MD | Gần như tức thì | |
| Đọc DOCX | Vài chục file/giây mỗi nhân | File nhiều ảnh chậm hơn |
| Đọc PDF | Vài chục trang/giây mỗi nhân | Khâu chậm nhất của phần đọc chữ |
| Đọc Excel | ~1 giây / 10.000 dòng | ClosedXML tốn RAM với file lớn |
| Ghi vào FTS5 | Hàng chục nghìn đoạn/giây | Ghi theo lô trong một transaction |
| Tạo vector (tìm theo nghĩa) | ~20–100 đoạn/giây trên CPU | **Nút thắt chính**, phụ thuộc kích thước mô hình |
| Tìm kiếm | < 0,2 giây cho vài trăm nghìn đoạn | FTS5 + so sánh vector bằng SIMD |

### Ví dụ: thư mục 5.000 file

*(2.000 PDF trung bình 20 trang, 2.500 DOCX, 500 Excel)*

| Mốc | Thời gian ước tính |
| --- | --- |
| Tìm theo chữ dùng được | ~5–10 phút sau lần quét đầu |
| Tìm theo nghĩa hoàn tất | ~30 phút – 1,5 giờ, chạy nền, không chặn tìm theo chữ |
| Cập nhật một file vừa sửa | ~1 giây (theo chữ), vài giây (theo nghĩa) |

### Chỉ tiêu cần đạt

- [ ] 1.000 trang PDF đọc và vào full-text search dưới 1 phút
- [ ] Tìm kiếm trả kết quả dưới 200 ms
- [ ] Quét lại thư mục 5.000 file không đổi dưới 10 giây
- [ ] App vẫn mượt khi đang lập chỉ mục nền

### Cách tối ưu

- **Đọc song song:** `Channel<T>` với số luồng đọc bằng số nhân CPU; **một luồng ghi** duy nhất vào SQLite, bật WAL, ghi theo lô.
- **Bỏ qua file không đổi:** so ngày sửa và kích thước trước khi đọc; dùng hash khi cần chắc chắn.
- **Hai pha:** full-text search làm trước để tìm được ngay; vector tạo sau khi máy rảnh.
- **Mô hình nhỏ, lượng tử hoá INT8:** nhanh hơn khoảng 2–4 lần, độ chính xác giảm ít.
- **Ưu tiên thông minh:** thư mục vừa mở hoặc vừa tìm được xử lý trước; tự giảm tốc khi người dùng đang làm việc.
- **GPU (tuỳ chọn):** ONNX Runtime + DirectML nếu máy có card rời.

---

## Kiến trúc

DocPoint theo **Clean Architecture**: phụ thuộc chỉ hướng vào trong, lõi nghiệp vụ không biết gì về SQLite, PdfPig hay WPF.

| Project | Chứa gì | Phụ thuộc vào |
| --- | --- | --- |
| `DocPoint.Domain` | Thực thể `Document`, `Node`, `SearchHit`; quy tắc thuần (tạo nhãn vị trí, bỏ dấu) | Không gì cả |
| `DocPoint.Application` | Use case `AddFolder`, `IndexFile`, `Search`, `Backup`, `Restore`; hàng đợi lập chỉ mục chạy nền; các interface `IDocumentParser`, `IDocumentRepository`, `ISearchIndex`, `IEmbeddingService`, `IFolderScanner`, `IFileWatcher` | Domain |
| `DocPoint.Infrastructure` | Cài đặt interface: SQLite + FTS5, vector, PdfPig, OpenXml, ClosedXML, ONNX Runtime, FileSystemWatcher | Application, Domain |
| `DocPoint.App` | Giao diện WPF + MVVM, composition root (đăng ký DI) | Application (Infrastructure chỉ để đăng ký DI) |
| `DocPoint.Tests` | Unit test, integration test, bộ câu tìm thử | Tất cả |

Mỗi loại tài liệu là một `IDocumentParser` riêng (`LegalParser`, `HeadingParser`, `PageParser`, `ExcelParser`). Thêm loại mới chỉ cần thêm một class.

### Cấu trúc thư mục

```
DocPoint/
├── src/
│   ├── DocPoint.Domain/
│   ├── DocPoint.Application/
│   ├── DocPoint.Infrastructure/
│   └── DocPoint.App/
├── tests/
│   └── DocPoint.Tests/
├── samples/            # tài liệu mẫu và bộ câu tìm thử
├── DocPoint.sln
└── README.md
```

---

## Công nghệ

| Phần | Thư viện |
| --- | --- |
| Nền tảng | .NET 10 (LTS), C# |
| Giao diện | WPF, CommunityToolkit.Mvvm |
| Cơ sở dữ liệu | SQLite (Microsoft.Data.Sqlite) + FTS5 |
| Tìm theo nghĩa | Microsoft.ML.OnnxRuntime, Microsoft.ML.Tokenizers |
| Đọc PDF | UglyToad.PdfPig |
| Xem PDF | WebView2 |
| Đọc DOCX | DocumentFormat.OpenXml |
| Đọc Excel | ClosedXML (xlsx), ExcelDataReader (xls, csv) |
| Kiểm thử | xUnit |
| Đóng gói | dotnet publish (self-contained, single file) + Inno Setup |

---

## Yêu cầu

**Để chạy:** Windows 10 hoặc 11 (64-bit). Không cần cài .NET khi dùng bản cài đặt.

**Để phát triển:**
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2026 (workload *.NET desktop development*) hoặc JetBrains Rider. Visual Studio 2022 không target được `net10.0`.
- WebView2 Runtime (có sẵn trên Windows 11)

---

## Bắt đầu

```bash
# Lấy mã nguồn
git clone https://github.com/noone-crlt/DocPoint.git
cd DocPoint

# Khôi phục thư viện và build
dotnet restore
dotnet build

# Chạy app
dotnet run --project src/DocPoint.App

# Chạy test
dotnet test
```

### Đóng gói bản phát hành

```bash
dotnet publish src/DocPoint.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

File `.exe` nằm trong `src/DocPoint.App/bin/Release/net10.0-windows/win-x64/publish/`. Dùng Inno Setup để tạo file cài đặt.

---

## Lộ trình

- [ ] **Giai đoạn 0** — Chuẩn bị bộ tài liệu mẫu và 30 câu tìm thử có đáp án
- [ ] **Giai đoạn 1** — Bộ tách cấu trúc cho văn bản luật, tài liệu có Heading, PDF theo trang, Excel
- [ ] **Giai đoạn 2** — Full-text search với FTS5, hỗ trợ không dấu
- [ ] **Giai đoạn 3** — Giao diện WPF, chọn và theo dõi thư mục, xem PDF, sao lưu và khôi phục, đóng gói
- [ ] **Giai đoạn 4** — Từ đồng nghĩa và tìm theo nghĩa (hybrid search)
- [ ] **Giai đoạn 5** — Quan hệ và hiệu lực văn bản pháp luật
- [ ] **Về sau** — OCR, hỏi đáp AI kèm trích dẫn, gói cập nhật dữ liệu

---

## Nguồn dữ liệu

- **Văn bản pháp luật:** [Cơ sở dữ liệu quốc gia về pháp luật (vbpl.vn)](https://vbpl.vn) do Bộ Tư pháp quản lý. Văn bản quy phạm pháp luật không thuộc phạm vi bảo hộ quyền tác giả (khoản 2 Điều 15 Luật Sở hữu trí tuệ).
- **Tài liệu khác:** chính các file trong thư mục người dùng chọn.

DocPoint không thu thập dữ liệu từ các trang tra cứu tư nhân.

---

## Lưu ý

Kết quả tra cứu chỉ mang tính tham khảo. Luôn đối chiếu với văn bản gốc và kiểm tra tình trạng hiệu lực trước khi áp dụng. DocPoint không phải dịch vụ tư vấn pháp lý.