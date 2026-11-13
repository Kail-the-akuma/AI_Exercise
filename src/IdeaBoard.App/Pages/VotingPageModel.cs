using IdeaBoard.App.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdeaBoard.App.Pages;

/// <summary>
/// Shared voting handlers for every page that shows a vote button.
///
/// The two handlers carry an explicit intent rather than toggling, so that a repeated
/// submission asks for the same end state instead of undoing the previous one. Without
/// JavaScript the response is a redirect back to the same page; with it, the same handler
/// answers with the resulting state as JSON.
/// </summary>
public abstract class VotingPageModel : PageModel
{
    #region Constants

    /// <summary>Handler name the form posts to when the person has not voted yet.</summary>
    public const string VoteHandlerName = "Vote";

    /// <summary>Handler name the form posts to when the person has already voted.</summary>
    public const string UnvoteHandlerName = "Unvote";

    #endregion


    #region Construction

    private readonly IVoteService voteService;

    /// <summary>Creates the page model over the voting service.</summary>
    protected VotingPageModel(IVoteService voteService)
    {
        ArgumentNullException.ThrowIfNull(voteService);

        this.voteService = voteService;
    }

    #endregion


    #region Handlers

    /// <summary>Records the current person's vote on an idea.</summary>
    public async Task<IActionResult> OnPostVoteAsync(int ideaId, CancellationToken cancellationToken)
    {
        VoteState? state = await voteService.AddAsync(ideaId, cancellationToken);

        return BuildResponse(ideaId, state);
    }


    /// <summary>Withdraws the current person's vote on an idea.</summary>
    public async Task<IActionResult> OnPostUnvoteAsync(int ideaId, CancellationToken cancellationToken)
    {
        VoteState? state = await voteService.RemoveAsync(ideaId, cancellationToken);

        return BuildResponse(ideaId, state);
    }

    #endregion


    #region Responses

    /// <summary>
    /// Where to send the browser back to when the page reloads after a vote. Each page
    /// answers for itself, because the ranking and the detail have different routes.
    /// </summary>
    protected abstract IActionResult RedirectAfterVote(int ideaId);


    /// <summary>
    /// Anchor for one idea, so that a reload after a vote puts the person back where they
    /// were instead of at the top of a long list.
    /// </summary>
    protected static string BuildIdeaAnchor(int ideaId)
    {
        return $"ideia-{ideaId}";
    }


    /// <summary>
    /// Answers with the state the database produced, as JSON when the caller asked for it
    /// and as a redirect otherwise.
    /// </summary>
    private IActionResult BuildResponse(int ideaId, VoteState? state)
    {
        if (state is null)
        {
            return NotFound();
        }

        if (WantsJson())
        {
            return new JsonResult(BuildVoteResponse(state));
        }

        return RedirectAfterVote(ideaId);
    }


    /// <summary>
    /// Turns the database state into everything the interface may show, wording included,
    /// so that the script is left with nothing to compute.
    /// </summary>
    public static VoteResponse BuildVoteResponse(VoteState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        string buttonText = UiText.VoteButton;
        string nextHandler = VoteHandlerName;

        if (state.HasVoted)
        {
            buttonText = UiText.VotedButton;
            nextHandler = UnvoteHandlerName;
        }

        return new VoteResponse
        {
            IdeaId = state.IdeaId,
            HasVoted = state.HasVoted,
            VoteCount = state.VoteCount,
            VoteCountText = UiText.FormatVoteCount(state.VoteCount),
            ButtonText = buttonText,
            NextHandler = nextHandler,
        };
    }


    /// <summary>Tells whether the caller asked for JSON rather than a page.</summary>
    private bool WantsJson()
    {
        string accept = Request.Headers.Accept.ToString();

        return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
