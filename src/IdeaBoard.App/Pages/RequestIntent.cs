using Microsoft.AspNetCore.Http;

namespace IdeaBoard.App.Pages;

/// <summary>
/// Whether a request was made by a person or by a browser getting ahead of one.
///
/// This exists for exactly one caller, the page that clears the new vote marker, and the
/// reason is worth stating where it is used rather than only where it was decided.
///
/// Clearing the marker is a WRITE INSIDE A GET. That is not free: a GET is supposed to be
/// repeatable and without consequence, and this one has a consequence that cannot be
/// undone. The specific way it goes wrong is a browser or an extension prefetching the
/// link to the personal page, which is in the layout and therefore on every page. The
/// prefetch arrives at the handler with nobody having clicked anything, the marker clears,
/// and the notice disappears having been seen by nobody. Nothing errors, nothing is logged,
/// and the person simply never finds out that four people voted on their idea.
///
/// Browsers that do this announce it, which is the whole reason the headers below exist.
/// They are not a complete defence and nothing claims they are: a client that prefetches
/// without saying so is indistinguishable from a person, by construction. What they do is
/// remove the cases that actually happen, which are the ones from browsers that follow the
/// standard.
///
/// THE OBSERVABLE TRIGGER, for whoever meets this later: somebody reports that the count of
/// new votes vanished without them ever opening the personal page. That is this, from a
/// client that prefetched and announced nothing. The answer then is not another header, it
/// is to stop clearing on a GET and clear from a form the person submits.
/// </summary>
public static class RequestIntent
{
    #region Constants

    /// <summary>The standard header, from the Fetch specification.</summary>
    private const string SecPurposeHeader = "Sec-Purpose";

    /// <summary>What older browsers sent before the standard one existed.</summary>
    private const string PurposeHeader = "Purpose";

    /// <summary>What Firefox sends for a link it decided to fetch early.</summary>
    private const string MozillaPrefetchHeader = "X-moz";

    /// <summary>The value all three carry when the request is not a person's.</summary>
    private const string PrefetchValue = "prefetch";

    /// <summary>What a prerender announces, which has the same consequence here.</summary>
    private const string PrerenderValue = "prerender";

    #endregion


    #region Questions

    /// <summary>
    /// Whether this request announced itself as something the browser did ahead of time.
    /// </summary>
    public static bool IsSpeculative(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (AnnouncesSpeculation(request, SecPurposeHeader))
        {
            return true;
        }

        if (AnnouncesSpeculation(request, PurposeHeader))
        {
            return true;
        }

        return AnnouncesSpeculation(request, MozillaPrefetchHeader);
    }


    /// <summary>Whether one header carries a value that means "not a person".</summary>
    private static bool AnnouncesSpeculation(HttpRequest request, string headerName)
    {
        string value = request.Headers[headerName].ToString();

        if (value.Length == 0)
        {
            return false;
        }

        if (value.Contains(PrefetchValue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return value.Contains(PrerenderValue, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
