using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdeaBoard.App.Pages;

/// <summary>
/// The page the status code middleware re-executes for a response that carried a status
/// code and no body of its own.
///
/// Anonymous for the same reason as the error page: a refusal raised while somebody is
/// still signing in must not be answered with a page that demands a signed-in person.
/// </summary>
[AllowAnonymous]
public class EstadoModel : PageModel
{
    #region State

    /// <summary>The status code of the response that was replaced.</summary>
    public int Code { get; private set; }

    /// <summary>
    /// True for the one case the product has a real message for, which is an idea that
    /// does not exist. Everything else gets the generic wording.
    /// </summary>
    public bool IsNotFound { get; private set; }

    #endregion


    #region Handlers

    /// <summary>Reads the status code out of the route.</summary>
    public void OnGet(int code)
    {
        Code = code;
        IsNotFound = code == StatusCodes.Status404NotFound;
    }

    #endregion
}
