using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace IdeaBoard.Local.Pages;

/// <summary>
/// Picks which configured person this browser is signed in as.
///
/// It is a development tool and not part of the product, so none of the interface strings
/// fixed by the functional specification appear here. Keeping them out is what stops this
/// page drifting into looking like something the application ships.
/// </summary>
[AllowAnonymous]
public class PessoaModel : PageModel
{
    #region Fields

    private readonly LocalPeopleOptions options;

    #endregion


    #region Construction

    /// <summary>Creates the page over the configured list of people.</summary>
    public PessoaModel(IOptions<LocalPeopleOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options.Value;
    }

    #endregion


    #region State

    /// <summary>People on offer, read from configuration.</summary>
    public IReadOnlyList<LocalPerson> People { get; private set; } = [];

    #endregion


    #region Handlers

    /// <summary>Shows the list of people.</summary>
    public void OnGet()
    {
        People = options.People;
    }


    /// <summary>Remembers the chosen person in a cookie and goes to the application.</summary>
    public IActionResult OnPostChoose(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return RedirectToPage();
        }

        CookieOptions cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            IsEssential = true,
        };

        Response.Cookies.Append(SelectedPersonIdentitySource.CookieName, objectId, cookieOptions);

        return Redirect("/");
    }


    /// <summary>Forgets the chosen person.</summary>
    public IActionResult OnPostClear()
    {
        Response.Cookies.Delete(SelectedPersonIdentitySource.CookieName);

        return RedirectToPage();
    }

    #endregion
}
