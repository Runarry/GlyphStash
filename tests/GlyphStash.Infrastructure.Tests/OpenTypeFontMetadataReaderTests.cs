using GlyphStash.Infrastructure.Fonts;

namespace GlyphStash.Infrastructure.Tests;

public sealed class OpenTypeFontMetadataReaderTests
{
    [Fact]
    public async Task ReadMetadataAsync_ReadsOs2WeightWidthAndSlant()
    {
        var path = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.ttf");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(
            path,
            CreateFont(
                ("name", CreateNameTable(
                    (1, "Noto Sans"),
                    (2, "Bold Italic"),
                    (4, "Noto Sans Bold Italic"),
                    (6, "NotoSans-BoldItalic"))),
                ("OS/2", CreateOs2Table(700, 5, italic: true))),
            CancellationToken.None);
        var reader = new OpenTypeFontMetadataReader();

        var metadata = await reader.ReadMetadataAsync(path, CancellationToken.None);

        Assert.Equal("Noto Sans", metadata.FamilyName);
        Assert.Equal("Bold Italic", metadata.SubfamilyName);
        Assert.Equal(700, metadata.Weight);
        Assert.Equal("Normal", metadata.Width);
        Assert.Equal("Italic", metadata.Slant);
    }

    private static byte[] CreateNameTable(params (int NameId, string Value)[] names)
    {
        var encodedNames = names
            .Select(name => (name.NameId, Bytes: EncodeUtf16Be(name.Value)))
            .ToList();
        var stringOffset = 6 + encodedNames.Count * 12;
        var table = new byte[stringOffset + encodedNames.Sum(name => name.Bytes.Length)];
        WriteUInt16(table, 0, 0);
        WriteUInt16(table, 2, encodedNames.Count);
        WriteUInt16(table, 4, stringOffset);

        var dataOffset = 0;
        for (var index = 0; index < encodedNames.Count; index++)
        {
            var recordOffset = 6 + index * 12;
            WriteUInt16(table, recordOffset, 3);
            WriteUInt16(table, recordOffset + 2, 1);
            WriteUInt16(table, recordOffset + 4, 0x0409);
            WriteUInt16(table, recordOffset + 6, encodedNames[index].NameId);
            WriteUInt16(table, recordOffset + 8, encodedNames[index].Bytes.Length);
            WriteUInt16(table, recordOffset + 10, dataOffset);
            encodedNames[index].Bytes.CopyTo(table, stringOffset + dataOffset);
            dataOffset += encodedNames[index].Bytes.Length;
        }

        return table;
    }

    private static byte[] CreateOs2Table(int weight, int width, bool italic)
    {
        var table = new byte[64];
        WriteUInt16(table, 0, 4);
        WriteUInt16(table, 4, weight);
        WriteUInt16(table, 6, width);
        WriteUInt16(table, 62, italic ? 1 : 0);
        return table;
    }

    private static byte[] CreateFont(params (string Tag, byte[] Data)[] tables)
    {
        var directoryLength = 12 + tables.Length * 16;
        var totalLength = directoryLength + tables.Sum(table => table.Data.Length);
        var font = new byte[totalLength];
        WriteUInt32(font, 0, 0x00010000);
        WriteUInt16(font, 4, tables.Length);
        var dataOffset = directoryLength;
        for (var index = 0; index < tables.Length; index++)
        {
            var recordOffset = 12 + index * 16;
            WriteAscii(font, recordOffset, tables[index].Tag);
            WriteUInt32(font, recordOffset + 8, (uint)dataOffset);
            WriteUInt32(font, recordOffset + 12, (uint)tables[index].Data.Length);
            tables[index].Data.CopyTo(font, dataOffset);
            dataOffset += tables[index].Data.Length;
        }

        return font;
    }

    private static byte[] EncodeUtf16Be(string value)
    {
        var bytes = new byte[value.Length * 2];
        for (var index = 0; index < value.Length; index++)
        {
            bytes[index * 2] = (byte)(value[index] >> 8);
            bytes[index * 2 + 1] = (byte)value[index];
        }

        return bytes;
    }

    private static void WriteAscii(byte[] bytes, int offset, string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            bytes[offset + index] = (byte)value[index];
        }
    }

    private static void WriteUInt16(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)((value >> 8) & 0xFF);
        bytes[offset + 1] = (byte)(value & 0xFF);
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)((value >> 24) & 0xFF);
        bytes[offset + 1] = (byte)((value >> 16) & 0xFF);
        bytes[offset + 2] = (byte)((value >> 8) & 0xFF);
        bytes[offset + 3] = (byte)(value & 0xFF);
    }
}
