using SampleGenerator;

// Sinh bộ tài liệu mẫu vào thư mục samples/. Chạy: dotnet run --project tools/SampleGenerator -- <samples-dir>
var dir = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "samples");
Directory.CreateDirectory(dir);

DocxSamples.WriteAll(dir);
PdfSamples.WriteAll(dir);
OtherSamples.WriteAll(dir);

Console.WriteLine($"Đã ghi {Directory.GetFiles(dir).Length} file vào {dir}");
