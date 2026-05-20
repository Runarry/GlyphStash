using System.Text.Json.Serialization;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Fonts;

[JsonSerializable(typeof(FontMergeReport))]
internal sealed partial class FontMergeReportJsonContext : JsonSerializerContext;
