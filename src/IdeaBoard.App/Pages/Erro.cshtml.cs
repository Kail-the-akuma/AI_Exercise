using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdeaBoard.App.Pages;

/// <summary>
/// The generic error page, reached by re-execution from the exception handler.
///
/// It is anonymous on purpose. The fallback policy requires an authenticated person
/// everywhere, and an exception raised while somebody is still signing in would send them
/// to a page that demands the very sign-in that is failing, which loops.
///
/// Nothing technical is shown. The correlation identifier is the one thing the person can
/// quote, and it is what ties their report to the entry in the Event Log.
/// </summary>
[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErroModel : PageModel
{
    #region State

    /// <summary>Identifier the person can quote when asking for help.</summary>
    public string CorrelationId { get; private set; } = string.Empty;

    #endregion


    #region Handlers

    /// <summary>
    /// Takes the identifier of the current activity when there is one, because that is
    /// what the logging stack records, and falls back to the request identifier.
    /// </summary>
    public void OnGet()
    {
        CorrelationId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }

    #endregion
}
