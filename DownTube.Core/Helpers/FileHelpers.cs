using System.Globalization;
using System.Text;

namespace DownTube.Core.Helpers;

public static class FileHelpers
{
    private static readonly Dictionary<char, char> InvalidCharsDict = new InvalidChars().Dict;

    public static string NormalizeFilenameOrPath(string str)
    {
        str = str.ToLower().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in str)
        {
            var currentChar = c;
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(currentChar);
            if (unicodeCategory == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (InvalidCharsDict.TryGetValue(currentChar, out var replacement))
            {
                currentChar = replacement;
            }

            sb.Append(currentChar);
        }

        str = sb.ToString().Normalize(NormalizationForm.FormC).Trim();
        str = RegexHelpers.MatchWhitespaces().Replace(str, "_");
        str = RegexHelpers.MatchEmojis().Replace(str, string.Empty);
        if (str.Length > 100)
        {
            str = str[..100];
        }

        if (str.EndsWith('_'))
        {
            str = str.TrimEnd('_');
        }       

        return str;
    }

    public static bool IsExistingFile(string filename, string path)
    {
        var files = Directory.GetFiles(path);
        return files.Any(
            f => Path.GetFileNameWithoutExtension(f).Equals(filename, StringComparison.OrdinalIgnoreCase));
    }
}