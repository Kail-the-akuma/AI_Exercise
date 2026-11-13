using System.Security.Claims;
using System.Text.Encodings.Web;
using IdeaBoard.App.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdeaBoard.LocalIdentity;

/// <summary>
/// Signs a request in as whoever the injected source names, emitting the same three claims
/// Entra ID emits: <c>oid</c>, <c>name</c> and <c>preferred_username</c>.
///
/// It is deliberately incapable of deciding anything. It reads the source, builds a
/// principal, and stops. When the source names nobody it returns no result and lets the
/// authorisation policy do its job, rather than inventing a default person. A default here
/// would be convenient, and it would also mean that anything verified against this handler
/// was verified against a decision the handler made for itself.
/// </summary>
public class LocalAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    #region Constants

    /// <summary>Name of the scheme this handler is registered under.</summary>
    public const string SchemeName = "Local";


    #endregion


    #region Fields

    private readonly ILocalIdentitySource identitySource;

    #endregion


    #region Construction

    /// <summary>Creates the handler over the injected identity source.</summary>
    public LocalAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        ILocalIdentitySource identitySource)
        : base(options, loggerFactory, encoder)
    {
        ArgumentNullException.ThrowIfNull(identitySource);

        this.identitySource = identitySource;
    }

    #endregion


    #region Authentication

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        LocalUserIdentity? identity = identitySource.Resolve(Context);

        if (identity is null)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        ClaimsPrincipal principal = BuildPrincipal(identity);
        AuthenticationTicket ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }


    /// <summary>Builds the principal with the three claims and nothing else.</summary>
    private static ClaimsPrincipal BuildPrincipal(LocalUserIdentity identity)
    {
        List<Claim> claims =
        [
            new Claim(IdeaBoardClaimTypes.ObjectId, identity.ObjectId),
            new Claim(IdeaBoardClaimTypes.DisplayName, identity.DisplayName),
            new Claim(IdeaBoardClaimTypes.Email, identity.Email),
        ];

        ClaimsIdentity claimsIdentity = new ClaimsIdentity(
            claims,
            SchemeName,
            IdeaBoardClaimTypes.DisplayName,
            ClaimTypes.Role);

        return new ClaimsPrincipal(claimsIdentity);
    }

    #endregion
}
