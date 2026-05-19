using System.Buffers.Binary;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Infrastructure.Glyphs;

public sealed class OpenTypeGlyphCatalogService : IGlyphCatalogService
{
    private const string AllBlocks = "全部区块";

    private static readonly string[] MacintoshStandardGlyphNames = ("""
.notdef .null nonmarkingreturn space exclam quotedbl numbersign dollar percent ampersand quotesingle parenleft parenright asterisk plus comma hyphen period slash zero one two three four five six seven eight nine colon semicolon less equal greater question at A B C D E F G H I J K L M N O P Q R S T U V W X Y Z bracketleft backslash bracketright asciicircum underscore grave a b c d e f g h i j k l m n o p q r s t u v w x y z braceleft bar braceright asciitilde Adieresis Aring Ccedilla Eacute Ntilde Odieresis Udieresis aacute agrave acircumflex adieresis atilde aring ccedilla eacute egrave ecircumflex edieresis iacute igrave icircumflex idieresis ntilde oacute ograve ocircumflex odieresis otilde uacute ugrave ucircumflex udieresis dagger degree cent sterling section bullet paragraph germandbls registered copyright trademark acute dieresis notequal AE Oslash infinity plusminus lessequal greaterequal yen mu partialdiff summation product pi integral ordfeminine ordmasculine Omega ae oslash questiondown exclamdown logicalnot radical florin approxequal Delta guillemotleft guillemotright ellipsis nonbreakingspace Agrave Atilde Otilde OE oe endash emdash quotedblleft quotedblright quoteleft quoteright divide lozenge ydieresis Ydieresis fraction currency guilsinglleft guilsinglright fi fl daggerdbl periodcentered quotesinglbase quotedblbase perthousand Acircumflex Ecircumflex Aacute Edieresis Egrave Iacute Icircumflex Idieresis Igrave Oacute Ocircumflex apple Ograve Uacute Ucircumflex Ugrave dotlessi circumflex tilde macron breve dotaccent ring cedilla hungarumlaut ogonek caron Lslash lslash Scaron scaron Zcaron zcaron brokenbar Eth eth Yacute yacute Thorn thorn minus multiply onesuperior twosuperior threesuperior onehalf onequarter threequarters franc Gbreve gbreve Idotaccent Scedilla scedilla Cacute cacute Ccaron ccaron dcroat
""").Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static readonly UnicodeBlockOption[] KnownBlocks =
    [
        new("Basic Latin", 0x0000, 0x007F),
        new("Latin-1 Supplement", 0x0080, 0x00FF),
        new("Latin Extended", 0x0100, 0x024F),
        new("Greek and Coptic", 0x0370, 0x03FF),
        new("Cyrillic", 0x0400, 0x04FF),
        new("General Punctuation", 0x2000, 0x206F),
        new("Currency Symbols", 0x20A0, 0x20CF),
        new("Letterlike Symbols", 0x2100, 0x214F),
        new("Number Forms", 0x2150, 0x218F),
        new("Arrows", 0x2190, 0x21FF),
        new("Mathematical Operators", 0x2200, 0x22FF),
        new("CJK Symbols and Punctuation", 0x3000, 0x303F),
        new("Hiragana", 0x3040, 0x309F),
        new("Katakana", 0x30A0, 0x30FF),
        new("CJK Unified Ideographs", 0x4E00, 0x9FFF),
        new("Private Use Area", 0xE000, 0xF8FF),
        new("CJK Compatibility Ideographs", 0xF900, 0xFAFF)
    ];

