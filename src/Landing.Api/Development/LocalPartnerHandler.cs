using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Landing.Api.Development;

internal sealed class LocalPartnerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IConfiguration configuration) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var remote = Context.Connection.RemoteIpAddress;
        var inContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
            && configuration.GetValue<bool>("LocalAuth:AllowContainerNetwork");
        var isLoopback = remote is not null && IPAddress.IsLoopback(
            remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote);
        if (!inContainer && !isLoopback)
            return Task.FromResult(AuthenticateResult.NoResult());

        Claim? claim = Request.Headers.Authorization.ToString() switch
        {
            "Bearer local-a" => new("partner_id", "partner-a"),
            "Bearer local-b" => new("partner_id", "partner-b"),
            "Bearer local-member" => new("segment", "member"),
            _ => null
        };
        if (claim is null)
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity([claim], Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
