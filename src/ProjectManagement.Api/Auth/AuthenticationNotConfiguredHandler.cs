using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ProjectManagement.Api.Auth;

public sealed class AuthenticationNotConfiguredHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        Response.ContentType = "application/problem+json";
        await Response.WriteAsync(JsonSerializer.Serialize(new
        {
            type = "about:blank",
            title = "Microsoft Entra ID no está configurado",
            status = StatusCodes.Status503ServiceUnavailable,
            detail = "El administrador debe configurar Tenant ID y Client ID antes de usar la API.",
            code = "entra_not_configured"
        }));
    }
}
