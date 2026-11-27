using System.Globalization;

namespace Aida.Arms.Health.Tests;

public sealed class ManualTime : TimeProvider
{
    private static readonly DateTimeOffset DefaultInitialUtc =
        DateTimeOffset.Parse("2026-11-20T15:00:00Z", CultureInfo.InvariantCulture);

    private static readonly TimeZoneInfo CustomUtcMinus5 =
        TimeZoneInfo.CreateCustomTimeZone("UTC-05:00", TimeSpan.FromHours(-5), "UTC-05:00", "UTC-05:00");

    private DateTimeOffset _now;

    public ManualTime(DateTimeOffset? initialUtc = null)
    {
        _now = initialUtc ?? DefaultInitialUtc;
    }

    public DateTimeOffset Now
    {
        get => _now;
        set => _now = value;
    }

    public override DateTimeOffset GetUtcNow() => _now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone => CustomUtcMinus5;

    public void Advance(TimeSpan span)
    {
        _now = _now.Add(span);
    }
}
