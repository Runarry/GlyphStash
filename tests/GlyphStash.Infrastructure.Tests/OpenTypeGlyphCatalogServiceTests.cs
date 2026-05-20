using GlyphStash.Domain.Fonts;
using GlyphStash.Infrastructure.Glyphs;

namespace GlyphStash.Infrastructure.Tests;

public sealed class OpenTypeGlyphCatalogServiceTests
{
    [Fact]
    public async Task GetGlyphsAsync_SupportsUnicodeSearchAndPaging()
    {
        var path = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.ttf");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, CreateFormat4Font(), CancellationToken.None);
        var service = new OpenTypeGlyphCatalogService();

        var page = await service.GetGlyphsAsync(new GlyphQuery(path, "Regular", "U+4F60"), CancellationToken.None);

        Assert.Single(page.Glyphs);
        Assert.Equal("你", page.Glyphs[0].Character);
        Assert.Equal("U+4F60", page.Glyphs[0].UnicodeLabel);
        Assert.Equal("uni4F60", page.Glyphs[0].GlyphName);
    }

    [Fact]
    public async Task GetGlyphsAsync_FiltersByUnicodeBlock()
    {
        var path = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.ttf");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, CreateFormat4Font(), CancellationToken.None);
        var service = new OpenTypeGlyphCatalogService();

        var page = await service.GetGlyphsAsync(new GlyphQuery(path, "Regular", UnicodeBlockName: "Basic Latin"), CancellationToken.None);

        Assert.Single(page.Glyphs);
        Assert.Equal("A", page.Glyphs[0].Character);
    }

    [Fact]
    public async Task GetGlyphsAsync_SupportsFormat12SupplementaryPlane()
    {
        var path = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.ttf");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, CreateFormat12Font(0x10000, 1, 7), CancellationToken.None);
        var service = new OpenTypeGlyphCatalogService();

        var page = await service.GetGlyphsAsync(new GlyphQuery(path, "Regular", "U+10000"), CancellationToken.None);

        Assert.Single(page.Glyphs);
        Assert.Equal(char.ConvertFromUtf32(0x10000), page.Glyphs[0].Character);
        Assert.Equal("U+10000", page.Glyphs[0].UnicodeLabel);
        Assert.Equal(7, page.Glyphs[0].GlyphId);
    }

    [Fact]
    public async Task GetGlyphsAsync_UsesPostStandardGlyphNames()
    {
        var path = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.ttf");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, CreateFormat4FontWithPostNames(), CancellationToken.None);
        var service = new OpenTypeGlyphCatalogService();

        var page = await service.GetGlyphsAsync(new GlyphQuery(path, "Regular", "U+0041"), CancellationToken.None);

        Assert.Single(page.Glyphs);
        Assert.Equal("A", page.Glyphs[0].GlyphName);
    }

    [Fact]
    public async Task GetGlyphsAsync_PagesLargeFormat12Fonts()
    {
        var path = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.ttf");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, CreateFormat12Font(0x10000, 10_000, 1), CancellationToken.None);
        var service = new OpenTypeGlyphCatalogService();

        var page = await service.GetGlyphsAsync(new GlyphQuery(path, "Regular", PageNumber: 2, PageSize: 120), CancellationToken.None);

        Assert.Equal(120, page.Glyphs.Count);
        Assert.Equal(10_000, page.TotalCount);
        Assert.Equal(84, page.TotalPages);
        Assert.Equal(0x10000 + 120, page.Glyphs[0].CodePoint);
    }

    [Fact]
    public async Task GetCoverageAsync_ReturnsContinuousRangesAndBlockCounts()
    {
        var path = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.ttf");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, CreateFormat4Font(), CancellationToken.None);
        var service = new OpenTypeGlyphCatalogService();

        var coverage = await service.GetCoverageAsync(new GlyphCoverageQuery(path, "Regular"), CancellationToken.None);

        Assert.Equal(2, coverage.TotalCodePointCount);
        Assert.Contains(coverage.Ranges, range => range.Label == "U+0041");
        Assert.Contains(coverage.Ranges, range => range.Label == "U+4F60");
        Assert.Contains(coverage.Blocks, block => block.Name == "Basic Latin" && block.Count == 1);
        Assert.Contains(coverage.Blocks, block => block.Name == "CJK Unified Ideographs" && block.Count == 1);
    }

