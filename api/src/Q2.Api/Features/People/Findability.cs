namespace Q2.Api.Features.People;

/// <summary>Who can find somebody through the people search (#106).</summary>
/// <remarks>
/// About strangers only: somebody already a friend, or with a request open
/// either way, is found as before. The invite link works whatever this says —
/// it is the person handing out their own way in.
/// </remarks>
public enum Findability
{
    /// <summary>Any part of the name or the handle finds them. The default, as it always was.</summary>
    Everyone,

    /// <summary>Only the whole handle, typed exactly — "@lena.s" or "lena.s".</summary>
    Handle,

    /// <summary>Nobody through the search: only through their invite link.</summary>
    Link,
}
