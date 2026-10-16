namespace Q2.Api.Features.People;

/// <summary>
/// What a handle a person types for themselves may look like.
/// </summary>
/// <remarks>
/// Derived handles (<c>ProfileDefaults</c>) are made of lowercase letters,
/// digits and dots, with "-2" for a duplicate. A chosen one follows the same
/// alphabet plus the underscore people expect from elsewhere, and a few rules
/// that keep it readable when somebody says it out loud or types it into the
/// search box: at least three characters, starting and ending with a letter or
/// a digit, and never two separators in a row.
///
/// Lowercase only, and folded here rather than refused: "@Lena" and "@lena"
/// would be two people to the unique index and one to everybody reading them.
/// </remarks>
public static class Handles
{
    /// <summary>The shortest body a person may choose, without the "@".</summary>
    public const int MinBodyLength = 3;

    /// <summary>The longest body, without the "@".</summary>
    public const int MaxBodyLength = Person.MaxHandleLength - 1;

    /// <summary>The rule in a sentence, for the field-level message.</summary>
    public const string Rule =
        "A handle has 3 to 39 characters: letters, digits, dots, hyphens or underscores, "
        + "starting and ending with a letter or a digit.";

    /// <summary>
    /// The handle somebody meant: trimmed, lowercased, with exactly one "@" in
    /// front. Null when there is nothing left of it.
    /// </summary>
    public static string? Normalise(string? typed)
    {
        var body = typed?.Trim().TrimStart('@').ToLowerInvariant() ?? string.Empty;
        return body.Length == 0 ? null : $"@{body}";
    }

    /// <summary>True when <paramref name="handle"/>, "@" included, is one a person may choose.</summary>
    public static bool IsChoosable(string? handle)
    {
        if (handle is null || !handle.StartsWith('@'))
        {
            return false;
        }

        var body = handle[1..];

        if (body.Length is < MinBodyLength or > MaxBodyLength)
        {
            return false;
        }

        if (!IsLetterOrDigit(body[0]) || !IsLetterOrDigit(body[^1]))
        {
            return false;
        }

        for (var index = 0; index < body.Length; index++)
        {
            var character = body[index];

            if (IsLetterOrDigit(character))
            {
                continue;
            }

            if (!IsSeparator(character) || IsSeparator(body[index - 1]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLetterOrDigit(char character) => character is >= 'a' and <= 'z' or >= '0' and <= '9';

    private static bool IsSeparator(char character) => character is '.' or '-' or '_';
}
