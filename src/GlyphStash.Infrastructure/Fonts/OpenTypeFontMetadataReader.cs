using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Fonts;

namespace GlyphStash.Infrastructure.Fonts;

public sealed class OpenTypeFontMetadataReader : IFontMetadataReader
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf",
        ".otf",
        ".ttc",
        ".otc"
    };

    public async Task<FontMetadata> ReadMetadataAsync(string fontFilePath, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fontFilePath);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("M2 仅支持 TTF、OTF、TTC、OTC 的本地安装和临时启用。");
        }

        var bytes = await File.ReadAllBytesAsync(fontFilePath, cancellationToken).ConfigureAwait(false);
        if (bytes.Length < 12)
        {
            throw new InvalidOperationException("字体文件过小或已损坏。");
        }

        var fontOffset = GetFirstFontOffset(bytes);
        var names = ReadNameRecords(bytes, fontOffset);
        var fallbackFamily = GuessFamilyName(fontFilePath);
        var family = ValueOrFallback(names, 1, fallbackFamily);
        var subfamily = ValueOrFallback(names, 2, GuessSubfamilyName(fontFilePath));
        var fullName = ValueOrFallback(names, 4, $"{family} {subfamily}".Trim());
        var postScript = ValueOrFallback(names, 6, $"{family.Replace(' ', '-')}-{subfamily.Replace(' ', '-')}");
        var style = ReadStyleMetrics(bytes, fontOffset, subfamily);
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        return new FontMetadata(
            fontFilePath,
            extension.TrimStart('.').ToUpperInvariant(),
            family,
            subfamily,
            fullName,
            postScript,
            names.TryGetValue(5, out var version) ? version : null,
            names.TryGetValue(8, out var manufacturer) ? manufacturer : null,
            names.TryGetValue(13, out var license) ? license : null,
            sha256,
            style.Weight,
            style.Width,
            style.Slant);
    }

    private static int GetFirstFontOffset(ReadOnlySpan<byte> bytes)
    {
        var signature = ReadUInt32(bytes, 0);
        if (signature != Tag("ttcf"))
        {
            return 0;
        }

        if (bytes.Length < 16)
        {
            throw new InvalidOperationException("TTC/OTC 文件头不完整。");
        }

        var count = ReadUInt32(bytes, 8);
        if (count == 0)
        {
            throw new InvalidOperationException("TTC/OTC 文件没有包含字体。");
        }

        var offset = checked((int)ReadUInt32(bytes, 12));
        if (offset < 0 || offset + 12 > bytes.Length)
        {
            throw new InvalidOperationException("TTC/OTC 字体偏移无效。");
        }

        return offset;
    }

    private static Dictionary<int, string> ReadNameRecords(ReadOnlySpan<byte> bytes, int fontOffset)
    {
        if (fontOffset + 12 > bytes.Length)
        {
            throw new InvalidOperationException("OpenType 字体表头不完整。");
        }

        if (!TryReadTable(bytes, fontOffset, "name", out var nameOffset, out var nameLength))
        {
            throw new InvalidOperationException("字体缺少有效 name 表。");
        }

        var count = ReadUInt16(bytes, nameOffset + 2);
        var stringOffset = ReadUInt16(bytes, nameOffset + 4);
        var best = new Dictionary<int, (int Score, string Value)>();
        for (var i = 0; i < count; i++)
        {
            var recordOffset = nameOffset + 6 + i * 12;
            if (recordOffset + 12 > bytes.Length)
            {
                break;
            }

            var platformId = ReadUInt16(bytes, recordOffset);
            var encodingId = ReadUInt16(bytes, recordOffset + 2);
            var languageId = ReadUInt16(bytes, recordOffset + 4);
            var nameId = ReadUInt16(bytes, recordOffset + 6);
            var length = ReadUInt16(bytes, recordOffset + 8);
            var offset = ReadUInt16(bytes, recordOffset + 10);
            var dataOffset = nameOffset + stringOffset + offset;
            if (length == 0 || dataOffset + length > bytes.Length)
            {
                continue;
            }

            var value = DecodeName(bytes.Slice(dataOffset, length), platformId, encodingId).Trim('\0', ' ', '\r', '\n', '\t');
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var score = ScoreName(platformId, languageId);
            if (!best.TryGetValue(nameId, out var current) || score > current.Score)
            {
                best[nameId] = (score, value);
            }
        }

        return best.ToDictionary(pair => pair.Key, pair => pair.Value.Value);
    }

    private static (int Weight, string Width, string Slant) ReadStyleMetrics(ReadOnlySpan<byte> bytes, int fontOffset, string subfamily)
    {
        var weight = GuessWeight(subfamily);
        var width = "Normal";
        var slant = subfamily.Contains("Italic", StringComparison.OrdinalIgnoreCase)
            ? "Italic"
            : "Normal";

        if (!TryReadTable(bytes, fontOffset, "OS/2", out var os2Offset, out var os2Length) || os2Length < 64)
        {
            return (weight, width, slant);
        }

        weight = ReadUInt16(bytes, os2Offset + 4);
        width = WidthName(ReadUInt16(bytes, os2Offset + 6));
        var fsSelection = ReadUInt16(bytes, os2Offset + 62);
        if ((fsSelection & 0x0001) != 0)
        {
            slant = "Italic";
        }

        return (weight, width, slant);
    }

    private static bool TryReadTable(ReadOnlySpan<byte> bytes, int fontOffset, string tag, out int offset, out int length)
    {
        offset = -1;
        length = 0;
        if (fontOffset + 12 > bytes.Length)
        {
            return false;
        }

        var tableCount = ReadUInt16(bytes, fontOffset + 4);
        var tableRecordsOffset = fontOffset + 12;
        for (var i = 0; i < tableCount; i++)
        {
            var recordOffset = tableRecordsOffset + i * 16;
            if (recordOffset + 16 > bytes.Length)
            {
                throw new InvalidOperationException("OpenType 表目录不完整。");
            }

            if (ReadUInt32(bytes, recordOffset) != Tag(tag))
            {
                continue;
            }

            offset = checked((int)ReadUInt32(bytes, recordOffset + 8));
            length = checked((int)ReadUInt32(bytes, recordOffset + 12));
            return offset >= 0 && offset + length <= bytes.Length;
        }

        return false;
    }

    private static string DecodeName(ReadOnlySpan<byte> bytes, ushort platformId, ushort encodingId)
    {
        if (platformId is 0 or 3 || encodingId is 1 or 10)
        {
            return Encoding.BigEndianUnicode.GetString(bytes);
        }

        return Encoding.Latin1.GetString(bytes);
    }

    private static int ScoreName(ushort platformId, ushort languageId)
    {
        if (platformId == 3 && languageId == 0x0409)
        {
            return 4;
        }

        if (platformId == 3)
        {
            return 3;
        }

        if (platformId == 0)
        {
            return 2;
        }

        return 1;
    }

    private static string ValueOrFallback(IReadOnlyDictionary<int, string> names, int id, string fallback) =>
        names.TryGetValue(id, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static string GuessFamilyName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.Split(['-', '_'], 2, StringSplitOptions.TrimEntries)[0].Replace('_', ' ');
    }

    private static string GuessSubfamilyName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var parts = name.Split(['-', '_'], 2, StringSplitOptions.TrimEntries);
        return parts.Length > 1 ? parts[1].Replace('_', ' ') : "Regular";
    }

    private static int GuessWeight(string subfamily)
    {
        var normalized = subfamily
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal);
        if (normalized.Contains("ExtraBlack", StringComparison.OrdinalIgnoreCase) || normalized.Contains("UltraBlack", StringComparison.OrdinalIgnoreCase)) return 950;
        if (normalized.Contains("Black", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Heavy", StringComparison.OrdinalIgnoreCase)) return 900;
        if (normalized.Contains("ExtraBold", StringComparison.OrdinalIgnoreCase) || normalized.Contains("UltraBold", StringComparison.OrdinalIgnoreCase)) return 800;
        if (normalized.Contains("Bold", StringComparison.OrdinalIgnoreCase)) return 700;
        if (normalized.Contains("SemiBold", StringComparison.OrdinalIgnoreCase) || normalized.Contains("DemiBold", StringComparison.OrdinalIgnoreCase)) return 600;
        if (normalized.Contains("Medium", StringComparison.OrdinalIgnoreCase)) return 500;
        if (normalized.Contains("ExtraLight", StringComparison.OrdinalIgnoreCase) || normalized.Contains("UltraLight", StringComparison.OrdinalIgnoreCase)) return 200;
        if (normalized.Contains("Light", StringComparison.OrdinalIgnoreCase)) return 300;
        if (normalized.Contains("Thin", StringComparison.OrdinalIgnoreCase)) return 100;
        return 400;
    }

    private static string WidthName(int widthClass) => widthClass switch
    {
        1 => "UltraCondensed",
        2 => "ExtraCondensed",
        3 => "Condensed",
        4 => "SemiCondensed",
        5 => "Normal",
        6 => "SemiExpanded",
        7 => "Expanded",
        8 => "ExtraExpanded",
        9 => "UltraExpanded",
        _ => "Normal"
    };

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));

    private static uint Tag(string value) => BinaryPrimitives.ReadUInt32BigEndian(Encoding.ASCII.GetBytes(value));
}
