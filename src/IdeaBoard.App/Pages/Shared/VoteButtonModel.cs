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
}
