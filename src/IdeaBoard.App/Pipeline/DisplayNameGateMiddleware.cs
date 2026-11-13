using IdeaBoard.App.Domain;
using IdeaBoard.App.Services;
using Microsoft.AspNetCore.Http;

namespace IdeaBoard.App.Pipeline;

/// <summary>
/// Keeps somebody whose name is not known yet out of everything except the page that asks
/// for it.
///
/// It is one piece of middleware rather than a check on each page, for the same reason
/// authorisation is a fallback policy rather than an attribute: a page added later is born
/// behind this gate without anybody having to remember. A page added later behind a
/// per-page check is born open.
///
/// What it protects is not access, it is what other people see. Submitting an idea or
/// casting a vote publishes a name to colleagues, and there is no editing and no removal
/// in this product, so a wrong name published once is published for good.
/// </summary>
public class DisplayNameGateMiddleware
{
    #region Constants

    /// <summary>Where somebody without a name is sent.</summary>
    public const string RegistrationPath = "/nome";

    #endregion


    #region Fields

    private readonly RequestDelegate next;

    #endregion


    #region Construction

    /// <summary>Creates the middleware over the rest of the pipeline.</summary>
    public DisplayNameGateMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);

        this.next = next;
    }

    #endregion


    #region Pipeline

    /// <summary>
    /// Lets the request through when a name is known, and redirects to the registration
    /// page when it is not.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(currentUser);

        if (!ShouldGuard(context))
        {
            await next(context);
            return;
        }

        User? person = await currentUser.TryGetAsync(context.RequestAborted);

        if (person is not null)
        {
            await next(context);
            return;
        }

        // A redirect and not a 403. The person has done nothing wrong and there is
        // something they can do about it, which is exactly what a redirect says and what a
        // refusal does not.
        context.Response.Redirect(RegistrationPath);
    }


    /// <summary>
    /// Decides whether this request is one the gate has an opinion about.
    ///
    /// Static files never reach here: they are served earlier in the pipeline. The error
    /// and status pages are let through because the status code middleware re-executes the
    /// pipeline against them, and a diagnostic page that redirects instead of explaining
    /// what went wrong is worse than no diagnostic page.
    /// </summary>
    private static bool ShouldGuard(HttpContext context)
    {
        if (context.User.Identity is null || !context.User.Identity.IsAuthenticated)
        {
            return false;
        }

        PathString path = context.Request.Path;

        if (path.StartsWithSegments(RegistrationPath))
        {
            return false;
        }

        if (path.StartsWithSegments("/erro"))
        {
            return false;
        }

        if (path.StartsWithSegments("/estado"))
        {
            return false;
        }

        return true;
    }

    #endregion
}
