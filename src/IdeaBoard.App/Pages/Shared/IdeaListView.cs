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
    /// Whether this list draws the two notes that sit above it, the public vote warning
    /// and the legend for the bar.
    ///
    /// A page that renders this partial once wants them and gets them. A page that
    /// renders it twice does not want them twice: both notes are about the whole page and
    /// not about one list, and repeated they read as though the second list were under
    /// different rules. Such a page says them once itself and turns this off.
    /// </summary>
    public bool ShowNotes { get; set; } = true;

    /// <summary>
    /// How many people have opened the board. It is the denominator the card writes out
    /// under the count, and it is written out rather than turned into a percentage
    /// because it moves: a colleague arriving for the first time makes every share on the
    /// page fall at once with nobody unvoting. With the denominator in view, the day it
    /// says twelve instead of eight the cause is beside the effect.
    ///
    /// Zero means the page did not supply it, and then nothing is drawn.
    /// </summary>
    public int ArrivedPeopleCount { get; set; }

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
