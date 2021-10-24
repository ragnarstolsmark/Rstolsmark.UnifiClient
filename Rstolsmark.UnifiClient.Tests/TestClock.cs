using System;
using Microsoft.Extensions.Internal;

public class TestClock : ISystemClock
{
    public DateTimeOffset UtcNow { get; set; }
}