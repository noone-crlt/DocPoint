namespace DocPoint.Application;

/// <summary>Đồng hồ để test được những phần phụ thuộc thời gian (nhóm Hôm nay/Hôm qua, thử lại...).</summary>
public interface IClock
{
    DateTime Now { get; }
}
