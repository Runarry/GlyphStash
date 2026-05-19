using System.Collections.Specialized;
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
        if (!vm.IsOnlineFontsPage || vm.CurrentPageMilestone != "M3")
        {
            throw new InvalidOperationException("Expected online fonts page to be implemented in M3.");
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

    [Fact]
    public async Task FontLibrary_FiltersBySelectedTagAndCollection()
    {
        var fonts = new[]
        {
            CreateFont("Inter", tags: ["无衬线", "UI"], collections: ["官网改版"]),
            CreateFont("Serif Sans", tags: ["衬线"], collections: ["书籍"])
        };
        var vm = new ShellViewModel(new FontLibraryService(new FakeInventory([]), new FakeStore(fonts)));

        await vm.InitializeAsync();
        vm.SelectedTagFilter = "UI";
        vm.SelectedCollectionFilter = "官网改版";

        Assert.Single(vm.Fonts);
        Assert.Equal("Inter", vm.SelectedFont?.FamilyName);
    }

    [Fact]
    public async Task SaveTagsDialog_UsesExistingSelectionsAndAllowsNewNames()
    {
        var fonts = new[] { CreateFont("Inter", tags: ["无衬线"], collections: []) };
        var tagStore = new FakeTagStore();
        var collectionStore = new FakeCollectionStore([new FontCollectionRecord("官网改版", [])]);
        var mutationStore = new FakeMutationStore(tagStore, collectionStore);
        var platform = new FakePlatformActivation();
        var service = CreateLocalService(
            platform,
            collectionStore.Collections,
            mutationStore: mutationStore,
            collectionStore: collectionStore,
            tagStore: tagStore);
        var vm = new ShellViewModel(
            new FontLibraryService(new FakeInventory([]), new FakeStore(fonts)),
            service,
            NullUserFileDialogService.Instance);

        await vm.InitializeAsync();
        vm.OpenTagsDialogCommand.Execute(null);
        vm.TagOptions.Single(option => option.Name == "UI").IsSelected = true;
        vm.CollectionOptions.Single(option => option.Name == "官网改版").IsSelected = true;
        vm.TagEditorText = "项目A";
        vm.CollectionEditorText = "品牌包";
        await vm.SaveTagsDialogCommand.ExecuteAsync(null);

        Assert.Equal(3, mutationStore.Tags.Count);
        Assert.Contains("无衬线", mutationStore.Tags);
        Assert.Contains("UI", mutationStore.Tags);
        Assert.Contains("项目A", mutationStore.Tags);
        Assert.Equal(2, mutationStore.Collections.Count);
        Assert.Contains("官网改版", mutationStore.Collections);
        Assert.Contains("品牌包", mutationStore.Collections);
        Assert.Contains("项目A", vm.SelectedFont?.Tags ?? []);
        Assert.Contains("品牌包", vm.SelectedFont?.Collections ?? []);
    }

    [Fact]
    public async Task SaveTagsDialog_PreservesExistingTagAndCollectionFilters()
    {
        var fonts = new[]
        {
            CreateFont("Inter", tags: ["UI"], collections: ["官网改版"]),
            CreateFont("Serif Sans", tags: ["衬线"], collections: ["书籍"])
        };
        var tagStore = new FakeTagStore([new TagRecord("UI", 1), new TagRecord("衬线", 1)]);
        var collectionStore = new FakeCollectionStore([new FontCollectionRecord("官网改版", ["Inter"]), new FontCollectionRecord("书籍", ["Serif Sans"])]);
        var mutationStore = new FakeMutationStore();
        var platform = new FakePlatformActivation();
        var service = CreateLocalService(
            platform,
            collectionStore.Collections,
            mutationStore: mutationStore,
            collectionStore: collectionStore,
            tagStore: tagStore);
        var vm = new ShellViewModel(
            new FontLibraryService(new FakeInventory([]), new FakeStore(fonts)),
            service,
            NullUserFileDialogService.Instance);

        await vm.InitializeAsync();
        vm.SelectedTagFilter = "UI";
        vm.SelectedCollectionFilter = "官网改版";
        vm.OpenTagsDialogCommand.Execute(null);
        await vm.SaveTagsDialogCommand.ExecuteAsync(null);

        Assert.Equal("UI", vm.SelectedTagFilter);
        Assert.Equal("官网改版", vm.SelectedCollectionFilter);
        Assert.Single(vm.Fonts);
        Assert.Equal("Inter", vm.SelectedFont?.FamilyName);
    }

    [Fact]
    public async Task SaveTagsDialog_NotifiesCurrentFiltersAfterReloadingOptions()
    {
        var fonts = new[]
        {
            CreateFont("Inter", tags: ["UI"], collections: ["官网改版"]),
            CreateFont("Serif Sans", tags: ["衬线"], collections: ["书籍"])
        };
        var tagStore = new FakeTagStore([new TagRecord("UI", 1), new TagRecord("衬线", 1)]);
        var collectionStore = new FakeCollectionStore([new FontCollectionRecord("官网改版", ["Inter"]), new FontCollectionRecord("书籍", ["Serif Sans"])]);
        var service = CreateLocalService(
            new FakePlatformActivation(),
            collectionStore.Collections,
            collectionStore: collectionStore,
            tagStore: tagStore);
        var vm = new ShellViewModel(
            new FontLibraryService(new FakeInventory([]), new FakeStore(fonts)),
            service,
            NullUserFileDialogService.Instance);
        var notifiedProperties = new List<string?>();

        await vm.InitializeAsync();
        vm.SelectedTagFilter = "UI";
        vm.SelectedCollectionFilter = "官网改版";
        vm.OpenTagsDialogCommand.Execute(null);
        vm.PropertyChanged += (_, args) => notifiedProperties.Add(args.PropertyName);

        await vm.SaveTagsDialogCommand.ExecuteAsync(null);

        Assert.Equal("UI", vm.SelectedTagFilter);
        Assert.Equal("官网改版", vm.SelectedCollectionFilter);
        Assert.Contains(nameof(vm.SelectedTagFilter), notifiedProperties);
        Assert.Contains(nameof(vm.SelectedCollectionFilter), notifiedProperties);
    }

    [Fact]
    public async Task SaveTagsDialog_DoesNotTemporarilyRemoveCurrentFilterOptions()
    {
        var fonts = new[]
        {
            CreateFont("Inter", tags: ["UI"], collections: ["官网改版"]),
            CreateFont("Serif Sans", tags: ["衬线"], collections: ["书籍"])
        };
        var tagStore = new FakeTagStore([new TagRecord("UI", 1), new TagRecord("衬线", 1)]);
        var collectionStore = new FakeCollectionStore([new FontCollectionRecord("官网改版", ["Inter"]), new FontCollectionRecord("书籍", ["Serif Sans"])]);
        var mutationStore = new FakeMutationStore(tagStore, collectionStore);
        var service = CreateLocalService(
            new FakePlatformActivation(),
            collectionStore.Collections,
            mutationStore: mutationStore,
            collectionStore: collectionStore,
            tagStore: tagStore);
        var vm = new ShellViewModel(
            new FontLibraryService(new FakeInventory([]), new FakeStore(fonts)),
            service,
            NullUserFileDialogService.Instance);
        var removedSelectedTag = false;
        var removedSelectedCollection = false;

        await vm.InitializeAsync();
        vm.SelectedTagFilter = "UI";
        vm.SelectedCollectionFilter = "官网改版";
        vm.OpenTagsDialogCommand.Execute(null);
        vm.TagEditorText = "项目A";
        vm.CollectionEditorText = "品牌包";
        vm.TagFilters.CollectionChanged += (_, args) => removedSelectedTag |= RemovesFilterOption(args, "UI");
        vm.CollectionFilters.CollectionChanged += (_, args) => removedSelectedCollection |= RemovesFilterOption(args, "官网改版");

        await vm.SaveTagsDialogCommand.ExecuteAsync(null);

        Assert.False(removedSelectedTag);
        Assert.False(removedSelectedCollection);
        Assert.Contains("项目A", vm.TagFilters);
        Assert.Contains("品牌包", vm.CollectionFilters);
    }

    [Fact]
    public async Task SaveTagsDialog_AddsNewFilterNamesWithoutClearingCurrentFilters()
    {
        var fonts = new[]
        {
            CreateFont("Inter", tags: ["UI"], collections: ["官网改版"]),
            CreateFont("Serif Sans", tags: ["衬线"], collections: ["书籍"])
        };
        var tagStore = new FakeTagStore([new TagRecord("UI", 1), new TagRecord("衬线", 1)]);
        var collectionStore = new FakeCollectionStore([new FontCollectionRecord("官网改版", ["Inter"]), new FontCollectionRecord("书籍", ["Serif Sans"])]);
        var mutationStore = new FakeMutationStore(tagStore, collectionStore);
        var platform = new FakePlatformActivation();
        var service = CreateLocalService(
            platform,
            collectionStore.Collections,
            mutationStore: mutationStore,
            collectionStore: collectionStore,
            tagStore: tagStore);
        var vm = new ShellViewModel(
            new FontLibraryService(new FakeInventory([]), new FakeStore(fonts)),
            service,
            NullUserFileDialogService.Instance);

        await vm.InitializeAsync();
        vm.SelectedTagFilter = "UI";
        vm.SelectedCollectionFilter = "官网改版";
        vm.OpenTagsDialogCommand.Execute(null);
        vm.TagEditorText = "项目A";
        vm.CollectionEditorText = "品牌包";
        await vm.SaveTagsDialogCommand.ExecuteAsync(null);

        Assert.Contains("项目A", vm.TagFilters);
        Assert.Contains("品牌包", vm.CollectionFilters);
        Assert.Equal("UI", vm.SelectedTagFilter);
        Assert.Equal("官网改版", vm.SelectedCollectionFilter);
    }

    [Fact]
    public async Task ConfirmDeleteTag_RemovesTagAndClearsMatchingFilter()
    {
        var fonts = new[]
        {
            CreateFont("Inter", tags: ["UI"], collections: []),
            CreateFont("Serif Sans", tags: ["UI", "衬线"], collections: [])
        };
        var tagStore = new FakeTagStore([new TagRecord("UI", 2), new TagRecord("衬线", 1)]);
        var platform = new FakePlatformActivation();
        var service = CreateLocalService(
            platform,
            [],
            tagStore: tagStore);
        var vm = new ShellViewModel(
            new FontLibraryService(new FakeInventory([]), new FakeStore(fonts)),
            service,
            NullUserFileDialogService.Instance);

        await vm.InitializeAsync();
        vm.SelectedTagFilter = "UI";
        vm.OpenTagsDialogCommand.Execute(null);
        vm.OpenDeleteTagDialogCommand.Execute("UI");
        await vm.ConfirmDeleteTagCommand.ExecuteAsync(null);

        Assert.Contains("UI", tagStore.DeletedTags);
        Assert.False(vm.IsDeleteTagDialogOpen);
        Assert.Equal("", vm.PendingDeleteTagName);
        Assert.Equal("全部标签", vm.SelectedTagFilter);
        Assert.DoesNotContain("UI", vm.TagFilters);
        Assert.All(vm.Fonts, font => Assert.DoesNotContain("UI", font.Tags));
    }

    [Fact]
    public async Task DeleteSelectedCollection_RemovesCollectionAndClearsFilter()
    {
        var collectionStore = new FakeCollectionStore([new FontCollectionRecord("官网改版", ["Inter"])]);
        var platform = new FakePlatformActivation();
        var service = CreateLocalService(
            platform,
            collectionStore.Collections,
            collectionStore: collectionStore);
        var vm = new ShellViewModel(
            new FontLibraryService(new FakeInventory([]), new FakeStore([CreateFont("Inter", collections: ["官网改版"])])),
            service,
            NullUserFileDialogService.Instance);

        await vm.InitializeAsync();
        vm.SelectedCollectionFilter = "官网改版";
        vm.OpenDeleteCollectionDialogCommand.Execute(null);
        await vm.ConfirmDeleteCollectionCommand.ExecuteAsync(null);

        Assert.Empty(vm.Collections);
        Assert.Equal("全部集合", vm.SelectedCollectionFilter);
        Assert.False(vm.IsDeleteCollectionDialogOpen);
    }

    [Fact]
    public async Task SearchOnlineFonts_ShowsReadableProviderError()
    {
        var logStore = new FakeOperationLogStore();
        var onlineService = new OnlineFontService(
            new ThrowingFontSourceProvider("Google Fonts 请求无效：Invalid family query"),
            new FakeSettingsStore("api-key"),
            new FakeMutationStore(),
            new FakeInstallService(),
            new FontActivationCoordinator(new FakePlatformActivation(), new FakeActivationStore(), logStore),
            logStore,
            new FakeDownloadRecordStore());
        var vm = new ShellViewModel(
            new FontLibraryService(new FakeInventory([]), new FakeStore([])),
            null,
            NullUserFileDialogService.Instance,
            onlineService);

        await vm.SearchOnlineFontsCommand.ExecuteAsync(null);

        Assert.Contains("请求无效", vm.OnlineStatus, StringComparison.Ordinal);
        Assert.DoesNotContain("Response status code does not indicate success", vm.OnlineStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchOnlineFonts_ForwardsSelectedFiltersToProvider()
    {
        var logStore = new FakeOperationLogStore();
        var provider = new CapturingFontSourceProvider();
        var onlineService = new OnlineFontService(
            provider,
            new FakeSettingsStore("api-key"),
            new FakeMutationStore(),
            new FakeInstallService(),
            new FontActivationCoordinator(new FakePlatformActivation(), new FakeActivationStore(), logStore),
            logStore,
            new FakeDownloadRecordStore());
        var vm = new ShellViewModel(
            new FontLibraryService(new FakeInventory([]), new FakeStore([])),
            null,
            NullUserFileDialogService.Instance,
            onlineService)
        {
            OnlineSearchText = "Noto",
            SelectedOnlineSubset = "latin-ext",
            SelectedOnlineCategory = "sans-serif",
            SelectedOnlineSort = "popularity",
            OnlineCapabilityVf = true,
            OnlineCapabilityWoff2 = true
        };

        await vm.SearchOnlineFontsCommand.ExecuteAsync(null);

        Assert.NotNull(provider.LastQuery);
        Assert.Equal("Noto", provider.LastQuery!.SearchText);
        Assert.Equal("latin-ext", provider.LastQuery.Subset);
        Assert.Equal("sans-serif", provider.LastQuery.Category);
        Assert.Equal("popularity", provider.LastQuery.Sort);
        Assert.Equal(["VF", "WOFF2"], provider.LastQuery.Capabilities);
    }

    [Fact]
    public async Task DownloadSelectedOnlineFont_EnqueuesAndCompletesQueueItem()
    {
        var logStore = new FakeOperationLogStore();
        var provider = new QueueingFontSourceProvider();
        var onlineService = new OnlineFontService(
            provider,
            new FakeSettingsStore("api-key"),
            new FakeMutationStore(),
            new FakeInstallService(),
            new FontActivationCoordinator(new FakePlatformActivation(), new FakeActivationStore(), logStore),
            logStore,
            new FakeDownloadRecordStore());
        var vm = new ShellViewModel(
            new FontLibraryService(new FakeInventory([]), new FakeStore([])),
            null,
            NullUserFileDialogService.Instance,
            onlineService)
        {
            SelectedRemoteFont = new RemoteFontFamilyItemViewModel(CreateRemoteFont("Noto Sans"))
        };

        await vm.DownloadSelectedOnlineFontCommand.ExecuteAsync(null);

        Assert.Single(vm.OnlineDownloadQueue);
        Assert.True(vm.HasOnlineDownloadQueue);
        Assert.Equal(OnlineFontDownloadStatus.Succeeded, vm.OnlineDownloadQueue[0].Status);
        Assert.Equal(1, provider.DownloadCalls);
        Assert.Contains("成功", vm.OnlineStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetryOnlineDownload_RerunsFailedQueueItem()
    {
        var logStore = new FakeOperationLogStore();
        var provider = new QueueingFontSourceProvider { FailDownloadCount = 1 };
        var onlineService = new OnlineFontService(
            provider,
            new FakeSettingsStore("api-key"),
            new FakeMutationStore(),
            new FakeInstallService(),
            new FontActivationCoordinator(new FakePlatformActivation(), new FakeActivationStore(), logStore),
            logStore,
            new FakeDownloadRecordStore());
        var vm = new ShellViewModel(
            new FontLibraryService(new FakeInventory([]), new FakeStore([])),
            null,
            NullUserFileDialogService.Instance,
            onlineService)
        {
            SelectedRemoteFont = new RemoteFontFamilyItemViewModel(CreateRemoteFont("Noto Sans"))
        };

        await vm.DownloadSelectedOnlineFontCommand.ExecuteAsync(null);

        var item = Assert.Single(vm.OnlineDownloadQueue);
        Assert.Equal(OnlineFontDownloadStatus.Failed, item.Status);
        Assert.True(item.CanRetry);
        Assert.Contains("fixture download failed", item.ErrorMessage, StringComparison.Ordinal);

        await vm.RetryOnlineDownloadCommand.ExecuteAsync(item);

        Assert.Equal(OnlineFontDownloadStatus.Succeeded, item.Status);
        Assert.Equal(2, provider.DownloadCalls);
    }

    private static FontFamilyRecord CreateFont(
        string family,
        FontActivationState state = FontActivationState.Installed,
        FontSourceKind sourceKind = FontSourceKind.System,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<string>? collections = null)
    {
        var file = new FontFileRecord($"C:/Fonts/{family}.ttf", "TTF", null, sourceKind, DateTimeOffset.UtcNow);
        return new FontFamilyRecord(
            family,
            [new FontFaceRecord(family, "Regular", $"{family} Regular", $"{family}-Regular", 400, "Normal", "Normal", file)],
            sourceKind,
            state,
            LicenseStatus.Unknown,
            "未知授权",
            tags ?? ["无衬线"],
            collections ?? [],
            false);
    }

    private static RemoteFontFamily CreateRemoteFont(string family) =>
        new(
            "google-fonts",
            family,
            "sans-serif",
            ["latin"],
            "v1",
            null,
            $"https://fonts.google.com/specimen/{family.Replace(' ', '+')}",
            $"请查看来源页面：https://fonts.google.com/specimen/{family.Replace(' ', '+')}",
            [new RemoteFontStyle("regular", "regular.ttf", $"https://fonts.gstatic.com/s/{family.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant()}/regular.ttf")]);

    private static bool RemovesFilterOption(NotifyCollectionChangedEventArgs args, string option) =>
        args.Action == NotifyCollectionChangedAction.Reset
        || (args.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace
            && (args.OldItems?.Cast<string>().Any(item => string.Equals(item, option, StringComparison.CurrentCultureIgnoreCase)) ?? false));

    private static LocalFontManagementService CreateLocalService(
        FakePlatformActivation platform,
        IReadOnlyList<FontCollectionRecord> collections,
        FakeMutationStore? mutationStore = null,
        FakeCollectionStore? collectionStore = null,
        FakeTagStore? tagStore = null)
    {
        var logStore = new FakeOperationLogStore();
        return new LocalFontManagementService(
            new FakeMetadataReader(),
            new FakeManagedFontFileStore(),
            new FakeSettingsStore(),
            new FakeMetadataStore(),
            mutationStore ?? new FakeMutationStore(),
            tagStore ?? new FakeTagStore(),
            collectionStore ?? new FakeCollectionStore(collections),
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
        private readonly string _apiKey;

        public FakeSettingsStore(string apiKey = "")
        {
            _apiKey = apiKey;
        }

        public Task<UserFontSettings?> GetSettingsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<UserFontSettings?>(new UserFontSettings("C:/GlyphStash", _apiKey));

        public Task SaveSettingsAsync(UserFontSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ThrowingFontSourceProvider : IFontSourceProvider
    {
        private readonly string _message;

        public ThrowingFontSourceProvider(string message)
        {
            _message = message;
        }

        public string ProviderId => "google-fonts";

        public Task<IReadOnlyList<RemoteFontFamily>> SearchAsync(RemoteFontSearchQuery query, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(_message);

        public Task<RemoteFontDownloadResult> DownloadAsync(RemoteFontDownloadRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingFontSourceProvider : IFontSourceProvider
    {
        public RemoteFontSearchQuery? LastQuery { get; private set; }

        public string ProviderId => "google-fonts";

        public Task<IReadOnlyList<RemoteFontFamily>> SearchAsync(RemoteFontSearchQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult<IReadOnlyList<RemoteFontFamily>>([]);
        }

        public Task<RemoteFontDownloadResult> DownloadAsync(RemoteFontDownloadRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class QueueingFontSourceProvider : IFontSourceProvider
    {
        public int DownloadCalls { get; private set; }

        public int FailDownloadCount { get; init; }

        public string ProviderId => "google-fonts";

        public Task<IReadOnlyList<RemoteFontFamily>> SearchAsync(RemoteFontSearchQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RemoteFontFamily>>([CreateRemoteFont("Noto Sans")]);

        public Task<RemoteFontDownloadResult> DownloadAsync(RemoteFontDownloadRequest request, CancellationToken cancellationToken)
        {
            DownloadCalls++;
            if (DownloadCalls <= FailDownloadCount)
            {
                throw new InvalidOperationException("fixture download failed");
            }

            var style = request.Styles[0];
            var path = $"C:/GlyphStash/{request.Family.FamilyName}-{style.Variant}.ttf";
            return Task.FromResult(new RemoteFontDownloadResult(
                request.Family,
                [new RemoteFontDownloadedFile(style, path, "hash", "TTF")],
                "已下载 1 个样式。"));
        }
    }

    private sealed class FakeDownloadRecordStore : IDownloadRecordStore
    {
        public Task AddDownloadRecordAsync(DownloadRecord record, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<DownloadRecord>> GetRecentDownloadRecordsAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DownloadRecord>>([]);
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
        private readonly FakeTagStore? _tagStore;
        private readonly FakeCollectionStore? _collectionStore;

        public FakeMutationStore(FakeTagStore? tagStore = null, FakeCollectionStore? collectionStore = null)
        {
            _tagStore = tagStore;
            _collectionStore = collectionStore;
        }

        public IReadOnlyList<string> Tags { get; private set; } = [];

        public IReadOnlyList<string> Collections { get; private set; } = [];

        public Task SetFavoriteAsync(string familyName, bool isFavorite, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetTagsAsync(string familyName, IReadOnlyList<string> tags, CancellationToken cancellationToken)
        {
            Tags = tags;
            _tagStore?.EnsureTags(tags);
            return Task.CompletedTask;
        }

        public Task SetCollectionsAsync(string familyName, IReadOnlyList<string> collections, CancellationToken cancellationToken)
        {
            Collections = collections;
            _collectionStore?.EnsureCollections(collections);
            return Task.CompletedTask;
        }

        public Task UpsertManagedFontAsync(ManagedFontRecord managedFont, FontFamilyRecord family, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeCollectionStore : ICollectionStore
    {
        public FakeCollectionStore(IReadOnlyList<FontCollectionRecord> collections)
        {
            Collections = collections.ToList();
        }

        public List<FontCollectionRecord> Collections { get; }

        public void EnsureCollections(IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                if (Collections.All(collection => !string.Equals(collection.Name, name, StringComparison.CurrentCultureIgnoreCase)))
                {
                    Collections.Add(new FontCollectionRecord(name, []));
                }
            }
        }

        public Task<IReadOnlyList<FontCollectionRecord>> GetCollectionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FontCollectionRecord>>(Collections);

        public Task CreateCollectionAsync(string name, CancellationToken cancellationToken)
        {
            if (Collections.All(collection => !string.Equals(collection.Name, name, StringComparison.CurrentCultureIgnoreCase)))
            {
                Collections.Add(new FontCollectionRecord(name, []));
            }

            return Task.CompletedTask;
        }

        public Task RenameCollectionAsync(string oldName, string newName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteCollectionAsync(string name, CancellationToken cancellationToken)
        {
            Collections.RemoveAll(collection => string.Equals(collection.Name, name, StringComparison.CurrentCultureIgnoreCase));
            return Task.CompletedTask;
        }

        public Task AddFontToCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveFontFromCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTagStore : ITagStore
    {
        public FakeTagStore(IReadOnlyList<TagRecord>? tags = null)
        {
            Tags = tags?.ToList() ?? [new TagRecord("无衬线", 1), new TagRecord("UI", 0)];
        }

        public List<TagRecord> Tags { get; }

        public List<string> DeletedTags { get; } = [];

        public void EnsureTags(IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                if (Tags.All(tag => !string.Equals(tag.Name, name, StringComparison.CurrentCultureIgnoreCase)))
                {
                    Tags.Add(new TagRecord(name, 0));
                }
            }
        }

        public Task<IReadOnlyList<TagRecord>> GetTagsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TagRecord>>(Tags);

        public Task CreateTagAsync(string name, CancellationToken cancellationToken)
        {
            EnsureTags([name]);
            return Task.CompletedTask;
        }

        public Task RenameTagAsync(string oldName, string newName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteTagAsync(string name, CancellationToken cancellationToken)
        {
            DeletedTags.Add(name);
            Tags.RemoveAll(tag => string.Equals(tag.Name, name, StringComparison.CurrentCultureIgnoreCase));
            return Task.CompletedTask;
        }
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
