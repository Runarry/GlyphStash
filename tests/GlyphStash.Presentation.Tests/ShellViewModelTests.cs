using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;
using GlyphStash.Presentation.Services;
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
    public async Task Navigation_CanSwitchToM2CollectionsComponentDemoAndPlaceholder()
    {
        var vm = new ShellViewModel(new FontLibraryService(new FakeInventory([]), new FakeStore([CreateFont("Inter")])));
        await vm.InitializeAsync();

        vm.SelectedNavigationItem = vm.NavigationItems.Single(item => item.Key == "component-demo");
        if (!vm.IsComponentDemoPage)
        {
            throw new InvalidOperationException("Expected component demo page to become active.");
        }

        vm.SelectedNavigationItem = vm.NavigationItems.Single(item => item.Key == "collections");
        if (!vm.IsCollectionsPage)
        {
            throw new InvalidOperationException("Expected collections page to be implemented in M2.");
        }

        vm.SelectedNavigationItem = vm.NavigationItems.Single(item => item.Key == "online-fonts");
        if (!vm.IsPlaceholderPage || vm.CurrentPageMilestone != "M3")
        {
            throw new InvalidOperationException("Expected post-M2 pages to use the milestone placeholder page.");
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

    [Fact]
    public void InstalledFont_DisablesTemporaryActivation()
    {
        var font = new FontFamilyItemViewModel(CreateFont("Installed Sans", FontActivationState.Installed, FontSourceKind.UserInstalled));

        Assert.False(font.CanTemporarilyActivate);
        Assert.Equal("已安装，无需临时启用", font.TemporaryActivationDisabledReason);
    }

    [Fact]
    public void TemporarilyEnabledFont_CanBeClosed()
    {
        var font = new FontFamilyItemViewModel(CreateFont("Temp Sans", FontActivationState.TemporarilyEnabled, FontSourceKind.GlyphStashManaged));

        Assert.True(font.CanTemporarilyActivate);
        Assert.Equal("关闭临时启用", font.TemporaryActivationLabel);
    }

    [Fact]
    public void ImportInstallForCurrentUser_DisablesTemporaryActivationOption()
    {
        var vm = new ShellViewModel(new FontLibraryService(new FakeInventory([]), new FakeStore([])));

        vm.ImportTemporarilyActivate = true;
        vm.ImportInstallForCurrentUser = true;

        Assert.False(vm.ImportTemporarilyActivate);
        Assert.False(vm.CanImportTemporarilyActivate);
        Assert.Equal("用户级安装后已安装，无需临时启用", vm.ImportTemporaryActivationReason);
    }

    [Fact]
    public async Task ActivateSelectedCollection_SkipsInstalledAndAlreadyEnabledFonts()
    {
        var fonts = new[]
        {
            CreateFont("Installed Sans", FontActivationState.Installed, FontSourceKind.UserInstalled),
            CreateFont("Managed Sans", FontActivationState.NotEnabled, FontSourceKind.GlyphStashManaged),
            CreateFont("Temp Sans", FontActivationState.TemporarilyEnabled, FontSourceKind.GlyphStashManaged)
        };
        var platform = new FakePlatformActivation();
        var service = CreateLocalService(
            platform,
            [new FontCollectionRecord("Website", fonts.Select(font => font.FamilyName).ToList())]);
        var vm = new ShellViewModel(
            new FontLibraryService(new FakeInventory([]), new FakeStore(fonts)),
            service,
            NullUserFileDialogService.Instance);

        await vm.InitializeAsync();
        await vm.ActivateSelectedCollectionCommand.ExecuteAsync(null);

        Assert.Equal(1, platform.ActivateCalls);
        Assert.Equal("已安装", vm.CollectionFonts.Single(font => font.FamilyName == "Installed Sans").StateLabel);
        Assert.Equal("已临时启用", vm.CollectionFonts.Single(font => font.FamilyName == "Managed Sans").StateLabel);
        Assert.Contains("跳过 2", vm.ToastMessage, StringComparison.Ordinal);
    }

    private static FontFamilyRecord CreateFont(
        string family,
        FontActivationState state = FontActivationState.Installed,
        FontSourceKind sourceKind = FontSourceKind.System)
    {
        var file = new FontFileRecord($"C:/Fonts/{family}.ttf", "TTF", null, sourceKind, DateTimeOffset.UtcNow);
        return new FontFamilyRecord(
            family,
            [new FontFaceRecord(family, "Regular", $"{family} Regular", $"{family}-Regular", 400, "Normal", "Normal", file)],
            sourceKind,
            state,
            LicenseStatus.Unknown,
            "未知授权",
            ["无衬线"],
            [],
            false);
    }

    private static LocalFontManagementService CreateLocalService(
        FakePlatformActivation platform,
        IReadOnlyList<FontCollectionRecord> collections)
    {
        var logStore = new FakeOperationLogStore();
        return new LocalFontManagementService(
            new FakeMetadataReader(),
            new FakeManagedFontFileStore(),
            new FakeSettingsStore(),
            new FakeMetadataStore(),
            new FakeMutationStore(),
            new FakeCollectionStore(collections),
            new FakeInstallService(),
            new FontActivationCoordinator(platform, new FakeActivationStore(), logStore),
            logStore);
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

    private sealed class FakePlatformActivation : ITemporaryFontActivationService
    {
        public int ActivateCalls { get; private set; }

        public Task<ActivationResult> ActivateForCurrentUserSessionAsync(IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken)
        {
            ActivateCalls += fonts.Count;
            return Task.FromResult(new ActivationResult(true, fonts.Select(font => new FontActivationFileResult(font, true, 1, 0)).ToList(), "activated"));
        }

        public Task<ActivationResult> DeactivateForCurrentUserSessionAsync(IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken) =>
            Task.FromResult(new ActivationResult(true, [], "deactivated"));

        public Task DeactivateAllOwnedActivationsAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeActivationStore : IActivationStore
    {
        public Task<IReadOnlyList<ActivationRecord>> GetOwnedActivationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ActivationRecord>>([]);

        public Task UpsertActivationAsync(ActivationRecord record, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveActivationAsync(string fontPath, string ownerKey, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task MarkAllOwnedStaleAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeMetadataReader : IFontMetadataReader
    {
        public Task<FontMetadata> ReadMetadataAsync(string fontFilePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeManagedFontFileStore : IManagedFontFileStore
    {
        public Task<ManagedFontCopyResult> CopyToManagedDirectoryAsync(string sourcePath, UserFontSettings settings, CancellationToken cancellationToken) =>
            Task.FromResult(new ManagedFontCopyResult(sourcePath, "hash", false));

        public Task<IReadOnlyList<string>> EnumerateManagedFontFilesAsync(UserFontSettings settings, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class FakeSettingsStore : IAppSettingsStore
    {
        public Task<UserFontSettings?> GetSettingsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<UserFontSettings?>(new UserFontSettings("C:/GlyphStash"));

        public Task SaveSettingsAsync(UserFontSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeMetadataStore : IFontMetadataStore
    {
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveFontIndexAsync(IReadOnlyList<FontFamilyRecord> fonts, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<FontFamilyRecord>> SearchAsync(FontSearchQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FontFamilyRecord>>([]);
    }

    private sealed class FakeMutationStore : IFontLibraryMutationStore
    {
        public Task SetFavoriteAsync(string familyName, bool isFavorite, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetTagsAsync(string familyName, IReadOnlyList<string> tags, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetCollectionsAsync(string familyName, IReadOnlyList<string> collections, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpsertManagedFontAsync(ManagedFontRecord managedFont, FontFamilyRecord family, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeCollectionStore : ICollectionStore
    {
        private readonly IReadOnlyList<FontCollectionRecord> _collections;

        public FakeCollectionStore(IReadOnlyList<FontCollectionRecord> collections)
        {
            _collections = collections;
        }

        public Task<IReadOnlyList<FontCollectionRecord>> GetCollectionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_collections);

        public Task CreateCollectionAsync(string name, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RenameCollectionAsync(string oldName, string newName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteCollectionAsync(string name, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddFontToCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveFontFromCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeInstallService : IFontInstallService
    {
        public Task<FontInstallResult> InstallForCurrentUserAsync(FontFileRef fontFile, CancellationToken cancellationToken) =>
            Task.FromResult(new FontInstallResult(true, fontFile.Path, fontFile.Path, "installed"));

        public Task<FontUninstallResult> UninstallManagedFontAsync(ManagedFontRecord managedFont, CancellationToken cancellationToken) =>
            Task.FromResult(new FontUninstallResult(true, managedFont.ManagedFilePath, "uninstalled"));
    }

    private sealed class FakeOperationLogStore : IOperationLogStore
    {
        public Task AppendOperationAsync(OperationLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<OperationLogEntry>> GetRecentOperationsAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OperationLogEntry>>([]);
    }
}
