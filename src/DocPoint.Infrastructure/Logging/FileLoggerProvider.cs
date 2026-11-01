using System.Text;
using Microsoft.Extensions.Logging;

namespace DocPoint.Infrastructure.Logging;

/// <summary>Ghi log ra file theo ngày: logs\docpoint-20261101.log. Lỗi ghi log không bao giờ làm app dừng.</summary>
public sealed class FileLoggerProvider(string directory) : ILoggerProvider
{
    private readonly object _gate = new();

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void Write(string line)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, $"docpoint-{DateTime.Now:yyyyMMdd}.log"), line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (IOException)
        {
            // Đĩa đầy hoặc file bị khoá: bỏ dòng log này.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class FileLogger(FileLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var line = $"{DateTime.Now:HH:mm:ss.fff} [{logLevel}] {category}: {formatter(state, exception)}";
            if (exception is not null)
                line += Environment.NewLine + exception;
            owner.Write(line);
        }
    }
}
