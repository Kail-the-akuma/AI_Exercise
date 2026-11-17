using System.Security.Claims;
using IdeaBoard.App.Domain;
using Microsoft.AspNetCore.Http;

namespace IdeaBoard.App.Services;

/// <summary>
/// Reads the signed-in person's name off the current user, and exposes nothing else.
/// </summary>
public class SignedInDisplayName : ISignedInDisplayName
{
    #region Fields

    private readonly ICurrentUser currentUser;
    private readonly IHttpContextAccessor httpContextAccessor;

    #endregion


    #region Construction

    /// <summary>Creates the accessor over the current user of the request.</summary>
    public SignedInDisplayName(ICurrentUser currentUser, IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);

        this.currentUser = currentUser;
        this.httpContextAccessor = httpContextAccessor;
    }

    #endregion


    #region Resolution

    /// <inheritdoc />
    public async Task<string?> TryGetAsync(CancellationToken cancellationToken)
    {
        // The request is checked here rather than left to the current user, which is right
        // to refuse an unauthenticated request outright: every page that asks it is behind
        // the fallback policy, so arriving there unauthenticated means the pipeline is
        // wrong and should say so loudly.
        //
        // The layout is not one of those callers. It is drawn on the error page too, and the
        // error page is exactly where a refused request ends up, so asking the blunt
        // question there turns a refusal that was working correctly into a 500. That is not
        // hypothetical: it is what this did when it was first written, and an end to end
        // scenario caught it.
        if (!IsAuthenticated())
        {
            return null;
        }

        // TryGetAsync and not GetAsync. The blunt one throws when there is no row, which is
        // correct for a page that sits behind the gate and wrong for a layout that is also
        // drawn in front of it.
        User? person = await currentUser.TryGetAsync(cancellationToken);

        if (person is null)
        {
            return null;
        }

        return person.DisplayName;
    }


    /// <summary>Whether there is a request and it carries an authenticated identity.</summary>
    private bool IsAuthenticated()
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            return false;
        }

        ClaimsPrincipal principal = httpContext.User;

        if (principal.Identity is null)
        {
            return false;
        }

        return principal.Identity.IsAuthenticated;
    }

    #endregion
}
