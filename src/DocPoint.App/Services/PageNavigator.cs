namespace DocPoint.App.Services;

/// <summary>Chuyển trang trong khung chính. MainWindow lắng nghe <see cref="Navigated"/> để đổi nội dung.</summary>
public interface IPageNavigator
{
    event Action<Type>? Navigated;

    void Navigate(Type pageType);
}

public sealed class PageNavigator : IPageNavigator
{
    public event Action<Type>? Navigated;

    public void Navigate(Type pageType) => Navigated?.Invoke(pageType);
}
