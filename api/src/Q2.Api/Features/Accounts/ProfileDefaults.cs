using System.Collections.Frozen;
using System.Text;
using Q2.Api.Features.People;

namespace Q2.Api.Features.Accounts;

/// <summary>
/// The profile a new account starts with, derived from the name somebody typed.
/// </summary>
/// <remarks>
/// Registration asks for three things — name, email address, password — and a
/// <see cref="Person"/> needs two more: a handle and an avatar. Asking for
/// those as well would be two more fields to get wrong on a phone keyboard, so
/// they are derived here instead.
///
/// Every function is pure and deterministic. The same name always produces the
/// same handle and the same colour, which is what lets a seed state an expected
/// handle and a test assert one.
/// </remarks>
public static class ProfileDefaults
{
    /// <summary>Room for the "-2", "-3" a duplicate handle needs.</summary>
    private const int SuffixAllowance = 4;

    /// <summary>What a name that survives none of the normalisation becomes.</summary>
    public const string FallbackHandle = "kudos";

    /// <summary>
    /// Letters that are not a–z, and what they become in a handle.
    /// </summary>
    /// <remarks>
    /// Spelled out rather than derived from Unicode normalisation, because
    /// <c>InvariantGlobalization</c> is on for the whole backend
    /// (api/Directory.Build.props): without ICU, <c>String.Normalize</c> leaves
    /// non-ASCII text exactly as it found it, and "Zoë" would have quietly
    /// become "zo".
    ///
    /// German comes first and is the reason this is a map to <em>strings</em>
    /// rather than to characters: "Jürgen Groß" has to become "juergen.gross".
    /// Reducing ü to u would produce "jurgen", which is not a name its owner
    /// would recognise. Everything after that reduces to the base letter, which
    /// is what the rest of Europe expects of a handle.
    ///
    /// A letter that is in neither list is treated as a separator. That is a
    /// real limit — a name written in Greek or Cyrillic derives no handle and
    /// falls back to <see cref="FallbackHandle"/> — and the moment somebody
    /// needs one, this is the table to extend.
    /// </remarks>
    private static readonly FrozenDictionary<char, string> Folded = BuildFoldingTable();

    private static FrozenDictionary<char, string> BuildFoldingTable()
    {
        var table = new Dictionary<char, string>();

        void Add(string characters, string replacement)
        {
            foreach (var character in characters)
            {
                table[character] = replacement;
            }
        }

        Add("äÄ", "ae");
        // ø joins ö rather than reducing to "o": "Søren" is conventionally
        // written "Soeren" when it has to be ASCII, for the same reason "Jörg"
        // is "Joerg".
        Add("öÖøØ", "oe");
        Add("üÜ", "ue");
        Add("ßẞ", "ss");

        Add("àáâãåāăąÀÁÂÃÅĀĂĄ", "a");
        Add("çćĉċčÇĆĈĊČ", "c");
        Add("ďđĎĐ", "d");
        Add("èéêëēĕėęěÈÉÊËĒĔĖĘĚ", "e");
        Add("ĝğġģĜĞĠĢ", "g");
        Add("ìíîïĩīĭįıÌÍÎÏĨĪĬĮİ", "i");
        Add("ĺļľłĹĻĽŁ", "l");
        Add("ñńņňÑŃŅŇ", "n");
        Add("òóôõōŏőÒÓÔÕŌŎŐ", "o");
        Add("ŕŗřŔŖŘ", "r");
        Add("śŝşšŚŜŞŠ", "s");
        Add("ţťŧŢŤŦ", "t");
        Add("ùúûũūŭůűųÙÚÛŨŪŬŮŰŲ", "u");
        Add("ýÿŷÝŸŶ", "y");
        Add("źżžŹŻŽ", "z");
        Add("æÆ", "ae");
        Add("œŒ", "oe");

        return table.ToFrozenDictionary();
    }

    /// <summary>
    /// The handle body for <paramref name="displayName"/>, without the leading
    /// "@" and without a uniqueness suffix — both are added by the caller,
    /// which is the only thing that can know what is already taken.
    /// </summary>
    public static string HandleBody(string? displayName)
    {
        var name = displayName?.Trim() ?? string.Empty;
        var body = new StringBuilder(name.Length);

        foreach (var character in name)
        {
            if (Folded.TryGetValue(character, out var replacement))
            {
                body.Append(replacement);
                continue;
            }

            var lowered = char.ToLowerInvariant(character);

            if (lowered is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                body.Append(lowered);
            }
            else if (body.Length > 0 && body[^1] != '.')
            {
                // Anything else — a space, a hyphen, an emoji — is a separator,
                // and a run of them is still just one.
                body.Append('.');
            }
        }

        var result = body.ToString().Trim('.');
        var limit = Person.MaxHandleLength - 1 - SuffixAllowance;

        if (result.Length > limit)
        {
            result = result[..limit].Trim('.');
        }

        return result.Length == 0 ? FallbackHandle : result;
    }

    /// <summary>The handle for <paramref name="body"/>, optionally disambiguated.</summary>
    /// <param name="attempt">
    /// 1 is the plain handle. Anything higher appends the number, which is how
    /// the second Lena Schmidt gets "@lena.schmidt-2".
    /// </param>
    public static string Handle(string body, int attempt) =>
        attempt <= 1 ? $"@{body}" : $"@{body}-{attempt}";

    /// <summary>One or two letters for the avatar.</summary>
    public static string Initials(string? displayName)
    {
        var words = (displayName ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => word.Where(char.IsLetterOrDigit).ToArray())
            .Where(letters => letters.Length > 0)
            .Take(2)
            .ToList();

        if (words.Count == 0)
        {
            return "?";
        }

        // One word gives one letter rather than its first two: "Mara" is "M",
        // which is how the seeded avatars read.
        return string.Concat(words.Select(word => char.ToUpperInvariant(word[0])));
    }

    /// <summary>
    /// An avatar colour from the closed palette, chosen by the handle.
    /// </summary>
    /// <remarks>
    /// Deterministic, and deliberately not <see cref="string.GetHashCode()"/>:
    /// .NET randomises string hashing per process, so the same person would
    /// change colour every time the server restarted.
    /// </remarks>
    public static string AvatarColor(string handle)
    {
        var hash = 2166136261u;

        foreach (var character in handle)
        {
            hash = (hash ^ character) * 16777619u;
        }

        return AvatarColors.All[(int)(hash % (uint)AvatarColors.All.Count)];
    }
}
