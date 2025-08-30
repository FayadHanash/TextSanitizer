using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TextSanitizer;

public class SanitizeText
{
    public static string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        input = input
            .RemoveUnicodedEscapes()
            .RemoveZeroWidths()
            .RemoveFormatChars()
            .RemoveControlChars()
            .RemoveCombiningMarks()
            .RemoveInvisibleSpaces();
        ReadOnlySpan<char> inputSpan = input.AsSpan();
        int initialLength = inputSpan.Length;
        char[] arrayFromPool = null;
        try
        {
            Span<char> buffer = initialLength <= 256 ?
                stackalloc char[initialLength] :
                (arrayFromPool = ArrayPool<char>.Shared.Rent(initialLength));
            int writeX = RemoveUnSupportedCharacters(inputSpan, buffer);
            Span<char> sanitized = buffer[..writeX];
            return sanitized
                .ToString()
                .CollapseWhitespace()
                .RemoveSpaceBeforePunct()
                .Trim();
        }
        finally
        {
            if (arrayFromPool is not null)
            {
                ArrayPool<char>.Shared.Return(arrayFromPool);
            }
        }

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RemoveUnSupportedCharacters(ReadOnlySpan<char> input, Span<char> buffer)
    {
        int count = 0;
        foreach (char c in input)
        {
            if (IsAllowedChar(c))
            {
                buffer[count++] = c;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (count == 0 || !char.IsWhiteSpace(buffer[count - 1]))
                {
                    buffer[count++] = ' ';
                }
            }
        }
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAllowedChar(char c)
    {
        if (c <= '\u007F' && c >= ' ') return true;
        ushort cp = c;
        foreach ((ushort s, ushort e) b in SupportedScriptBlock)
        {
            if (cp >= b.s && cp <= b.e)
            {
                return true;
            }
        }

        return char.GetUnicodeCategory(c) switch
        {
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.DecimalDigitNumber or
            UnicodeCategory.LetterNumber or
            UnicodeCategory.OtherNumber or
            UnicodeCategory.DashPunctuation or
            UnicodeCategory.OpenPunctuation or
            UnicodeCategory.ClosePunctuation or
            UnicodeCategory.InitialQuotePunctuation or
            UnicodeCategory.FinalQuotePunctuation or
            UnicodeCategory.OtherPunctuation or
            UnicodeCategory.CurrencySymbol or
            UnicodeCategory.MathSymbol or
            UnicodeCategory.OtherSymbol
                => true,
            _ => false
        };
    }

    public static FormattedString ShowHiddenChars(string text)
    {
        FormattedString fs = new();
        StringBuilder sb = new();
        for (int i = 0; i < text.Length;)
        {

            if (i + 5 < text.Length && text[i] == '\\' && (text[i + 1] == 'u' || text[i + 1] == 'U'))
            {
                int code = 0;
                bool validHex = true;
                for (int j = 2; j < 6; j++)
                {
                    char ch = text[i + j];
                    int val = ch switch
                    {
                        >= '0' and <= '9' => ch - '0',
                        >= 'A' and <= 'F' => 10 + (ch - 'A'),
                        >= 'a' and <= 'f' => 10 + (ch - 'a'),
                        _ => -1
                    };

                    if (val < 0)
                    {
                        validHex = false;
                        break;
                    }
                    code = (code << 4) | val;
                }
                if (validHex)
                {
                    Flush();
                    fs.Spans.Add(new Span
                    {
                        Text = text.Substring(i, 6),
                        TextColor = Colors.Red
                    });
                    i += 6;
                    continue;
                }
            }

            char c = text[i];
            if (IsHiddenChar(c))
            {
                Flush();
                var code = $"\\u{(int)c:X4}";
                fs.Spans.Add(new Span
                {
                    Text = code,
                    TextColor = Colors.Red,
                });
                i++;
                continue;
            }

            sb.Append(c);
            i++;
        }

        Flush();
        return fs;

        void Flush()
        {
            if (sb.Length > 0)
            {
                fs.Spans.Add(new Span
                {
                    Text = sb.ToString(),

                });
                sb.Clear();
            }
        }
    }
    static bool IsHiddenChar(char c) 
    {
        var cat = char.GetUnicodeCategory(c);
        if (cat == UnicodeCategory.Control
            || cat == UnicodeCategory.Format
            || cat == UnicodeCategory.Surrogate
            || cat == UnicodeCategory.OtherNotAssigned)
            return true;

        return c switch
        {
            '\u200B' or '\u200C' or '\u200D' or '\uFEFF' or '\u2060' or
            '\u2061' or '\u2062' or '\u2063' or '\u2064' or
            '\u202A' or '\u202B' or '\u202C' or '\u202D' or '\u202E' or '\u202F' or
            '\u00A0' or '\u202F' or '\u205F' or '\u3000' => true,
            '\u180E' => true,
            _ => false,
        };
    }

    // Unicode blocks for supported languages
    private static readonly (ushort Start, ushort End)[] SupportedScriptBlock =
    {
            // Basic Latin (English, Swedish, German, Kurdish Latin)
            (0x0020, 0x007F), (0x00A0, 0x00FF),
            
            // Latin Extended (European languages, Kurdish)
            (0x0100, 0x017F), (0x0180, 0x024F),
            (0x1E00, 0x1EFF), // Latin Extended Additional
            
            // Arabic Script (Arabic, Kurdish Arabic)
            (0x0600, 0x06FF), (0x0750, 0x077F), (0x08A0, 0x08FF),
            (0x0870, 0x089F), (0xFB50, 0xFDFF), (0xFE70, 0xFEFF),
            
            // Chinese: CJK Unified Ideographs
            (0x4E00, 0x9FFF), (0x3400, 0x4DBF),
            (0x3000, 0x303F), (0xFF00, 0xFFEF),
            
            // General Punctuation
            (0x2000, 0x206F), (0x2E00, 0x2E7F),
            
            // Currency and symbols
            (0x20A0, 0x20CF),
    };

}
