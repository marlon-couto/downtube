using System.Text.RegularExpressions;

namespace DownTube.Core.Helpers;

public static partial class RegexHelpers
{
    [GeneratedRegex(@"\s+")]
    public static partial Regex MatchWhitespaces();

    [GeneratedRegex(@"\p{Cs}")]
    public static partial Regex MatchEmojis();
}