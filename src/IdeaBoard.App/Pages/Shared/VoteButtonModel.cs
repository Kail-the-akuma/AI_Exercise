namespace IdeaBoard.App.Pages.Shared;

/// <summary>
/// Everything the vote button partial renders, and nothing else.
///
/// It is a separate carrier rather than the list row or the detail read model so that the
/// partial renders the same way on every page. The button is the one piece of markup the
/// progressive enhancement script depends on, and one shape for it everywhere is what
/// keeps that dependency from being page specific.
/// </summary>
public class VoteButtonModel
{
    /// <summary>The idea the button votes on.</summary>
    public int IdeaId { get; set; }

    /// <summary>Whether the person looking at the page has already voted on it.</summary>
    public bool HasVoted { get; set; }

    /// <summary>Votes the idea has right now, as the server counted them.</summary>
    public int VoteCount { get; set; }

    /// <summary>
    /// The search the person is looking at this row through, carried so that voting does
    /// not throw them back to the whole board.
    ///
    /// It travels inside the form rather than on its address because the form's action is
    /// built from the page's route values, which a query string is not part of. Without it
    /// the term is lost on the way to the server and the redirect after a vote has nothing
    /// to put back. Null on every page that has no search.
    /// </summary>
    public string? SearchTerm { get; set; }
}
