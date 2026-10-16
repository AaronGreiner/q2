namespace Q2.Api.Features.Onboarding;

/// <summary>What a typed handle would be, from where the person stands.</summary>
public enum HandleAvailability
{
    /// <summary>Nobody has it, and it may be chosen.</summary>
    Available,

    /// <summary>Somebody else has it.</summary>
    Taken,

    /// <summary>Not a handle a person may choose — too short, or the wrong characters.</summary>
    Invalid,

    /// <summary>It is the one they already have.</summary>
    Current,
}

/// <summary>Request body for asking about a handle, and for choosing one.</summary>
/// <param name="Handle">As typed: with or without the "@", in any case.</param>
public sealed record HandleRequest(string? Handle = null);

/// <summary>What a typed handle would be.</summary>
/// <param name="Handle">
/// The handle as it would be stored — lowercase, with its "@" — so the screen
/// can show what the person is about to get rather than what they typed. Null
/// when nothing usable was typed.
/// </param>
public sealed record HandleCheckResponse(string? Handle, HandleAvailability Availability);
