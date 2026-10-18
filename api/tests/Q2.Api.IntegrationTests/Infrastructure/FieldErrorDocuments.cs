namespace Q2.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A validation problem as the app reads it: the messages, and beside them the
/// code each field is worded by in the reader's language (#80).
/// </summary>
public sealed record CodedProblemDocument(
    IReadOnlyDictionary<string, string[]> Errors,
    IReadOnlyDictionary<string, FieldErrorCodeDocument[]> Codes)
{
    /// <summary>The one code a field carries.</summary>
    public FieldErrorCodeDocument CodeOf(string field) => Assert.Single(Codes[field]);
}

/// <summary>One rule a field broke, with the bounds it names.</summary>
public sealed record FieldErrorCodeDocument(string Code, int? Min, int? Max);
