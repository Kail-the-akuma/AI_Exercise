using IdeaBoard.App.Services;
using Microsoft.AspNetCore.Mvc;

namespace IdeaBoard.App.Pages;

/// <summary>
/// The third tab: what the current person has put in and what they have supported.
///
/// It is a view over what already exists and stores nothing of its own. Authorship is
/// already on the idea, the vote is already its own row, and the vote is already public,
/// so nothing here records a new relationship between a person and an idea.
/// </summary>
public class PessoalModel : VotingPageModel
{
    #region Fields

    private readonly IIdeaService ideaService;

    #endregion


    #region Construction

    /// <summary>Creates the page over the idea and vote services.</summary>
    public PessoalModel(IIdeaService ideaService, IVoteService voteService)
        : base(voteService)
    {
        ArgumentNullException.ThrowIfNull(ideaService);

        this.ideaService = ideaService;
    }

    #endregion


    #region State

    /// <summary>The ideas this person submitted, most recent first.</summary>
    public IReadOnlyList<IdeaListItem> MyIdeas { get; private set; } = [];

    /// <summary>The ideas this person voted on, most recent vote first.</summary>
    public IReadOnlyList<IdeaListItem> VotedIdeas { get; private set; } = [];

    #endregion


    #region Emptiness

    /// <summary>
    /// Whether this person has not submitted anything yet.
    ///
    /// The two ways this page can be empty are kept apart, and this is the whole reason the
    /// page carries two lists instead of one. On the first day EVERYBODY is in both states
    /// at once, so an empty page is not a rare edge here: it is what the whole team sees
    /// first. A single empty list would leave the view unable to tell the two apart, and
    /// nobody could write two different sentences even wanting to.
    /// </summary>
    public bool HasSubmittedNothing
    {
        get
        {
            return MyIdeas.Count == 0;
        }
    }

    /// <summary>Whether this person has not voted on anything yet.</summary>
    public bool HasVotedOnNothing
    {
        get
        {
            return VotedIdeas.Count == 0;
        }
    }

    #endregion


    #region Handlers

    /// <summary>
    /// Reads both lists. An idea may be in both, because a person may vote on an idea of
    /// their own, and nothing here hides it from either list: what the page does about that
    /// is a decision for the page and not for the data.
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        MyIdeas = await ideaService.GetMineAsync(cancellationToken);
        VotedIdeas = await ideaService.GetVotedByMeAsync(cancellationToken);
    }

    #endregion


    #region Responses

    /// <inheritdoc />
    protected override IActionResult RedirectAfterVote(int ideaId)
    {
        // The anchor can point at a row that is no longer here, and that is correct rather
        // than a defect. Withdrawing a vote on this tab takes the idea out of the list it
        // was in, so there is nothing left to scroll back to unless the person also wrote
        // it. The browser then lands at the top of the page, which is the honest answer to
        // "take me back to where I was" when that place stopped existing.
        return RedirectToPage("/Pessoal", null, null, BuildIdeaAnchor(ideaId));
    }

    #endregion
}