    [Fact]
    public async Task GetCoverageAsync_DoesNotHideOtherCoverage()
    {
        var path = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.ttf");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, CreateFormat12Font(0x1F600, 3, 9), CancellationToken.None);
        var service = new OpenTypeGlyphCatalogService();

        var coverage = await service.GetCoverageAsync(new GlyphCoverageQuery(path, "Regular"), CancellationToken.None);

        Assert.Single(coverage.Ranges);
        Assert.Equal("U+1F600-U+1F602", coverage.Ranges[0].Label);
        Assert.Contains(coverage.Blocks, block => block.Name == UnicodeCoverageBlocks.OtherCoverage && block.Count == 3 && block.IsOther);
        Assert.Contains(coverage.Segments, segment => segment.BlockName == UnicodeCoverageBlocks.OtherCoverage && segment.Range.Label == "U+1F600-U+1F602");
    }

    private static byte[] CreateFormat4Font()
    {
        return CreateFont(("cmap", CreateFormat4Cmap()));
    }

    private static byte[] CreateFormat4FontWithPostNames()
    {
        return CreateFont(
            ("cmap", CreateFormat4Cmap()),
            ("post", CreatePostTable([0, 36, 258], ["customTwo"])));
    }

    private static byte[] CreateFormat4Cmap()
    {
        var cmap = new byte[52];
        WriteUInt16(cmap, 0, 0);
        WriteUInt16(cmap, 2, 1);
        WriteUInt16(cmap, 4, 3);
        WriteUInt16(cmap, 6, 1);
        WriteUInt32(cmap, 8, 12);
        var offset = 12;
        WriteUInt16(cmap, offset, 4);
        WriteUInt16(cmap, offset + 2, 40);
        WriteUInt16(cmap, offset + 4, 0);
        WriteUInt16(cmap, offset + 6, 6);
        WriteUInt16(cmap, offset + 8, 4);
        WriteUInt16(cmap, offset + 10, 1);
        WriteUInt16(cmap, offset + 12, 0);
        WriteUInt16(cmap, offset + 14, 0x0041);
        WriteUInt16(cmap, offset + 16, 0x4F60);
        WriteUInt16(cmap, offset + 18, 0xFFFF);
        WriteUInt16(cmap, offset + 20, 0);
        WriteUInt16(cmap, offset + 22, 0x0041);
        WriteUInt16(cmap, offset + 24, 0x4F60);
        WriteUInt16(cmap, offset + 26, 0xFFFF);
        WriteUInt16(cmap, offset + 28, unchecked((ushort)(1 - 0x0041)));
        WriteUInt16(cmap, offset + 30, unchecked((ushort)(2 - 0x4F60)));
        WriteUInt16(cmap, offset + 32, 1);
        WriteUInt16(cmap, offset + 34, 0);
        WriteUInt16(cmap, offset + 36, 0);
        WriteUInt16(cmap, offset + 38, 0);

        return cmap;
    }

    private static byte[] CreateFormat12Font(int startCodePoint, int count, int glyphStart)
    {
        var cmap = new byte[40];
        WriteUInt16(cmap, 0, 0);
        WriteUInt16(cmap, 2, 1);
        WriteUInt16(cmap, 4, 3);
        WriteUInt16(cmap, 6, 10);
        WriteUInt32(cmap, 8, 12);
        var offset = 12;
        WriteUInt16(cmap, offset, 12);
        WriteUInt16(cmap, offset + 2, 0);
        WriteUInt32(cmap, offset + 4, 28);
        WriteUInt32(cmap, offset + 8, 0);
        WriteUInt32(cmap, offset + 12, 1);
        WriteUInt32(cmap, offset + 16, (uint)startCodePoint);
        WriteUInt32(cmap, offset + 20, (uint)(startCodePoint + count - 1));
        WriteUInt32(cmap, offset + 24, (uint)glyphStart);
        return CreateFont(("cmap", cmap));
    }

    private static byte[] CreatePostTable(IReadOnlyList<int> nameIndexes, IReadOnlyList<string> customNames)
    {
        var customLength = customNames.Sum(name => 1 + name.Length);
        var post = new byte[34 + nameIndexes.Count * 2 + customLength];
        WriteUInt32(post, 0, 0x00020000);
        WriteUInt16(post, 32, nameIndexes.Count);
        var indexOffset = 34;
        for (var index = 0; index < nameIndexes.Count; index++)
        {
            WriteUInt16(post, indexOffset + index * 2, nameIndexes[index]);
        }

        var stringOffset = indexOffset + nameIndexes.Count * 2;
        foreach (var name in customNames)
        {
            post[stringOffset++] = (byte)name.Length;
            for (var index = 0; index < name.Length; index++)
            {
                post[stringOffset++] = (byte)name[index];
            }
        }

        return post;
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
