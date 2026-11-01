using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using DocPoint.Domain;

namespace DocPoint.App.Controls;

/// <summary>
/// Gắn vào TextBlock: <c>controls:HighlightText.Source="{Binding Snippet}"</c>. Đoạn nằm giữa
/// <see cref="SearchHit.HighlightStart"/> và <see cref="SearchHit.HighlightEnd"/> được tô bằng DpHlBrush.
/// </summary>
public static class HighlightText
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached(
        "Source", typeof(string), typeof(HighlightText), new PropertyMetadata(null, OnSourceChanged));

    public static string? GetSource(DependencyObject d) => (string?)d.GetValue(SourceProperty);

    public static void SetSource(DependencyObject d, string? value) => d.SetValue(SourceProperty, value);

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock block)
            return;

        block.Inlines.Clear();
        if (e.NewValue is not string text)
            return;

        var index = 0;
        while (index < text.Length)
        {
            var start = text.IndexOf(SearchHit.HighlightStart, index);
            if (start < 0)
            {
                block.Inlines.Add(new Run(text[index..]));
                break;
            }

            if (start > index)
                block.Inlines.Add(new Run(text[index..start]));

            var end = text.IndexOf(SearchHit.HighlightEnd, start + 1);
            if (end < 0)
                end = text.Length;

            var run = new Run(text[(start + 1)..end]);
            run.SetResourceReference(TextElement.BackgroundProperty, "DpHlBrush");
            run.SetResourceReference(TextElement.ForegroundProperty, "DpHlInkBrush");
            block.Inlines.Add(run);
            index = end + 1;
        }
    }
}
