using System.Net;
using GlyphStash.Domain.Fonts;
using GlyphStash.Infrastructure.Providers.GoogleFonts;

namespace GlyphStash.Infrastructure.Tests;

public sealed class GoogleFontsProviderTests
{
    [Fact]
    public async Task SearchAsync_MapsOfficialApiFields()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "items": [
                    {
                      "family": "Noto Sans",
                      "variants": ["regular", "700"],
                      "subsets": ["latin", "latin-ext"],
                      "version": "v39",
                      "lastModified": "2025-01-01",
                      "files": {
                        "regular": "https://fonts.gstatic.com/s/notosans/regular.ttf",
                        "700": "https://fonts.gstatic.com/s/notosans/700.ttf"
                      },
                      "category": "sans-serif"
                    }
                  ]
                }
                """)
        });
        var provider = new GoogleFontsProvider(new HttpClient(handler));

        var result = await provider.SearchAsync(new RemoteFontSearchQuery("Noto Sans", "key"), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Noto Sans", result[0].FamilyName);
        Assert.Equal("sans-serif", result[0].Category);
        Assert.Equal("v39", result[0].Version);
        Assert.Equal(new DateOnly(2025, 1, 1), result[0].LastModified);
        Assert.Equal(2, result[0].Styles.Count);
        Assert.Contains("fonts.google.com", result[0].LicenseText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_FiltersLocallyWithoutFamilyQueryParameter()
    {
        Uri? requestUri = null;
        var provider = new GoogleFontsProvider(new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "items": [
                        {
                          "family": "Noto Sans",
                          "variants": ["regular"],
                          "subsets": ["latin"],
                          "version": "v1",
                          "lastModified": "2025-01-01",
                          "files": { "regular": "https://fonts.gstatic.com/s/notosans/regular.ttf" },
                          "category": "sans-serif"
                        },
                        {
                          "family": "Roboto",
                          "variants": ["regular"],
                          "subsets": ["latin"],
                          "version": "v1",
                          "lastModified": "2025-01-01",
                          "files": { "regular": "https://fonts.gstatic.com/s/roboto/regular.ttf" },
                          "category": "sans-serif"
                        }
                      ]
                    }
                    """)
            };
        })));

        var result = await provider.SearchAsync(new RemoteFontSearchQuery("Noto", "key"), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Noto Sans", result[0].FamilyName);
        Assert.NotNull(requestUri);
        Assert.DoesNotContain("family=", requestUri!.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=alpha", requestUri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAsync_IncludesSelectedGoogleFontsQueryOptions()
    {
        Uri? requestUri = null;
        var provider = new GoogleFontsProvider(new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "items": [] }""")
            };
        })));

        await provider.SearchAsync(
            new RemoteFontSearchQuery("Noto", "key", "latin-ext", "sans-serif", ["VF", "WOFF2"], "popularity"),
            CancellationToken.None);

        Assert.NotNull(requestUri);
        Assert.Contains("sort=popularity", requestUri!.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("subset=latin-ext", requestUri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("category=sans-serif", requestUri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("capability=VF", requestUri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("capability=WOFF2", requestUri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("family=", requestUri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAsync_EmptySearchReturnsFirstFiftyFamilies()
    {
        var items = string.Join(
            ",",
            Enumerable.Range(0, 60).Select(index =>
                $$"""
                  {
                    "family": "Family {{index:D2}}",
                    "variants": ["regular"],
                    "subsets": ["latin"],
                    "version": "v1",
                    "lastModified": "2025-01-01",
                    "files": { "regular": "https://fonts.gstatic.com/s/family{{index}}/regular.ttf" },
                    "category": "sans-serif"
                  }
                  """));
        var provider = new GoogleFontsProvider(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""{ "items": [{{items}}] }""")
        })));

        var result = await provider.SearchAsync(new RemoteFontSearchQuery("", "key"), CancellationToken.None);

        Assert.Equal(50, result.Count);
        Assert.Equal("Family 00", result[0].FamilyName);
        Assert.Equal("Family 49", result[^1].FamilyName);
    }

    [Fact]
    public async Task SearchAsync_ReportsBadRequestResponseBody()
    {
        var provider = new GoogleFontsProvider(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error":{"message":"Invalid family query"}}""")
        })));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.SearchAsync(new RemoteFontSearchQuery("Noto", "key"), CancellationToken.None));

        Assert.Contains("请求无效", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Invalid family query", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_ReportsRateLimitClearly()
    {
        var provider = new GoogleFontsProvider(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests))));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.SearchAsync(new RemoteFontSearchQuery("Noto Sans", "key"), CancellationToken.None));

        Assert.Contains("限流", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_ReportsNetworkFailureClearly()
    {
        var provider = new GoogleFontsProvider(new HttpClient(new StubHttpMessageHandler(_ => throw new HttpRequestException("fixture offline"))));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.SearchAsync(new RemoteFontSearchQuery("Noto Sans", "key"), CancellationToken.None));

        Assert.Contains("网络不可用", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture offline", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_ReportsTimeoutClearly()
    {
        var provider = new GoogleFontsProvider(new HttpClient(new StubHttpMessageHandler(_ => throw new TaskCanceledException("fixture timeout"))));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.SearchAsync(new RemoteFontSearchQuery("Noto Sans", "key"), CancellationToken.None));

        Assert.Contains("请求超时", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture timeout", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DownloadAsync_WritesSelectedStylesToManagedStagingDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", Guid.NewGuid().ToString("N"));
        var provider = new GoogleFontsProvider(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([0, 1, 2, 3])
        })));
        var family = new RemoteFontFamily(
            "google-fonts",
            "Noto Sans",
            "sans-serif",
            ["latin"],
            "v1",
            null,
            "https://fonts.google.com/specimen/Noto+Sans",
            "请查看来源页面：https://fonts.google.com/specimen/Noto+Sans",
            [new RemoteFontStyle("regular", "regular.ttf", "https://fonts.gstatic.com/s/notosans/regular.ttf")]);

        var result = await provider.DownloadAsync(new RemoteFontDownloadRequest(family, family.Styles, directory, "key"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(result.Files[0].LocalPath));
        Assert.Equal("TTF", result.Files[0].Format);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}
