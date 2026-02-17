using Microsoft.Extensions.Internal;

namespace Rstolsmark.UnifiClient.Tests;

public class TestClock : ISystemClock
{
    public DateTimeOffset UtcNow { get; set; }
}