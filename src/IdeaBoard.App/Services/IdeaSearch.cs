using System.Globalization;

namespace IdeaBoard.App.Services;

/// <summary>
/// Narrowing a list of ideas to the ones whose title or author matches what somebody typed.
///
/// The matching is done here, in the application, and deliberately not in SQL. That is a
/// decision and not an oversight, and it was taken against measurements rather than from
/// memory, so the measurements are written down next to it.
///
/// SQLite folds case for ASCII only. Measured against a real database through this very
/// stack, with a row reading "Sessões de partilha" and a person named "João Gonçalves":
///
///   LIKE '%sessoes%'  finds nothing, because the pattern has no circumflex.
///   LIKE '%SESSÕES%'  finds nothing EITHER, although the row contains exactly that word,
///                     because the uppercase O with tilde does not fold to the lowercase one.
///   LIKE '%joao%'     does not find "João", and neither does LIKE '%JOÃO%'.
///   upper(title)      is ASCII only as well, so "Sessões" uppercases to "SESSõES" and the
///                     usual trick of comparing uppercased values does not rescue it.
///
/// The second line is the one that settles it. Somebody who spells a colleague's name
/// correctly, in capitals, finds nobody. A search that fails the person who types it right
/// is not a search, and in an application written in Portuguese that is most people.
///
/// There is a further measured fact behind the shape of this file: string.Contains with a
/// StringComparison does not translate to SQL on this provider at all. It throws at run
/// time rather than falling back, so the obvious way to write this query does not exist.
///
/// The one test over these claims lives in IdeaSearchTests and asserts them against a real
/// SQLite file, so that moving this filtering back into the query cannot happen quietly.
/// </summary>
public static class IdeaSearch
{
    #region Constants

    /// <summary>
    /// Name of the query string value the term travels in.
    ///
    /// The term is in the address because the search has to work without JavaScript, like
    /// everything else here, which makes it a plain form submitted with GET. The address
    /// being the whole state is what lets a search be bookmarked, reloaded and sent to
    /// somebody else.
    /// </summary>
    public const string QueryParameterName = "procura";


    /// <summary>
    /// How the term and the text are compared: case is ignored, and so are the marks above
    /// the letters. The second half is the whole point, and the first half is what SQLite
    /// already did for unaccented letters only.
    /// </summary>
    private const CompareOptions MatchingOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    #endregion


    #region Term

    /// <summary>
    /// Turns what arrived in the address into either a usable term or nothing at all.
    ///
    /// Whitespace alone is nothing, so that a stray space in a bookmarked address does not
    /// produce an empty result set, and so that the pages have one state to test for
    /// instead of two that look alike.
    /// </summary>
    public static string? Clean(string? rawTerm)
    {
        if (string.IsNullOrWhiteSpace(rawTerm))
        {
            return null;
        }

        return rawTerm.Trim();
    }

    #endregion


    #region Filtering

    /// <summary>
    /// The ideas whose title or author name contains the term, keeping the order they
    /// arrived in. A term of nothing returns the list untouched, which is the unsearched
    /// page.
    /// </summary>
    public static List<IdeaListItem> Filter(List<IdeaListItem> items, string? term)
    {
        ArgumentNullException.ThrowIfNull(items);

        string? cleaned = Clean(term);

        if (cleaned is null)
        {
            return items;
        }

        List<IdeaListItem> matching = new List<IdeaListItem>();

        foreach (IdeaListItem item in items)
        {
            if (!Matches(item, cleaned))
            {
                continue;
            }

            matching.Add(item);
        }

        return matching;
    }


    /// <summary>
    /// Whether one idea answers the term, by its title or by who proposed it.
    ///
    /// Both fields are searched because the two questions people actually arrive with are
    /// "was this already suggested" and "what did this person suggest", and making them two
    /// separate controls would be a filter, which the product does not have.
    /// </summary>
    public static bool Matches(IdeaListItem item, string term)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Contains(item.Title, term))
        {
            return true;
        }

        return Contains(item.AuthorDisplayName, term);
    }


    /// <summary>
    /// Whether the text contains the term, ignoring case and accents.
    ///
    /// The comparison is the invariant one rather than a Portuguese one, on purpose. What
    /// is wanted here is that the same term finds the same ideas on every machine the
    /// application is ever deployed to, and a culture-sensitive comparison makes that a
    /// property of the server's configuration instead of a property of the product.
    /// </summary>
    private static bool Contains(string text, string term)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        int found = CompareInfo.GetCompareInfo(CultureInfo.InvariantCulture.Name)
            .IndexOf(text, term, MatchingOptions);

        return found >= 0;
    }

    #endregion
}
