namespace IdeaBoard.App.Services;

/// <summary>
/// The name of whoever is signed in, for the parts of the interface that are on every page.
///
/// It exists instead of handing the layout the whole user row, because that row carries the
/// account name and the subject identifier, and the data model says in as many words that
/// the account name is never rendered in the interface. A layout that can reach it is one
/// edit away from rendering it, so it is not given the chance.
/// </summary>
public interface ISignedInDisplayName
{
    /// <summary>
    /// The name to show, or null when there is nobody to name yet.
    ///
    /// Null is a normal answer and not a failure, and this method never throws because of
    /// it. The layout is drawn on every page, including the two that are reachable before a
    /// person has a row at all: the page that asks them to register a name, and the error
    /// page. Something drawn on every page cannot be allowed to fail on the pages that
    /// exist precisely because something has not happened yet.
    /// </summary>
    Task<string?> TryGetAsync(CancellationToken cancellationToken);
}
