using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;
using GlyphStash.Presentation.ViewModels;
using GlyphStash.Presentation.Views;

namespace GlyphStash.Presentation.Tests;

public sealed class AvaloniaHeadlessTests
{
    private static bool s_initialized;

    [Fact]
    public void ShellView_LoadsWithThemeResources()
    {
        EnsureAvalonia();
        var vm = new ShellViewModel(new FontLibraryService(new FakeInventory(), new FakeStore()));
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
        EnsureAvalonia();
        var vm = new ShellViewModel(new FontLibraryService(new FakeInventory(), new FakeStore()));
        vm.SelectedNavigationItem = vm.NavigationItems.Single(item => item.Key == "online-fonts");
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
    }

    private static void EnsureAvalonia()
    {
        if (s_initialized)
        {
            return;
        }

        TestAppBuilder.BuildAvaloniaApp().SetupWithoutStarting();
        s_initialized = true;
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

public sealed class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}

public sealed class TestApp : Avalonia.Application
{
    public override void Initialize()
    {
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://GlyphStash.Presentation/"))
        {
            Source = new Uri("avares://GlyphStash.Presentation/Styles/Tokens.axaml")
        });

        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://GlyphStash.Presentation/"))
        {
            Source = new Uri("avares://GlyphStash.Presentation/Styles/Components.axaml")
        });
    }
}
