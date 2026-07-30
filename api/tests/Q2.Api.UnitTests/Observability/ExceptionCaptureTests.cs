using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.UnitTests.Observability;

/// <summary>
/// The rule that stops one exception from becoming several Sentry issues as it
/// travels through the layers. The end-to-end proof — one failing request
/// producing exactly one event — lives in the integration tests.
/// </summary>
[Trait("Category", "Sentry")]
public class ExceptionCaptureTests
{
    [Fact]
    public void TheFirstCallerMayReport()
    {
        var exception = new InvalidOperationException("boom");

        Assert.True(ExceptionCapture.TryMarkForCapture(exception));
    }

    [Fact]
    public void EveryLaterCallerIsTurnedAway()
    {
        var exception = new InvalidOperationException("boom");

        ExceptionCapture.TryMarkForCapture(exception);

        Assert.False(ExceptionCapture.TryMarkForCapture(exception));
        Assert.False(ExceptionCapture.TryMarkForCapture(exception));
    }

    [Fact]
    public void DistinctExceptionsAreTrackedSeparately()
    {
        var first = new InvalidOperationException("boom");
        var second = new InvalidOperationException("boom");

        Assert.True(ExceptionCapture.TryMarkForCapture(first));
        Assert.True(ExceptionCapture.TryMarkForCapture(second));
    }

    [Fact]
    public void CaptureStateIsReadableAfterwards()
    {
        var exception = new InvalidOperationException("boom");

        Assert.False(ExceptionCapture.WasCaptured(exception));

        ExceptionCapture.TryMarkForCapture(exception);

        Assert.True(ExceptionCapture.WasCaptured(exception));
    }
}
