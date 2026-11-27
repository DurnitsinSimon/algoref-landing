using Landing.Api.Development;
using Landing.Api.Landings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var migrate = args is ["--migrate"];
var builder = WebApplication.CreateBuilder(migrate ? [] : args);
if (!builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Configure platform authentication before deploying outside Development.");

var connection = builder.Configuration.GetConnectionString("Landing")
    ?? throw new InvalidOperationException("Set ConnectionStrings__Landing.");
var connectionSettings = new NpgsqlConnectionStringBuilder(connection);
if (builder.Configuration["LandingDb:Password"] is { } password)
    connectionSettings.Password = password;
builder.Services.AddDbContext<LandingDb>(options => options.UseNpgsql(connectionSettings.ConnectionString));
builder.Services.AddAuthentication("LocalPartner")
    .AddScheme<AuthenticationSchemeOptions, LocalPartnerHandler>("LocalPartner", _ => { });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("partner", policy => policy.RequireAuthenticatedUser().RequireClaim("partner_id"));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 2 * 1024 * 1024);

await using var app = builder.Build();
if (migrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<LandingDb>();
    await db.Database.MigrateAsync();
    return;
}
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();
app.MapLandingRoutes();
await app.RunAsync();
