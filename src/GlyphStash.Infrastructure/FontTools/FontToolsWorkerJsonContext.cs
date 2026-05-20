using System.Text.Json.Serialization;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Infrastructure.FontTools;

[JsonSerializable(typeof(FontToolsWorkerRequestMessage))]
[JsonSerializable(typeof(FontToolsWorkerResponseMessage))]
[JsonSerializable(typeof(FontMergeProgress))]
internal sealed partial class FontToolsWorkerJsonContext : JsonSerializerContext;
