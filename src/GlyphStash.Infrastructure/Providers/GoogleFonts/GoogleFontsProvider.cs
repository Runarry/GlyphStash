using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Infrastructure.Providers.GoogleFonts;

public sealed class GoogleFontsProvider : IFontSourceProvider
{
    private const string Endpoint = "https://www.googleapis.com/webfonts/v1/webfonts";
    private readonly HttpClient _httpClient;

    public GoogleFontsProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string ProviderId => "google-fonts";

    public async Task<IReadOnlyList<RemoteFontFamily>> SearchAsync(RemoteFontSearchQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.ApiKey))
        {
            throw new InvalidOperationException("需要先在设置页配置 Google Fonts API key。");
        }

        var url = BuildListUrl(query);

        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        await EnsureGoogleFontsSuccessAsync(response, "Google Fonts 请求", cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var searchText = query.SearchText.Trim();
        var result = new List<RemoteFontFamily>();
        foreach (var item in items.EnumerateArray())
        {
            var family = GetString(item, "family");
            if (string.IsNullOrWhiteSpace(family))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(searchText) && !family.Contains(searchText, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            result.Add(new RemoteFontFamily(
                ProviderId,
                family,
                GetString(item, "category"),
                GetStringArray(item, "subsets"),
                GetString(item, "version"),
                DateOnly.TryParse(GetString(item, "lastModified"), out var modified) ? modified : null,
                BuildSourceUrl(family),
                $"请查看来源页面：{BuildSourceUrl(family)}",
                ReadStyles(item)));
        }

        return result.Take(50).ToList();
    }

    public async Task<RemoteFontDownloadResult> DownloadAsync(RemoteFontDownloadRequest request, CancellationToken cancellationToken)
    {
        var stagingDirectory = Path.Combine(request.ManagedFontDirectory, ".downloads", ProviderId, SanitizeFileName(request.Family.FamilyName));
        Directory.CreateDirectory(stagingDirectory);
        var files = new List<RemoteFontDownloadedFile>();
        foreach (var style in request.Styles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(new Uri(style.DownloadUrl).AbsolutePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".ttf";
            }

            var localPath = Path.Combine(stagingDirectory, $"{SanitizeFileName(request.Family.FamilyName)}-{SanitizeFileName(style.Variant)}{extension}");
            using var response = await _httpClient.GetAsync(style.DownloadUrl, cancellationToken).ConfigureAwait(false);
            await EnsureGoogleFontsSuccessAsync(response, "Google Fonts 下载", cancellationToken).ConfigureAwait(false);
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var output = File.Create(localPath))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            var bytes = await File.ReadAllBytesAsync(localPath, cancellationToken).ConfigureAwait(false);
            files.Add(new RemoteFontDownloadedFile(
                style,
                localPath,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                extension.TrimStart('.').ToUpperInvariant()));
        }

        return new RemoteFontDownloadResult(request.Family, files, $"已下载 {files.Count} 个样式。");
    }

    private static IReadOnlyList<RemoteFontStyle> ReadStyles(JsonElement item)
    {
        if (!item.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var result = new List<RemoteFontStyle>();
        foreach (var property in files.EnumerateObject())
        {
            var url = property.Value.GetString() ?? "";
            result.Add(new RemoteFontStyle(property.Name, Path.GetFileName(new Uri(url).AbsolutePath), url, true));
        }

        return result;
    }

    private static string GetString(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static IReadOnlyList<string> GetStringArray(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(entry => entry.GetString()).Where(entry => !string.IsNullOrWhiteSpace(entry)).Select(entry => entry!).ToList()
            : [];

    private static string BuildSourceUrl(string family) =>
        $"https://fonts.google.com/specimen/{Uri.EscapeDataString(family).Replace("%20", "+", StringComparison.Ordinal)}";

    private static string BuildListUrl(RemoteFontSearchQuery query)
    {
        var parameters = new List<string>
        {
            $"key={Uri.EscapeDataString(query.ApiKey)}",
            $"sort={Uri.EscapeDataString(NormalizeSort(query.Sort))}"
        };

        if (!string.IsNullOrWhiteSpace(query.Subset))
        {
            parameters.Add($"subset={Uri.EscapeDataString(query.Subset.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            parameters.Add($"category={Uri.EscapeDataString(query.Category.Trim())}");
        }

        foreach (var capability in query.Capabilities?.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase) ?? [])
        {
            parameters.Add($"capability={Uri.EscapeDataString(capability.Trim())}");
        }

        return $"{Endpoint}?{string.Join('&', parameters)}";
    }

    private static string NormalizeSort(string sort) =>
        string.IsNullOrWhiteSpace(sort) ? "alpha" : sort.Trim();

    private static string BuildExactFamilyUrl(string apiKey, string family) =>
        $"{Endpoint}?key={Uri.EscapeDataString(apiKey)}&family={Uri.EscapeDataString(family)}";

    private static async Task EnsureGoogleFontsSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var summary = SummarizeResponseBody(body);
        var message = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => $"{operation}无效：{summary}",
            HttpStatusCode.TooManyRequests => $"{operation}已限流，请稍后重试。",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Google Fonts API key 无效或没有权限。",
            _ => $"{operation}失败：{(int)response.StatusCode} {response.ReasonPhrase}. {summary}"
        };

        throw new InvalidOperationException(message);
    }

    private static string SummarizeResponseBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "响应体为空。";
        }

        var normalized = body.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return normalized.Length <= 240 ? normalized : normalized[..240] + "...";
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '-');
        }

        return value.Replace(' ', '-');
    }
}
