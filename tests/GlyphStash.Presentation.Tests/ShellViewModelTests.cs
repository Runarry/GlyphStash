using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;
using GlyphStash.Presentation.ViewModels;

namespace GlyphStash.Presentation.Tests;

public sealed class ShellViewModelTests
{
    [Fact]
    public async Task Initialize_LoadsCachedFontsAndFiltersBySearch()
    {
        var store = new FakeStore([CreateFont("Inter"), CreateFont("Cascadia Code")]);
        var vm = new ShellViewModel(new FontLibraryService(new FakeInventory([]), store));

        await vm.InitializeAsync();
        vm.SearchText = "Cascadia";

        if (vm.Fonts.Count != 1 || vm.SelectedFont?.FamilyName != "Cascadia Code")
        {
            throw new InvalidOperationException("Expected search to keep only Cascadia Code selected.");
        }
    }

    [Fact]
    public async Task PreviewSettings_UpdateFontItems()
    {
        var store = new FakeStore([CreateFont("Inter")]);
        var vm = new ShellViewModel(new FontLibraryService(new FakeInventory([]), store));

        await vm.InitializeAsync();
        vm.PreviewText = "Hello 字体";
        vm.PreviewFontSize = 42;

        if (vm.Fonts[0].PreviewText != "Hello 字体" || Math.Abs(vm.Fonts[0].PreviewFontSize - 42) > 0.01)
        {
            throw new InvalidOperationException("Expected preview settings to propagate to visible font items.");
        }
    }

    [Fact]
    public async Task Initialize_DefaultsToFontLibraryNavigation()
    {
        var vm = new ShellViewModel(new FontLibraryService(new FakeInventory([]), new FakeStore([CreateFont("Inter")])));

        await vm.InitializeAsync();

        if (!vm.IsFontLibraryPage || vm.SelectedNavigationItem?.Key != "font-library")
        {
            throw new InvalidOperationException("Expected font library to be the default page.");
        }
    }

    [Fact]
    public async Task Navigation_CanSwitchToComponentDemoAndPlaceholder()
    {
        var vm = new ShellViewModel(new FontLibraryService(new FakeInventory([]), new FakeStore([CreateFont("Inter")])));
        await vm.InitializeAsync();

        vm.SelectedNavigationItem = vm.NavigationItems.Single(item => item.Key == "component-demo");
        if (!vm.IsComponentDemoPage)
        {
            throw new InvalidOperationException("Expected component demo page to become active.");
        }

        vm.SelectedNavigationItem = vm.NavigationItems.Single(item => item.Key == "collections");
        if (!vm.IsPlaceholderPage || vm.CurrentPageMilestone != "M2")
        {
            throw new InvalidOperationException("Expected non-M1 pages to use the milestone placeholder page.");
        }
    }

    [Fact]
    public async Task ScanFailure_ShowsRootErrorMessage()
    {
        var vm = new ShellViewModel(new FontLibraryService(new ThrowingInventory(), new FakeStore([])));

        await vm.InitializeAsync();

        if (!vm.ScanStatus.Contains("fixture scan failed", StringComparison.Ordinal) ||
            !vm.StatusMessage.Contains("fixture scan failed", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected scan failure to expose the root error message.");
        }
    }

    private static FontFamilyRecord CreateFont(string family)
    {
        var file = new FontFileRecord($"C:/Fonts/{family}.ttf", "TTF", null, FontSourceKind.System, DateTimeOffset.UtcNow);
        return new FontFamilyRecord(
            family,
            [new FontFaceRecord(family, "Regular", $"{family} Regular", $"{family}-Regular", 400, "Normal", "Normal", file)],
            FontSourceKind.System,
            FontActivationState.Installed,
            LicenseStatus.Unknown,
            "未知授权",
            ["无衬线"],
            [],
            false);
    }

    private sealed class FakeInventory : IFontInventoryService
    {
        private readonly IReadOnlyList<FontFamilyRecord> _fonts;

        public FakeInventory(IReadOnlyList<FontFamilyRecord> fonts)
        {
            _fonts = fonts;
        }

        public Task<IReadOnlyList<FontFamilyRecord>> ScanInstalledFontsAsync(CancellationToken cancellationToken) => Task.FromResult(_fonts);
    }

    private sealed class ThrowingInventory : IFontInventoryService
    {
        public Task<IReadOnlyList<FontFamilyRecord>> ScanInstalledFontsAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("fixture scan failed");
    }

    private sealed class FakeStore : IFontMetadataStore
    {
        private IReadOnlyList<FontFamilyRecord> _fonts;

        public FakeStore(IReadOnlyList<FontFamilyRecord> fonts)
        {
            _fonts = fonts;
        }

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveFontIndexAsync(IReadOnlyList<FontFamilyRecord> fonts, CancellationToken cancellationToken)
        {
            _fonts = fonts;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FontFamilyRecord>> SearchAsync(FontSearchQuery query, CancellationToken cancellationToken) => Task.FromResult(_fonts);
    }
}
