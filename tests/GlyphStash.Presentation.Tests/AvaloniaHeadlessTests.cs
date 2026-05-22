using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Skia;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;
using GlyphStash.Presentation.Services;
using GlyphStash.Presentation.ViewModels;
using GlyphStash.Presentation.Views;

namespace GlyphStash.Presentation.Tests;

public sealed class AvaloniaHeadlessTests
{
    [Fact]
    public void ShellView_LoadsWithThemeResources()
    {
        HeadlessTestHost.EnsureAvalonia();
        var vm = ShellViewModelTestFactory.Create(ShellViewModelTestFactory.CreateLibraryService(new FakeInventory(), new FakeStore()));
        var window = new Window
        {
            Width = 1360,
            Height = 860,
            Content = new ShellView { DataContext = vm }
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);

        if (window.Content is not ShellView || window.Bounds.Width <= 0 || window.Bounds.Height <= 0)
        {
            throw new InvalidOperationException("Expected ShellView to load into a headless window with valid bounds.");
        }
    }

    [Fact]
    public void ShellView_LoadsOnlineFontsPage()
    {
        HeadlessTestHost.EnsureAvalonia();
        var vm = ShellViewModelTestFactory.Create(ShellViewModelTestFactory.CreateLibraryService(new FakeInventory(), new FakeStore()));
        vm.SelectedNavigationItem = vm.NavigationItems.Single(item => item.Key == "online-fonts");
        var remoteFont = new RemoteFontFamily(
            "google-fonts",
            "Noto Sans",
            "sans-serif",
            ["latin"],
            "v1",
            null,
            "https://fonts.google.com/specimen/Noto+Sans",
            "请查看来源页面：https://fonts.google.com/specimen/Noto+Sans",
            [new RemoteFontStyle("regular", "regular.ttf", "https://fonts.gstatic.com/s/notosans/regular.ttf")]);
        vm.SelectedRemoteFont = new RemoteFontFamilyItemViewModel(remoteFont);
        vm.OnlineDownloadQueue.Add(new OnlineFontDownloadQueueItemViewModel(
            remoteFont,
            [new RemoteFontStyle("regular", "regular.ttf", "https://fonts.gstatic.com/s/notosans/regular.ttf")],
            new OnlineFontImportOptions([], [], false, false, false)));
        var window = new Window
        {
            Width = 1360,
            Height = 860,
            Content = new ShellView { DataContext = vm }
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);

        Assert.True(vm.IsOnlineFontsPage);
        Assert.IsType<ShellView>(window.Content);
        Assert.Contains(window.GetVisualDescendants().OfType<Control>(), control => control.Name == "OnlineDownloadQueueSection");
    }

    [Fact]
    public void ShellView_LoadsMergeToolPage()
    {
        HeadlessTestHost.EnsureAvalonia();
        var vm = ShellViewModelTestFactory.Create(ShellViewModelTestFactory.CreateLibraryService(new FakeInventory(), new FakeStore()));
        vm.SelectedNavigationItem = vm.NavigationItems.Single(item => item.Key == "merge-tool");
        var window = new Window
        {
            Width = 1360,
            Height = 860,
            Content = new ShellView { DataContext = vm }
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);

        Assert.True(vm.IsMergeToolPage);
        Assert.Contains(window.GetVisualDescendants().OfType<Control>(), control => control.Name == "MergeToolRoot");
    }

    [Fact]
    public void ComboBoxSelectedValueBindings_UpdateStableSelectionProperties()
    {
        HeadlessTestHost.EnsureAvalonia();
        var vm = ShellViewModelTestFactory.Create(ShellViewModelTestFactory.CreateLibraryService(new FakeInventory(), new FakeStore()));
        vm.TagFilterOptions.Add(new LocalizedOptionViewModel("UI", "UI"));
        vm.CollectionFilterOptions.Add(new LocalizedOptionViewModel("官网改版", "官网改版"));

        var fontLibraryWindow = Show(new FontLibraryPageView { DataContext = vm });
        try
        {
            SelectComboBoxValue(fontLibraryWindow, vm.TagFilterOptions, "UI");
            SelectComboBoxValue(fontLibraryWindow, vm.CollectionFilterOptions, "官网改版");
        }
        finally
        {
            fontLibraryWindow.Close();
        }

        var onlineFontsWindow = Show(new OnlineFontsPageView { DataContext = vm });
        try
        {
            SelectComboBoxValue(onlineFontsWindow, vm.OnlineSubsetOptionModels, "latin-ext");
            SelectComboBoxValue(onlineFontsWindow, vm.OnlineCategoryOptionModels, "sans-serif");
        }
        finally
        {
            onlineFontsWindow.Close();
        }

        var mergeToolWindow = Show(new MergeToolPageView { DataContext = vm });
        try
        {
            SelectComboBoxValue(mergeToolWindow, vm.MergeModeOptionModels, "覆盖");
        }
        finally
        {
            mergeToolWindow.Close();
        }

        Assert.Equal("UI", vm.SelectedTagFilter);
        Assert.Equal("官网改版", vm.SelectedCollectionFilter);
        Assert.Equal("latin-ext", vm.SelectedOnlineSubset);
        Assert.Equal("sans-serif", vm.SelectedOnlineCategory);
        Assert.Equal("覆盖", vm.SelectedMergeModeLabel);
    }

