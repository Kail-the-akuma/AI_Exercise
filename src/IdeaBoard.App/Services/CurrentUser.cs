using System.Security.Claims;
using IdeaBoard.App.Domain;
using Microsoft.AspNetCore.Http;

namespace IdeaBoard.App.Services;

/// <summary>
/// Derives the local user row from the principal the authentication pipeline produced.
///
/// There is exactly one implementation, and it lives here rather than in a host, because
/// the claims it reads are the same ones in every host. Which scheme produced the
/// principal is the only thing that differs, and that is the seam on purpose.
/// </summary>
public class CurrentUser : ICurrentUser
{
    #region Fields

    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IUserProvisioningService userProvisioningService;

    private User? resolved;

    #endregion


    #region Construction

    /// <summary>Creates the accessor for the current request.</summary>
    public CurrentUser(
        IHttpContextAccessor httpContextAccessor,
        IUserProvisioningService userProvisioningService)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        ArgumentNullException.ThrowIfNull(userProvisioningService);

        this.httpContextAccessor = httpContextAccessor;
        this.userProvisioningService = userProvisioningService;
    }

    #endregion


    #region Resolution

    /// <inheritdoc />
    public async Task<User> GetAsync(CancellationToken cancellationToken)
    {
        if (resolved is not null)
        {
            return resolved;
        }

        HttpContext? httpContext = httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            throw new InvalidOperationException(
                "There is no current request, so there is no current user to resolve.");
        }

        ClaimsPrincipal principal = httpContext.User;

        if (principal.Identity is null || !principal.Identity.IsAuthenticated)
        {
            throw new InvalidOperationException(
                "The current request is not authenticated. Every page requires a signed-in person " +
                "through the fallback authorisation policy, so reaching here means the pipeline " +
                "was composed without an authentication scheme.");
        }

        resolved = await userProvisioningService.EnsureUserAsync(principal, cancellationToken);

        return resolved;
    }

    #endregion
}
