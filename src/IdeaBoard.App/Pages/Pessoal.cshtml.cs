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
    private readonly INewVoteNotices newVoteNotices;

    #endregion


    #region Construction

    /// <summary>Creates the page over the idea, vote and notice services.</summary>
    public PessoalModel(
        IIdeaService ideaService,
        IVoteService voteService,
        INewVoteNotices newVoteNotices)
        : base(voteService)
    {
        ArgumentNullException.ThrowIfNull(ideaService);
        ArgumentNullException.ThrowIfNull(newVoteNotices);

        this.ideaService = ideaService;
        this.newVoteNotices = newVoteNotices;
    }

    #endregion


    #region State

    /// <summary>The ideas this person submitted, most recent first.</summary>
    public IReadOnlyList<IdeaListItem> MyIdeas { get; private set; } = [];

    /// <summary>The ideas this person voted on, most recent vote first.</summary>
    public IReadOnlyList<IdeaListItem> VotedIdeas { get; private set; } = [];

    /// <summary>
    /// What this person had not seen when the page was asked for.
    ///
    /// It is read BEFORE the marker is cleared, because clearing it is what makes the
    /// answer zero. A page that cleared first and read afterwards would be correct about
    /// the database and useless to the person, who would be shown the news they came for
    /// as nothing at all.
    /// </summary>
    public NewVoteNotice NewVotes { get; private set; } = new NewVoteNotice();

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

        NewVotes = await newVoteNotices.ReadAsync(cancellationToken);

        AttachNewVoteCounts();

        await ClearTheMarkerAsync(cancellationToken);
    }


    /// <summary>
    /// Puts each idea's unseen vote count on the row that shows it, so the card can say how
    /// many of its votes are news to this person.
    ///
    /// Only this page's own ideas carry it. An idea on the ranking belongs to somebody
    /// else, and a count of votes that are new to its author is not a fact about whoever is
    /// reading.
    /// </summary>
    private void AttachNewVoteCounts()
    {
        foreach (IdeaListItem item in MyIdeas)
        {
            if (!NewVotes.VoteCountByIdea.TryGetValue(item.Id, out int newVotes))
            {
                continue;
            }

            item.NewVoteCount = newVotes;
        }
    }


    /// <summary>
    /// Marks the news as read, which is a WRITE INSIDE A GET, and the three things that
    /// make that acceptable here.
    ///
    /// It is a write inside a GET because the interface clears by the person arriving, and
    /// that is the right choice: a marker cleared by a timer is a notice claiming to have
    /// been seen by somebody who was not there. Having chosen it, the cost is paid openly
    /// rather than ignored.
    ///
    /// ONE, it does not run for a request the browser made on its own. The link to this
    /// page is in the layout and therefore on every page, so a prefetch of it would clear
    /// news nobody read, silently. See RequestIntent, which also says what to do if that
    /// turns out not to be enough.
    ///
    /// TWO, the write is conditional. The overwhelming majority of visits have nothing to
    /// clear and cost no write at all, so a repeated GET is a GET that does nothing the
    /// second time. That is as close to repeatable as this can be made while still clearing
    /// anything.
    ///
    /// THREE, nothing downstream keeps a copy. A cached copy of a page whose whole job is
    /// to be current would show stale news and would take the clearing with it.
    /// </summary>
    private async Task ClearTheMarkerAsync(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";

        if (RequestIntent.IsSpeculative(Request))
        {
            return;
        }

        await newVoteNotices.MarkSeenAsync(cancellationToken);
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