    [Fact]
    public void ShellView_LoadsMergeRangeDialog()
    {
        HeadlessTestHost.EnsureAvalonia();
        var vm = ShellViewModelTestFactory.Create(ShellViewModelTestFactory.CreateLibraryService(new FakeInventory(), new FakeStore()));
        vm.SelectedNavigationItem = vm.NavigationItems.Single(item => item.Key == "merge-tool");
        vm.IsMergeRangeDialogOpen = true;
        vm.MergeRangeDialogStatus = "empty";
        var window = new Window
        {
            Width = 1360,
            Height = 860,
            Content = new ShellView { DataContext = vm }
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);

        Assert.True(vm.IsMergeRangeDialogOpen);
        Assert.IsType<ShellView>(window.Content);
    }

    [Fact]
    public void FontPreviewRegistry_Dispose_ReleasesSessionCollection()
    {
        HeadlessTestHost.EnsureAvalonia();
        var registry = new AvaloniaFontPreviewRegistry();

        registry.Dispose();
        registry.Dispose();

        Assert.True(registry.IsDisposed);
        Assert.NotEqual(FontFamily.Default, registry.Resolve(null, "Inter"));
    }

    private static Window Show(Control content)
    {
        var window = new Window
        {
            Width = 1360,
            Height = 860,
            Content = content
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
        return window;
    }

    private static void SelectComboBoxValue(Window window, object itemsSource, string value)
    {
        var comboBox = window.GetVisualDescendants()
            .OfType<ComboBox>()
            .Single(comboBox => ReferenceEquals(comboBox.ItemsSource, itemsSource));
        comboBox.SelectedValue = value;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private sealed class FakeInventory : IFontInventoryService
    {
        public Task<IReadOnlyList<FontFamilyRecord>> ScanInstalledFontsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<FontFamilyRecord>>([]);
    }

    private sealed class FakeStore : IFontMetadataStore
    {
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveFontIndexAsync(IReadOnlyList<FontFamilyRecord> fonts, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<FontFamilyRecord>> SearchAsync(FontSearchQuery query, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<FontFamilyRecord>>([]);
    }
}

internal static class HeadlessTestHost
{
    private static readonly AppLocalizationService LocalizationService = new();
    private static bool s_initialized;

    public static void EnsureAvalonia()
    {
        if (!s_initialized)
        {
            if (Avalonia.Application.Current is null)
            {
                TestAppBuilder.BuildAvaloniaApp().SetupWithoutStarting();
            }

            s_initialized = true;
        }

        LocalizationService.ApplyAvaloniaResources();
    }
}

public sealed class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class TestApp : Avalonia.Application
{
    public override void Initialize()
    {
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://GlyphStash.Presentation/"))
        {
            Source = new Uri("avares://GlyphStash.Presentation/Styles/Tokens.axaml")
        });

        var fluentTheme = new FluentTheme();
        fluentTheme.Palettes.Add(ThemeVariant.Light, new ColorPaletteResources { Accent = Color.Parse("#118169") });
        fluentTheme.Palettes.Add(ThemeVariant.Dark, new ColorPaletteResources { Accent = Color.Parse("#46C2A0") });
        Styles.Add(fluentTheme);
        Styles.Add(new StyleInclude(new Uri("avares://GlyphStash.Presentation/"))
        {
            Source = new Uri("avares://GlyphStash.Presentation/Styles/Components.axaml")
        });
    }
}
