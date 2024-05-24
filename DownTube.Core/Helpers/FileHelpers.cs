using System.Globalization;
using System.Text;

namespace DownTube.Core.Helpers;

public static class FileHelpers
{
    public static string NormalizeFilenameOrPath(string str)
    {
        var validChar = new Func<char, bool>(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');
        if (str.All(c => validChar(c)))
        {
            return str;
        }

        str = str.ToLower().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in from c in str
                 let unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c)
                 where unicodeCategory != UnicodeCategory.NonSpacingMark
                 select c)
        {
            if (validChar(c))
            {
                sb.Append(c);
            }
            else
            {
                switch (c)
                {
                    case '-':
                        sb.Append('_');
                        break;
                    case '@':
                        sb.Append('a');
                        break;
                    case '$':
                        sb.Append('s');
                        break;
                    case '&':
                        sb.Append('e');
                        break;
                    default:
                        sb.Append(' ');
                        break;
                }
            }
        }

        str = sb.ToString().Normalize(NormalizationForm.FormC).Trim();
        if (str.Length > 50)
        {
            str = str[..50];
        }

        str = RegexHelpers.MatchWhitespaces().Replace(str, "_");
        str = RegexHelpers.MatchTwoOrMoreUnderscores().Replace(str, "_");
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