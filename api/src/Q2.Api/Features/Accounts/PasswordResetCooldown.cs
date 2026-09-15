using System.Collections.Concurrent;

namespace Q2.Api.Features.Accounts;

/// <summary>At most one reset mail per account in <see cref="AccountPolicy.PasswordResetMailCooldown"/>.</summary>
/// <remarks>
/// The limit on the endpoint stops one client from asking for many mails
/// (<see cref="PasswordResetRateLimit"/>); this stops many clients from filling
/// one inbox — which is what somebody would do to make q2 a nuisance to one of
/// its own people.
///
/// In memory and a singleton, because q2 is one process on one host
/// (docs/adr/0008-deployment-topology.md), the same posture as
/// <c>LiveConnections</c>. A restart forgets it, which costs at most one extra
/// mail. There is one entry per account that asked since the last start, so it
/// cannot grow past the number of accounts.
/// </remarks>
public sealed class PasswordResetCooldown
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastSent = new();

    /// <summary>Claims the next mail for an account, or says one went out too recently.</summary>
    public bool TryClaim(Guid accountId, DateTimeOffset now)
    {
        while (true)
        {
            if (!_lastSent.TryGetValue(accountId, out var last))
            {
                if (_lastSent.TryAdd(accountId, now))
                {
                    return true;
                }

                continue;
            }

            if (now - last < AccountPolicy.PasswordResetMailCooldown)
            {
                return false;
            }

            if (_lastSent.TryUpdate(accountId, now, last))
            {
                return true;
            }
        }
    }

    /// <summary>Gives a claim back, for a mail that never left.</summary>
    /// <remarks>
    /// A provider that was down for a moment must not cost somebody the next
    /// two minutes as well.
    /// </remarks>
    public void Release(Guid accountId) => _lastSent.TryRemove(accountId, out _);

    /// <summary>
    /// Forgets every claim. The integration tests' host outlives a single test,
    /// and a claim made by one test must not decide the next.
    /// </summary>
    internal void Clear() => _lastSent.Clear();
}