    public async Task<GlyphPage> GetGlyphsAsync(GlyphQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.FontFilePath) || !File.Exists(query.FontFilePath))
        {
            return Empty(query, "当前字体没有可读取的本地文件路径。");
        }

        var bytes = await File.ReadAllBytesAsync(query.FontFilePath, cancellationToken).ConfigureAwait(false);
        var fontOffset = GetFirstFontOffset(bytes);
        var tables = ReadTableDirectory(bytes, fontOffset);
        if (!tables.TryGetValue("cmap", out var cmap))
        {
            return Empty(query, "当前字体缺少 Unicode cmap 表。");
        }

        var names = tables.TryGetValue("post", out var post) ? ReadPostNames(bytes, post.Offset, post.Length) : [];
        var glyphs = ReadUnicodeMappings(bytes, cmap.Offset, cmap.Length, names, query.FaceName);
        var blocks = BuildBlockOptions(glyphs);
        glyphs = ApplyBlock(glyphs, query.UnicodeBlockName);
        glyphs = ApplySearch(glyphs, query.SearchText);

        var pageSize = Math.Clamp(query.PageSize, 24, 240);
        var pageNumber = Math.Max(1, query.PageNumber);
        var page = glyphs
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        var emptyMessage = glyphs.Count == 0 ? "当前字体不包含该字符/码位。" : "";
        return new GlyphPage(page, blocks, pageNumber, pageSize, glyphs.Count, emptyMessage);
    }

    private static GlyphPage Empty(GlyphQuery query, string message) =>
        new([], [new UnicodeBlockOption(AllBlocks, 0, 0)], Math.Max(1, query.PageNumber), Math.Clamp(query.PageSize, 24, 240), 0, message);

    private static List<GlyphRecord> ApplyBlock(List<GlyphRecord> glyphs, string blockName)
    {
        if (string.IsNullOrWhiteSpace(blockName) || string.Equals(blockName, AllBlocks, StringComparison.CurrentCultureIgnoreCase))
        {
            return glyphs;
        }

        var block = KnownBlocks.FirstOrDefault(candidate => string.Equals(candidate.Name, blockName, StringComparison.CurrentCultureIgnoreCase));
        return block is null
            ? glyphs
            : glyphs.Where(glyph => glyph.CodePoint >= block.Start && glyph.CodePoint <= block.End).ToList();
    }

    private static List<GlyphRecord> ApplySearch(List<GlyphRecord> glyphs, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return glyphs;
        }

        var search = searchText.Trim();
        if (TryParseCodePoint(search, out var codePoint))
        {
            return glyphs.Where(glyph => glyph.CodePoint == codePoint).ToList();
        }

        var codePoints = search.EnumerateRunes().Select(rune => rune.Value).ToHashSet();
        if (codePoints.Count > 0 && search.Length <= 16)
        {
            return glyphs.Where(glyph => codePoints.Contains(glyph.CodePoint)).ToList();
        }

        return glyphs.Where(glyph => glyph.GlyphName.Contains(search, StringComparison.CurrentCultureIgnoreCase)).ToList();
    }

    private static bool TryParseCodePoint(string value, out int codePoint)
    {
        codePoint = 0;
        var normalized = value.Trim();
        if (normalized.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }
        else if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }
        else
        {
            return false;
        }

        return int.TryParse(normalized, System.Globalization.NumberStyles.HexNumber, null, out codePoint);
    }

    private static IReadOnlyList<UnicodeBlockOption> BuildBlockOptions(IReadOnlyList<GlyphRecord> glyphs)
    {
        var options = new List<UnicodeBlockOption> { new(AllBlocks, 0, 0, glyphs.Count) };
        options.AddRange(KnownBlocks
            .Select(block => block with { Count = glyphs.Count(glyph => glyph.CodePoint >= block.Start && glyph.CodePoint <= block.End) })
            .Where(block => block.Count > 0));
        return options;
    }

    private static List<GlyphRecord> ReadUnicodeMappings(ReadOnlySpan<byte> bytes, int cmapOffset, int cmapLength, IReadOnlyDictionary<int, string> names, string faceName)
    {
        if (cmapOffset < 0 || cmapOffset + cmapLength > bytes.Length || cmapLength < 4)
        {
            throw new InvalidOperationException("字体 cmap 表不完整。");
        }

        var subtableOffset = FindBestCmapSubtable(bytes, cmapOffset, cmapLength);
        if (subtableOffset < 0)
        {
            return [];
        }

        var format = ReadUInt16(bytes, subtableOffset);
        var records = format switch
        {
            4 => ReadFormat4(bytes, subtableOffset, names, faceName),
            12 => ReadFormat12(bytes, subtableOffset, names, faceName),
            _ => []
        };

        return records
            .Where(glyph => glyph.CodePoint is > 0 and <= 0x10FFFF && glyph.GlyphId > 0)
            .DistinctBy(glyph => glyph.CodePoint)
            .OrderBy(glyph => glyph.CodePoint)
            .ToList();
    }

    private static int FindBestCmapSubtable(ReadOnlySpan<byte> bytes, int cmapOffset, int cmapLength)
    {
        var tableCount = ReadUInt16(bytes, cmapOffset + 2);
        var bestOffset = -1;
        var bestScore = -1;
        for (var index = 0; index < tableCount; index++)
        {
            var recordOffset = cmapOffset + 4 + index * 8;
            if (recordOffset + 8 > cmapOffset + cmapLength)
            {
                break;
            }

            var platformId = ReadUInt16(bytes, recordOffset);
            var encodingId = ReadUInt16(bytes, recordOffset + 2);
            var offset = checked((int)ReadUInt32(bytes, recordOffset + 4));
            var subtableOffset = cmapOffset + offset;
            if (subtableOffset + 2 > bytes.Length)
            {
                continue;
            }

            var format = ReadUInt16(bytes, subtableOffset);
            var score = format switch
            {
                12 when platformId == 3 && encodingId == 10 => 5,
                12 => 4,
                4 when platformId == 3 => 3,
                4 => 2,
                _ => 0
            };
            if (score > bestScore)
            {
                bestScore = score;
                bestOffset = subtableOffset;
            }
        }

        return bestOffset;
    }

    private static List<GlyphRecord> ReadFormat12(ReadOnlySpan<byte> bytes, int offset, IReadOnlyDictionary<int, string> names, string faceName)
    {
        var length = checked((int)ReadUInt32(bytes, offset + 4));
        var groups = checked((int)ReadUInt32(bytes, offset + 12));
        var result = new List<GlyphRecord>();
        for (var index = 0; index < groups; index++)
        {
            var groupOffset = offset + 16 + index * 12;
            if (groupOffset + 12 > offset + length || groupOffset + 12 > bytes.Length)
            {
                break;
            }

            var start = checked((int)ReadUInt32(bytes, groupOffset));
            var end = checked((int)ReadUInt32(bytes, groupOffset + 4));
            var glyphStart = checked((int)ReadUInt32(bytes, groupOffset + 8));
            for (var codePoint = start; codePoint <= end && codePoint <= 0x10FFFF; codePoint++)
            {
                if (!IsValidScalar(codePoint))
                {
                    continue;
                }

                var glyphId = glyphStart + codePoint - start;
                result.Add(CreateGlyph(codePoint, glyphId, names, faceName));
            }
        }

        return result;
    }

    private static List<GlyphRecord> ReadFormat4(ReadOnlySpan<byte> bytes, int offset, IReadOnlyDictionary<int, string> names, string faceName)
    {
        var length = ReadUInt16(bytes, offset + 2);
        var segCount = ReadUInt16(bytes, offset + 6) / 2;
        var endCodes = offset + 14;
        var startCodes = endCodes + segCount * 2 + 2;
        var idDeltas = startCodes + segCount * 2;
        var idRangeOffsets = idDeltas + segCount * 2;
        var result = new List<GlyphRecord>();

        for (var segment = 0; segment < segCount; segment++)
        {
            var end = ReadUInt16(bytes, endCodes + segment * 2);
            var start = ReadUInt16(bytes, startCodes + segment * 2);
            var delta = unchecked((short)ReadUInt16(bytes, idDeltas + segment * 2));
            var rangeOffset = ReadUInt16(bytes, idRangeOffsets + segment * 2);
            if (start == 0xFFFF && end == 0xFFFF)
            {
                continue;
            }

            for (var codePoint = start; codePoint <= end; codePoint++)
            {
                var glyphId = 0;
                if (rangeOffset == 0)
                {
                    glyphId = (codePoint + delta) & 0xFFFF;
                }
                else
                {
                    var glyphOffset = idRangeOffsets + segment * 2 + rangeOffset + (codePoint - start) * 2;
                    if (glyphOffset + 2 > offset + length || glyphOffset + 2 > bytes.Length)
                    {
                        continue;
                    }

                    var glyphIndex = ReadUInt16(bytes, glyphOffset);
                    glyphId = glyphIndex == 0 ? 0 : (glyphIndex + delta) & 0xFFFF;
                }

                if (glyphId > 0)
                {
                    if (!IsValidScalar(codePoint))
                    {
                        continue;
                    }

                    result.Add(CreateGlyph(codePoint, glyphId, names, faceName));
                }
            }
        }

        return result;
    }

    private static GlyphRecord CreateGlyph(int codePoint, int glyphId, IReadOnlyDictionary<int, string> names, string faceName) =>
        new(char.ConvertFromUtf32(codePoint), codePoint, glyphId, names.TryGetValue(glyphId, out var name) ? name : $"uni{codePoint:X4}", faceName, true);

    private static bool IsValidScalar(int codePoint) =>
        codePoint is >= 0 and <= 0x10FFFF && (codePoint < 0xD800 || codePoint > 0xDFFF);

    private static Dictionary<int, string> ReadPostNames(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        var result = new Dictionary<int, string>();
        if (offset + 34 > bytes.Length || length < 34 || ReadUInt32(bytes, offset) != 0x00020000)
        {
            return result;
        }

        var glyphCount = ReadUInt16(bytes, offset + 32);
        var indexOffset = offset + 34;
        var stringOffset = indexOffset + glyphCount * 2;
        var customNames = new List<string>();
        while (stringOffset < offset + length && stringOffset < bytes.Length)
        {
            var count = bytes[stringOffset++];
            if (count == 0 || stringOffset + count > bytes.Length)
            {
                break;
            }

            customNames.Add(System.Text.Encoding.ASCII.GetString(bytes.Slice(stringOffset, count)));
            stringOffset += count;
        }

        for (var glyphId = 0; glyphId < glyphCount; glyphId++)
        {
            var nameIndex = ReadUInt16(bytes, indexOffset + glyphId * 2);
            if (nameIndex < MacintoshStandardGlyphNames.Length)
            {
                result[glyphId] = MacintoshStandardGlyphNames[nameIndex];
            }
            else if (nameIndex >= 258)
            {
                var customIndex = nameIndex - 258;
                if (customIndex >= 0 && customIndex < customNames.Count)
                {
                    result[glyphId] = customNames[customIndex];
                }
            }
        }

        return result;
    }

    private static int GetFirstFontOffset(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12 || ReadUInt32(bytes, 0) != 0x74746366)
        {
            return 0;
        }

        return checked((int)ReadUInt32(bytes, 12));
    }

    private static Dictionary<string, TableRecord> ReadTableDirectory(ReadOnlySpan<byte> bytes, int fontOffset)
    {
        var tableCount = ReadUInt16(bytes, fontOffset + 4);
        var result = new Dictionary<string, TableRecord>(StringComparer.Ordinal);
        for (var index = 0; index < tableCount; index++)
        {
            var recordOffset = fontOffset + 12 + index * 16;
            if (recordOffset + 16 > bytes.Length)
            {
                break;
            }

            var tag = System.Text.Encoding.ASCII.GetString(bytes.Slice(recordOffset, 4));
            var offset = checked((int)ReadUInt32(bytes, recordOffset + 8));
            var length = checked((int)ReadUInt32(bytes, recordOffset + 12));
            result[tag] = new TableRecord(offset, length);
        }

        return result;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));

    private sealed record TableRecord(int Offset, int Length);
}
