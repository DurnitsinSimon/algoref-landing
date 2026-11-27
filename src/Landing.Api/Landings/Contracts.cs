using System.Text.Json;
using System.Text.RegularExpressions;

namespace Landing.Api.Landings;

public sealed record CreateLanding(string Slug, LandingDocument Content);
public sealed record SaveDraft(int Revision, LandingDocument Content);
public sealed record PublishDraft(int Revision);

public sealed record LandingDocument(string Name, string Type, JsonElement Theme, Block[] Blocks, int SchemaVersion = 1);
public sealed record Block(string Id, string Type, JsonElement Props, bool Hidden = false, string[]? Segments = null);
public sealed record DraftView(Guid Id, string Slug, int Revision, int? PublishedRevision, LandingDocument Content);
public sealed record PublishedView(string Slug, int Revision, string Name, string Type, JsonElement Theme,
    PublicBlock[] Blocks, int SchemaVersion);
public sealed record PublicBlock(string Id, string Type, JsonElement Props);

internal static partial class DocumentRules
{
    [GeneratedRegex(@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z")]
    private static partial Regex SlugFormat();
    public static bool ValidSlug(string? slug) => slug is { Length: > 0 and <= 100 } && SlugFormat().IsMatch(slug);

    public static string? Validate(LandingDocument? doc)
    {
        if (doc is null || doc.SchemaVersion != 1) return "Expected schemaVersion 1.";
        if (string.IsNullOrWhiteSpace(doc.Name) || doc.Name.Length > 160) return "Provide a name of up to 160 characters.";
        if (doc.Type is not ("event" or "school" or "group" or "course")) return "Unknown landing type.";
        if (doc.Theme.ValueKind != JsonValueKind.Object) return "theme must be an object.";
        if (doc.Blocks is null || doc.Blocks.Length > 100) return "Expected an array of up to 100 blocks.";

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < doc.Blocks.Length; i++)
        {
            var block = doc.Blocks[i];
            if (block is null || string.IsNullOrWhiteSpace(block.Id) || block.Id.Length > 80 || !ids.Add(block.Id))
                return $"blocks[{i}].id must be present and unique.";
            if (block.Type is not ("hero" or "heading" or "coursegrid" or "promo"))
                return $"blocks[{i}].type is unsupported in schema v1.";
            if (block.Props.ValueKind != JsonValueKind.Object) return $"blocks[{i}].props must be an object.";
            if (block.Segments?.Any(s => s is not ("guest" or "member")) == true) return $"blocks[{i}].segments contains an unknown segment.";
        }
        return null;
    }

    public static bool Visible(Block block, string segment) =>
        !block.Hidden && (block.Segments is not { Length: > 0 } || block.Segments.Contains(segment));
}
