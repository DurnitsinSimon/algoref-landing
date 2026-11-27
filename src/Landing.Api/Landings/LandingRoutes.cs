using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Landing.Api.Landings;

internal static class LandingRoutes
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapLandingRoutes(this WebApplication app)
    {
        var editor = app.MapGroup("/api/landings").RequireAuthorization("partner").WithTags("Editor");
        editor.MapPost("", Create).Produces<DraftView>(201);
        editor.MapGet("/{id:guid}", GetDraft).Produces<DraftView>();
        editor.MapPut("/{id:guid}", Save).Produces<DraftView>();
        editor.MapPost("/{id:guid}/publish", Publish);
        app.MapGet("/api/public/landings/{slug}", ReadPublished).WithTags("Site").Produces<PublishedView>();
    }

    private static async Task<IResult> Create(CreateLanding input, ClaimsPrincipal user, LandingDb db, CancellationToken ct)
    {
        if (!DocumentRules.ValidSlug(input.Slug)) return Invalid("slug", "Use lowercase Latin letters, digits and separating hyphens.");
        if (DocumentRules.Validate(input.Content) is { } error) return Invalid("content", error);

        var page = new LandingPage
        {
            Id = Guid.NewGuid(), PartnerId = PartnerId(user), Slug = input.Slug,
            Draft = JsonSerializer.Serialize(input.Content, Json)
        };
        db.Add(page);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
               { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_landings_slug" })
        {
            return Results.Problem(statusCode: 409, detail: "Slug is already in use.");
        }
        return Results.Created($"/api/landings/{page.Id}", View(page));
    }

    private static async Task<IResult> GetDraft(Guid id, ClaimsPrincipal user, LandingDb db, CancellationToken ct)
    {
        var page = await Owned(id, user, db, ct);
        return page is null ? Results.NotFound() : Results.Ok(View(page));
    }

    private static async Task<IResult> Save(Guid id, SaveDraft input, ClaimsPrincipal user, LandingDb db, CancellationToken ct)
    {
        var page = await Owned(id, user, db, ct);
        if (page is null) return Results.NotFound();
        if (input.Revision != page.Revision) return Conflict();
        if (DocumentRules.Validate(input.Content) is { } error) return Invalid("content", error);

        page.Draft = JsonSerializer.Serialize(input.Content, Json);
        page.Revision++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        return Results.Ok(View(page));
    }

    private static async Task<IResult> Publish(Guid id, PublishDraft input, ClaimsPrincipal user, LandingDb db, CancellationToken ct)
    {
        var page = await Owned(id, user, db, ct);
        if (page is null) return Results.NotFound();
        if (input.Revision != page.Revision) return Conflict();

        if (page.PublishedRevision != page.Revision)
        {
            if (!Read(page.Draft).Blocks.Any(b => !b.Hidden))
                return Invalid("content.blocks", "Add at least one visible block before publishing.");

            db.Publications.Add(new Publication
            {
                LandingId = id, Revision = page.Revision, Content = page.Draft, PublishedAt = DateTimeOffset.UtcNow
            });
            page.PublishedRevision = page.Revision;
            // EF saves the immutable snapshot and its active revision in one transaction.
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { return Conflict(); }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return Conflict();
            }
        }
        return Results.Ok(new { page.Slug, page.PublishedRevision, apiPath = $"/api/public/landings/{page.Slug}" });
    }

    private static async Task<IResult> ReadPublished(string slug, HttpContext context, LandingDb db, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        var snapshot = await (from page in db.Landings
                              join published in db.Publications on page.Id equals published.LandingId
                              where page.Slug == slug && published.Revision == page.PublishedRevision
                              select new { published.Content, published.Revision }).SingleOrDefaultAsync(ct);
        if (snapshot is null) return Results.NotFound();
        var doc = Read(snapshot.Content);
        var segment = context.User.HasClaim("segment", "member") ? "member" : "guest";
        var blocks = doc.Blocks.Where(b => DocumentRules.Visible(b, segment))
            .Select(b => new PublicBlock(b.Id, b.Type, b.Props)).ToArray();
        return Results.Ok(new PublishedView(slug, snapshot.Revision, doc.Name, doc.Type, doc.Theme, blocks, doc.SchemaVersion));
    }

    private static string PartnerId(ClaimsPrincipal user) => user.FindFirst("partner_id")!.Value;
    private static Task<LandingPage?> Owned(Guid id, ClaimsPrincipal user, LandingDb db, CancellationToken ct)
    {
        var owner = PartnerId(user);
        return db.Landings.SingleOrDefaultAsync(p => p.Id == id && p.PartnerId == owner, ct);
    }

    private static LandingDocument Read(string json) => JsonSerializer.Deserialize<LandingDocument>(json, Json)!;
    private static DraftView View(LandingPage p) => new(p.Id, p.Slug, p.Revision, p.PublishedRevision, Read(p.Draft));
    private static IResult Invalid(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
    private static IResult Conflict() => Results.Problem(statusCode: 409, detail: "Reload the current revision before retrying.");
}
