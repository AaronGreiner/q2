using System.Threading.Channels;

namespace Q2.Api.Features.Accounts;

/// <summary>Where a request for a reset link is handed, so the answer does not wait on it.</summary>
/// <remarks>
/// The answer to "send me a link" must not depend on whether the address has an
/// account — not in what it says, and not in how long it takes. Looking the
/// account up, issuing a token and talking to an SMTP server all happen after
/// the response, so a request for an unknown address and one for a known one
/// take the same time by construction rather than by padding.
///
/// Two implementations, as with <c>INotificationQueue</c>: a queue a running
/// host drains in the background, and an inline one for <c>AutomatedTest</c>,
/// so a test can assert on the mail as soon as the request returns.
/// </remarks>
public interface IPasswordResetQueue
{
    ValueTask EnqueueAsync(string email, CancellationToken cancellationToken);
}

/// <summary>The queue a running host sends reset mails from.</summary>
/// <remarks>
/// In memory, bounded, and dropping the oldest when full. A restart loses what
/// was waiting, and what it loses is a mail somebody can ask for again with one
/// tap; a durable outbox would be a table and a job for a message whose whole
/// value runs out in an hour.
/// </remarks>
public sealed class PasswordResetQueue : IPasswordResetQueue
{
    /// <summary>Far more than the limit per client lets through between two reads.</summary>
    public const int Capacity = 200;

    private readonly Channel<string> _channel = Channel.CreateBounded<string>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    public ChannelReader<string> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(string email, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(email, cancellationToken);
}

/// <summary>Sends straight away, in the caller's own time. <c>AutomatedTest</c> only.</summary>
/// <remarks>
/// In a fresh scope rather than the caller's, exactly as the worker would.
/// </remarks>
public sealed class InlinePasswordResetQueue(IServiceScopeFactory scopes) : IPasswordResetQueue
{
    public async ValueTask EnqueueAsync(string email, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<PasswordResetMailer>()
            .SendAsync(email, cancellationToken);
    }
}

/// <summary>Reads <see cref="PasswordResetQueue"/> and sends, one at a time.</summary>
/// <remarks>
/// Does not run in <c>AutomatedTest</c>, where <see cref="InlinePasswordResetQueue"/>
/// stands in for it. A failed send is logged and the loop carries on: one
/// provider hiccup must not stop every mail behind it.
/// </remarks>
public sealed class PasswordResetMailWorker(
    PasswordResetQueue queue,
    IServiceScopeFactory scopes,
    ILogger<PasswordResetMailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var email in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider
                        .GetRequiredService<PasswordResetMailer>()
                        .SendAsync(email, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Never the address. The exception says what went wrong,
                    // and the scrubber removes an address a server echoed into it.
                    logger.LogError(exception, "A password reset mail could not be sent");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down. Whatever is still queued is dropped, deliberately
            // — see PasswordResetQueue.
        }
    }
}
