using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Q2.Api.Infrastructure.Errors;

/// <summary>
/// Which rule a field broke, as data a client can word: <c>tooShort</c> with
/// <c>min</c> 10, not "A password must be at least 10 characters long."
/// </summary>
/// <remarks>
/// The API sends structure, not sentences, wherever a person reads the result
/// (app/AGENTS.md section 11). A validation message is read by a person, in
/// German by default, so the client words it from its catalogue by this code;
/// the English message beside it is for whoever reads the raw response (#80,
/// docs/adr/0042-field-errors-as-codes.md).
/// </remarks>
/// <param name="Code">One of <see cref="FieldErrorCodes"/>.</param>
/// <param name="Min">The lower bound the rule names, when it names one.</param>
/// <param name="Max">The upper bound the rule names, when it names one.</param>
public sealed record FieldErrorCode(
    string Code,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Min = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Max = null);

/// <summary>
/// The codes a field error can carry. A short, closed list, mirrored by the
/// <c>validation</c> block of the frontend's catalogue: a new rule either fits
/// one of these or adds one on both sides.
/// </summary>
public static class FieldErrorCodes
{
    /// <summary>Something is wrong that has no code of its own; worded generically.</summary>
    public const string Invalid = "invalid";

    /// <summary>Nothing was given.</summary>
    public const string Required = "required";

    /// <summary>An address that already has an account.</summary>
    public const string Taken = "taken";

    /// <summary>Fewer than <see cref="FieldErrorCode.Min"/> characters.</summary>
    public const string TooShort = "tooShort";

    /// <summary>More than <see cref="FieldErrorCode.Max"/> characters.</summary>
    public const string TooLong = "tooLong";

    /// <summary>A number outside <see cref="FieldErrorCode.Min"/> to <see cref="FieldErrorCode.Max"/>.</summary>
    public const string OutOfRange = "outOfRange";

    /// <summary>More than <see cref="FieldErrorCode.Max"/> items.</summary>
    public const string TooMany = "tooMany";

    /// <summary>Not an email address.</summary>
    public const string Email = "email";

    /// <summary>Not one of the values on offer — an icon, a kind, a period.</summary>
    public const string UnknownOption = "unknownOption";

    /// <summary>A reset link that is invalid, expired or used.</summary>
    public const string ResetLink = "resetLink";

    /// <summary>A goal without a friend to check it.</summary>
    public const string NoChecker = "noChecker";

    /// <summary>A weekday schedule without a day in it.</summary>
    public const string NoWeekday = "noWeekday";

    /// <summary>A day that has already passed.</summary>
    public const string PastDay = "pastDay";

    /// <summary>A day more than <see cref="FieldErrorCode.Max"/> days ahead.</summary>
    public const string TooFarAhead = "tooFarAhead";

    /// <summary>A waiting Do that would not repeat.</summary>
    public const string MustRepeat = "mustRepeat";

    /// <summary>An edit that would turn a one-off into a repeating goal, or the other way round.</summary>
    public const string KindFixed = "kindFixed";

    /// <summary>A one-off moved to a later day when its deadline is less than a day away.</summary>
    public const string TooLateToMove = "tooLateToMove";

    /// <summary>The current password, given to change it, is not the account's (#40).</summary>
    public const string WrongPassword = "wrongPassword";

    /// <summary>Too many wrong passwords; the account is locked for a while (#40).</summary>
    public const string LockedOut = "lockedOut";

    /// <summary>A message older than <see cref="FieldErrorCode.Max"/> minutes, whose words can no longer be changed (#25).</summary>
    public const string TooLateToEdit = "tooLateToEdit";
}

/// <summary>
/// What is wrong with a request, per field: an English message for whoever
/// reads the response, and a <see cref="FieldErrorCode"/> for the client to
/// word in the reader's language.
/// </summary>
/// <remarks>
/// Read as a dictionary it is the messages alone, which is what a validation
/// problem's <c>errors</c> has always been, so code that only asks "is this
/// field wrong?" does not change. The codes travel beside it, as the problem's
/// <c>codes</c> extension (<see cref="ToProblem"/>, and the global handler for
/// a <see cref="DomainValidationException"/>).
/// </remarks>
public sealed class FieldErrors : IReadOnlyDictionary<string, string[]>
{
    private readonly Dictionary<string, string[]> _messages = [];
    private readonly Dictionary<string, FieldErrorCode[]> _codes = [];

    /// <summary>The codes, under the same field names as the messages.</summary>
    public IReadOnlyDictionary<string, FieldErrorCode[]> Codes => _codes;

    public int Count => _messages.Count;

    public IEnumerable<string> Keys => _messages.Keys;

    public IEnumerable<string[]> Values => _messages.Values;

    public string[] this[string key] => _messages[key];

    /// <summary>Records what is wrong with a field, replacing whatever was recorded for it.</summary>
    public void Add(string field, FieldErrorCode code, string message)
    {
        _messages[field] = [message];
        _codes[field] = [code];
    }

    /// <summary>Takes over everything another set says, field by field.</summary>
    public void Add(FieldErrors other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var (field, messages) in other._messages)
        {
            _messages[field] = messages;
            _codes[field] = other._codes[field];
        }
    }

    /// <summary>The answer a minimal-API endpoint gives: a validation problem with the codes beside the messages.</summary>
    public ValidationProblem ToProblem() =>
        TypedResults.ValidationProblem(_messages, extensions: new Dictionary<string, object?> { ["codes"] = _codes });

    public bool ContainsKey(string key) => _messages.ContainsKey(key);

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string[] value) => _messages.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<string, string[]>> GetEnumerator() => _messages.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
