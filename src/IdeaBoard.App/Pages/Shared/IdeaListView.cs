using IdeaBoard.App.Services;

namespace IdeaBoard.App.Pages.Shared;

/// <summary>
/// What the list partial renders: the rows, and enough about where they came from to say
/// the right thing when there are none.
///
/// The second part is the whole reason this type exists. A list of rows cannot tell apart a
/// board where nobody has proposed anything yet from a search that matched nothing, and the
/// two need opposite sentences: one invites the person to be the first to propose an idea,
/// and saying that to somebody whose search found nothing on a board with two hundred ideas
/// is simply false.
/// </summary>
public class IdeaListView
{
    /// <summary>The rows to draw, already ordered and already narrowed.</summary>
    public IReadOnlyList<IdeaListItem> Items { get; set; } = [];

    /// <summary>
    /// The term these rows were narrowed by, or null when nothing was searched for. Blank
    /// is not a search, which <see cref="IdeaSearch.Clean"/> settles in one place.
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>Whether these rows are the result of a search rather than the whole board.</summary>
    public bool IsSearching
    {
        get
        {
            return IdeaSearch.Clean(SearchTerm) is not null;
        }
    }

    /// <summary>Whether the empty state to show is the one for a search that found nothing.</summary>
    public bool FoundNothing
    {
        get
        {
            return Items.Count == 0 && IsSearching;
        }
    }
}
