using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SettleCore.Api.Security;

public sealed class LocalApiAccessOptions
{
    public bool Enabled { get; set; } = true;
    public string? OperatorKey { get; set; }
}

public static class LocalApiAccess
{
    public const string Policy = "LocalOperator";
    public const string Scheme = "LocalOperatorKey";
    public const string Header = "X-SettleCore-Operator-Key";

    public static IServiceCollection AddLocalApiAccess(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<LocalApiAccessOptions>()
            .Bind(configuration.GetSection("LocalApiAccess"))
            .Validate(settings => settings.Enabled || environment.IsDevelopment(),
                "Local API access controls may only be disabled explicitly in Development.")
            .Validate(settings => !settings.Enabled ||
                (!string.IsNullOrWhiteSpace(settings.OperatorKey) && settings.OperatorKey.Length >= 32),
                "Enabled local API access requires an operator key of at least 32 characters.")
            .ValidateOnStart();
        services.AddAuthentication(Scheme)
            .AddScheme<AuthenticationSchemeOptions, LocalOperatorAuthenticationHandler>(Scheme, _ => { });
        services.AddAuthorizationBuilder().AddPolicy(Policy, policy =>
            policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser());
        return services;
    }
}

public sealed class LocalOperatorAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IOptions<LocalApiAccessOptions> access)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var settings = access.Value;
        if (settings.Enabled)
        {
            var supplied = Request.Headers[LocalApiAccess.Header];
            if (!Request.IsHttps || supplied.Count != 1 || supplied[0] is not { Length: >= 32 and <= 1024 } key ||
                !CryptographicOperations.FixedTimeEquals(
                    SHA256.HashData(Encoding.UTF8.GetBytes(key)),
                    SHA256.HashData(Encoding.UTF8.GetBytes(settings.OperatorKey!))))
                return Task.FromResult(AuthenticateResult.Fail("Valid local operator credentials are required over HTTPS."));
        }
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "local-operator")], LocalApiAccess.Scheme));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, LocalApiAccess.Scheme)));
    }
}
