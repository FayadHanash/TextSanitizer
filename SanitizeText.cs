using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TextSanitizer;

public static class SanitizeText
{
    public static string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        return input
            .RemoveUnicodedEscapes()
            .RemoveZeroWidths()
            .RemoveFormatChars()
            .RemoveControlChars()
            .RemoveCombiningMarks()
            .RemoveInvisibleSpaces()
            .RemoveUnSupportedCharacters()
            .CollapseWhitespace()
            .RemoveSpaceBeforePunct()
            .Trim();
    }

    public static string RemoveUnSupportedCharacters(this string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        int length = input.Length;
        char[]? pool = null;
        bool changed = false;
        try
        {
            ReadOnlySpan<char> text = input.AsSpan();
            Span<char> buffer = length <= 256
                ? stackalloc char[length]
                : (pool = ArrayPool<char>.Shared.Rent(length)).AsSpan(0, length);

            int idx = 0;
            for (int i = 0; i < length;)
            {
                ReadOnlySpan<char> slice = text.Slice(i);
                if (TryParseEscape(slice, out int escLen, out int cp, out var _))
                {
                    if (IsAllowedChar(cp))
                    {
                        var rune = new Rune(cp);
                        rune.TryEncodeToUtf16(buffer.Slice(idx), out int wrt);
                        idx += wrt;
                    }
                    if (escLen != (IsAllowedChar(cp) ? (new Rune(cp)).Utf16SequenceLength : 0))
                    {
                        changed = true;
                    }
                    i += escLen;
                    continue;
                }
                if (Rune.DecodeFromUtf16(slice, out Rune rune2, out int rLen) == OperationStatus.Done)
                {
                    if (IsAllowedChar(rune2.Value))
                    {
                        rune2.TryEncodeToUtf16(buffer.Slice(idx), out int wrt);
                        idx += wrt;
                    }
                    else
                    {
                        changed = true;
                    }
                    i += rLen;
                    continue;
                }
                char c = slice[0];
                if (IsAllowedChar(c))
                {
                    buffer[idx] = c;
                }
                else
                {
                    changed = true;
                }

                i++;
            }

            if (!changed && idx == length) return input;

            return new string(buffer.Slice(0, idx));
        }
        finally
        {
            if (pool is not null)
            {
                ArrayPool<char>.Shared.Return(pool);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAllowedChar(int cp)
    {
        foreach (var (s, e) in SupportedScriptBlock)
        {
            if ((uint)(cp - s) <= (uint)(e - s))
            {
                return true;
            }
        }
        return false;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseEscape(ReadOnlySpan<char> sp, out int length, out int cp, out ReadOnlySpan<char> literal)
    {
        length = 0;
        cp = 0;
        literal = default;

        if (sp.Length < 2 || sp[0] != '\\' || (sp[1] is not ('u' or 'U')))
        {
            return false;
        }

        bool isUpperU = sp[1] == 'U';
        bool braced = sp.Length > 2 && sp[2] == '{';
        int hexStart = braced ? 3 : 2;
        int minHex, maxHex;
        if (isUpperU)
        {
            minHex = braced ? 1 : 8;
            maxHex = 8;
        }
        else
        {
            minHex = braced ? 1 : 4;
            maxHex = 6;
        }
        int idx = hexStart;
        while (idx < sp.Length && idx - hexStart < maxHex && IsHexDigit(sp[idx]))
        {
            idx++;
        }

        int hexCount = idx - hexStart;

        if (braced)
        {
            if (hexCount < minHex || idx >= sp.Length || sp[idx] != '}')
            {
                return false;
            }
            length = idx + 1;
        }
        else
        {
            if (hexCount < minHex || hexCount > maxHex)
            {
                return false;
            }
            length = hexStart + hexCount;
        }
        if (!TryParseHex(sp.Slice(hexStart, hexCount), out cp))
        {
            return false;
        }

        if (cp < 0 || cp > 0x10FFFF || (cp is >= 0xD800 and <= 0xDFFF))
        {
            return false;
        }
        literal = sp.Slice(0, length);
        return true;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HexValue(char c)
    {
        return c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'A' and <= 'F' => 10 + (c - 'A'),
            >= 'a' and <= 'f' => 10 + (c - 'a'),
            _ => -1
        };
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHexDigit(char c) => (uint)(c - '0') <= 9 || (uint)(c - 'A') <= 5 || (uint)(c - 'a') <= 5;

    private static bool TryParseHex(ReadOnlySpan<char> hex, out int value)
    {
        value = 0;
        foreach (char c in hex)
        {
            int nib = HexValue(c);
            if (nib < 0)
            {
                return false;
            }
            value = (value << 4) | nib;
        }
        return true;
    }


    public static FormattedString ShowHiddenChars(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new FormattedString { Spans = { new Span { Text = input ?? string.Empty, } } };
        }
        FormattedString fs = new();
        char[]? pool = null;
        int idx = 0;
        ReadOnlySpan<char> text = input.AsSpan();
        int length = text.Length;
        Span<char> buffer = length <= 256
            ? stackalloc char[length]
            : (pool = ArrayPool<char>.Shared.Rent(length)).AsSpan(0, length);
        try
        {

            for (int i = 0; i < length;)
            {
                ReadOnlySpan<char> slice = text.Slice(i);
                if (TryParseEscape(slice, out int escLen, out int cp, out var literal))
                {
                    if (idx > 0)
                    {
                        fs.Spans.Add(new Span
                        {
                            Text = new string(buffer.Slice(0, idx))
                        });
                        idx = 0;
                    }

                    fs.Spans.Add(new Span
                    {
                        Text = literal.ToString(),
                        TextColor = Colors.Red
                    });

                    i += escLen;
                    continue;
                }
                if (Rune.DecodeFromUtf16(slice, out Rune rune, out int rLen) == OperationStatus.Done)
                {
                    if (IsHiddenChar((char)rune.Value))
                    {
                        if (idx > 0)
                        {
                            fs.Spans.Add(new Span
                            {
                                Text = new string(buffer.Slice(0, idx))

                            });
                            idx = 0;
                        }
                        fs.Spans.Add(new Span
                        {
                            Text = $"\\u{rune.Value:X4}",
                            TextColor = Colors.Red
                        });
                    }
                    else
                    {
                        rune.TryEncodeToUtf16(buffer.Slice(idx), out int written);
                        idx += written;
                    }
                    i += rLen;
                    continue;
                }

                i++;
            }
            if (idx > 0)
            {
                fs.Spans.Add(new Span
                {
                    Text = new string(buffer.Slice(0, idx))

                });

            }
            return fs;
        }
        finally
        {
            if (pool is not null)
            {
                ArrayPool<char>.Shared.Return(pool);
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
            (0x0020, 0x007E), (0x00A0, 0x00FF),
            
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
