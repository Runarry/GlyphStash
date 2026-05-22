using GlyphStash.Domain.Fonts;
using Microsoft.Data.Sqlite;

namespace GlyphStash.Infrastructure.Storage.Sqlite;

public sealed partial class SqliteFontMetadataStore
{
    public async Task<UserFontSettings?> GetSettingsAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT key, value FROM app_settings WHERE key IN ('managed_font_directory', 'google_fonts_api_key', 'ui_culture');";
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            values[reader.GetString(0)] = reader.GetString(1);
        }

        values.TryGetValue("managed_font_directory", out var directory);
        values.TryGetValue("google_fonts_api_key", out var apiKey);
        values.TryGetValue("ui_culture", out var uiCulture);
        return string.IsNullOrWhiteSpace(directory) && string.IsNullOrWhiteSpace(apiKey) && string.IsNullOrWhiteSpace(uiCulture)
            ? null
            : new UserFontSettings(directory ?? "", apiKey ?? "", uiCulture ?? "");
    }

    public async Task SaveSettingsAsync(UserFontSettings settings, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await UpsertSettingAsync(connection, "managed_font_directory", settings.ManagedFontDirectory, cancellationToken).ConfigureAwait(false);
        await UpsertSettingAsync(connection, "google_fonts_api_key", settings.GoogleFontsApiKey, cancellationToken).ConfigureAwait(false);
        await UpsertSettingAsync(connection, "ui_culture", settings.UiCultureCode, cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertSettingAsync(SqliteConnection connection, string key, string value, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO app_settings(key, value, updated_at)
            VALUES($key, $value, datetime('now'))
            ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
