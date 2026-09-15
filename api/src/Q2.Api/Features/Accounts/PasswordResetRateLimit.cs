using System.Threading.RateLimiting;

namespace Q2.Api.Features.Accounts;

/// <summary>How often one client may ask for a reset link.</summary>
/// <remarks>
/// Every request for a link can send a mail, and a mail costs things the
/// endpoint cannot see: the provider's quota, the sending domain's standing
/// with spam filters, and somebody's inbox. So one client gets
/// <see cref="DefaultRequestsPerClient"/> requests in <see cref="Window"/>, and
/// the next is answered 429 until the window rolls over.
/// <see cref="PasswordResetCooldown"/> covers the other direction — many
/// clients, one inbox.
///
/// A client is its address, and that is the real one only because the forwarded
/// headers from the reverse proxy are applied first (ApiRegistration). Without
/// them every request would come from 127.0.0.1 and share one budget.
/// </remarks>
public static class PasswordResetRateLimit
{
    public const string PolicyName = "password-reset-request";

    public const string RequestsPerClientKey = "Q2:PasswordReset:RequestsPerClient";

    public const int DefaultRequestsPerClient = 5;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    public static IServiceCollection AddPasswordResetRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        var permits = configuration.GetValue(RequestsPerClientKey, DefaultRequestsPerClient);

        return services.AddRateLimiter(options =>
        {
            // A body is written by UseStatusCodePages, which answers a bare 429
            // with Problem Details like every other refusal.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permits,
                    Window = Window,
                    QueueLimit = 0,
                }));
        });
    }
}
