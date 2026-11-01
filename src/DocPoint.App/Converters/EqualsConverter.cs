using System.Globalization;
using System.Windows.Data;

namespace DocPoint.App.Converters;

/// <summary>True khi giá trị (enum, chuỗi…) bằng tham số, để gắn IsChecked của nút điều hướng.</summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
