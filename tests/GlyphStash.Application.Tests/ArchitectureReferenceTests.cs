using System.Xml.Linq;

namespace GlyphStash.Application.Tests;

public sealed class ArchitectureReferenceTests
{
    [Fact]
    public void Application_ReferencesOnlyDomainAndLocalization()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "GlyphStash.Application", "GlyphStash.Application.csproj"));
        var doc = XDocument.Load(projectPath);
        var references = doc.Descendants("ProjectReference").Select(node => node.Attribute("Include")?.Value).Where(value => value is not null).ToArray();

        Assert.Equal(2, references.Length);
        Assert.Contains(references, reference => reference!.Contains("GlyphStash.Domain", StringComparison.Ordinal));
        Assert.Contains(references, reference => reference!.Contains("GlyphStash.Localization", StringComparison.Ordinal));
    }
}
