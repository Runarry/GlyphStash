using System.Drawing.Text;
using System.Runtime.Versioning;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Platform.Windows.Fonts;

[SupportedOSPlatform("windows")]
public sealed class WindowsFontInventoryService : IFontInventoryService
{
    private static readonly string[] SupportedExtensions = [".ttf", ".otf", ".ttc", ".otc", ".woff", ".woff2"];

    public Task<IReadOnlyList<FontFamilyRecord>> ScanInstalledFontsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var files = EnumerateFontFiles(now);
        var installedFamilyNames = GetInstalledFamilyNames();
        var grouped = new Dictionary<string, List<FontFaceRecord>>(StringComparer.CurrentCultureIgnoreCase);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var familyName = GuessFamilyName(file.Path);
            if (string.IsNullOrWhiteSpace(familyName))
            {
                continue;
            }

            var subfamily = GuessSubfamilyName(file.Path);
            var face = new FontFaceRecord(
                familyName,
                subfamily,
                $"{familyName} {subfamily}".Trim(),
                $"{familyName.Replace(' ', '-')}-{subfamily.Replace(' ', '-')}",
                GuessWeight(subfamily),
                "Normal",
                subfamily.Contains("Italic", StringComparison.OrdinalIgnoreCase) ? "Italic" : "Normal",
                file);

            if (!grouped.TryGetValue(familyName, out var faces))
            {
                faces = [];
                grouped.Add(familyName, faces);
            }

            faces.Add(face);
        }

        foreach (var familyName in installedFamilyNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (grouped.ContainsKey(familyName))
            {
                continue;
            }

            var file = new FontFileRecord(CreateInstalledFontUri(familyName), "Installed", null, FontSourceKind.System, now);
            grouped[familyName] =
            [
                new FontFaceRecord(familyName, "Regular", familyName, familyName.Replace(' ', '-'), 400, "Normal", "Normal", file)
            ];
        }

        var records = grouped
            .Select(pair =>
            {
                var source = pair.Value.Select(face => face.File.SourceKind).DefaultIfEmpty(FontSourceKind.Unknown).Min();
                return new FontFamilyRecord(
                    pair.Key,
                    pair.Value.OrderBy(face => face.Weight).ThenBy(face => face.SubfamilyName).ToList(),
                    source,
                    FontActivationState.Installed,
                    LicenseStatus.Unknown,
                    "未知授权",
                    InferTags(pair.Key),
                    Array.Empty<string>(),
                    false);
            })
            .OrderBy(font => font.FamilyName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<FontFamilyRecord>>(records);
    }

    private static IReadOnlyList<FontFileRecord> EnumerateFontFiles(DateTimeOffset now)
    {
        var roots = new[]
        {
            new { Path = Environment.GetFolderPath(Environment.SpecialFolder.Fonts), Source = FontSourceKind.System },
            new { Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts"), Source = FontSourceKind.UserInstalled }
        };

        var result = new List<FontFileRecord>();
        foreach (var root in roots)
        {
            if (!Directory.Exists(root.Path))
            {
                continue;
            }

            try
            {
                foreach (var path in Directory.EnumerateFiles(root.Path, "*.*", SearchOption.TopDirectoryOnly))
                {
                    var extension = Path.GetExtension(path);
                    if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    result.Add(new FontFileRecord(path, extension.TrimStart('.').ToUpperInvariant(), null, root.Source, now));
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return result;
    }

    private static IReadOnlySet<string> GetInstalledFamilyNames()
    {
        try
        {
            using var collection = new InstalledFontCollection();
            return collection.Families.Select(family => family.Name).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        }
    }

    private static string GuessFamilyName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var tokens = name.Split(['-', '_'], 2, StringSplitOptions.TrimEntries);
        return tokens[0].Replace('_', ' ');
    }

    private static string CreateInstalledFontUri(string familyName)
    {
        var escaped = Uri.EscapeDataString(familyName);
        return $"installed://{escaped}";
    }

    private static string GuessSubfamilyName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var tokens = name.Split(['-', '_'], 2, StringSplitOptions.TrimEntries);
        return tokens.Length > 1 ? tokens[1].Replace('_', ' ') : "Regular";
    }

    private static int GuessWeight(string subfamily)
    {
        if (subfamily.Contains("Black", StringComparison.OrdinalIgnoreCase)) return 900;
        if (subfamily.Contains("ExtraBold", StringComparison.OrdinalIgnoreCase)) return 800;
        if (subfamily.Contains("Bold", StringComparison.OrdinalIgnoreCase)) return 700;
        if (subfamily.Contains("SemiBold", StringComparison.OrdinalIgnoreCase)) return 600;
        if (subfamily.Contains("Medium", StringComparison.OrdinalIgnoreCase)) return 500;
        if (subfamily.Contains("Light", StringComparison.OrdinalIgnoreCase)) return 300;
        if (subfamily.Contains("Thin", StringComparison.OrdinalIgnoreCase)) return 100;
        return 400;
    }

    private static IReadOnlyList<string> InferTags(string familyName)
    {
        var tags = new List<string>();
        if (familyName.Contains("Mono", StringComparison.OrdinalIgnoreCase) || familyName.Contains("Code", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("等宽");
        }

        if (familyName.Contains("Serif", StringComparison.OrdinalIgnoreCase) || familyName.Contains("Song", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("衬线");
        }
        else
        {
            tags.Add("无衬线");
        }

        if (familyName.Contains("CJK", StringComparison.OrdinalIgnoreCase) || familyName.Contains("Hei", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("中文");
        }

        return tags;
    }
}
