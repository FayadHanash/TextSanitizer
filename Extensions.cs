using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TextSanitizer;

public static class Extensions
{
    public static readonly Regex UnicodeEscapes =
        new Regex(@"\\(?:u[0-9A-Fa-f]{4,8}|U[0-9A-Fa-f]{8}|[uU]\{[0-9A-Fa-f]{1,8}\})", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    public static readonly Regex ZeroWidthChars =
        new Regex(@"[\u200B-\u200D\uFEFF\u2060-\u2064]", RegexOptions.Compiled);
    public static readonly Regex ExcessiveBrackets =
        new Regex(@"\[.*?\]|\(.*?\)", RegexOptions.Compiled);
    public static readonly Regex FormatChars =
        new Regex(@"\p{Cf}", RegexOptions.Compiled);
    public static readonly Regex ControlChars =
        new Regex(@"[\u0000-\u001F\u007F-\u009F]", RegexOptions.Compiled);
    public static readonly Regex CombiningMarks =
        new Regex(@"\p{Mn}", RegexOptions.Compiled);
    public static readonly Regex InvisibleSpaces =
        new Regex(@"[\u00A0\u202F\u205F\u3000]", RegexOptions.Compiled);

    public static string Sanitize(this string text, string replacement = " ", params Regex[] regexes)
    {
        foreach (var regex in regexes)
        {
            text = regex.Replace(text, replacement);
        }
        return text;
    }
    public static string RemoveFormatChars(this string text, string rep = " ")
        => FormatChars.Replace(text, rep);
    public static string RemoveZeroWidths(this string text, string rep = " ")
        => ZeroWidthChars.Replace(text, rep);
    public static string RemoveUnicodedEscapes(this string text, string rep = " ")
        => UnicodeEscapes.Replace(text, rep);
    public static string RemoveExcessiveBrackets(this string text, string rep = " ")
        => ExcessiveBrackets.Replace(text, rep);
    public static string RemoveControlChars(this string text, string rep = " ")
    => ControlChars.Replace(text, rep);
    public static string RemoveCombiningMarks(this string text, string rep = " ")
    => CombiningMarks.Replace(text, rep);
    public static string RemoveInvisibleSpaces(this string text, string rep = " ")
    => InvisibleSpaces.Replace(text, rep);

    public static string CollapseWhitespace(this string text, string rep = " ")
        => Regex.Replace(text, @"\s+", rep);
    public static string RemoveSpaceBeforePunct(this string text, string rep = "$1")
    => Regex.Replace(text, @"\s+([,.;:!?])", rep);


}