using DocPoint.Application;

namespace DocPoint.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}
