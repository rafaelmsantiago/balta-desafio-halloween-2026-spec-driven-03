namespace Encurtador.UnitTests.Fakes;

internal sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    public FakeTimeProvider()
        : this(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero))
    {
    }

    public DateTimeOffset UtcNow { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public void Advance(TimeSpan delta) => UtcNow += delta;
}
