using IdeaBoard.App.Services;
using Microsoft.AspNetCore.Mvc;

namespace IdeaBoard.App.Pages;

/// <summary>
/// The same ideas as the ranking, ordered by submission date descending. The second tab
/// exists so that a new idea is findable before it has any votes.
/// </summary>
public class RecentesModel : VotingPageModel
{
    #region Fields

    private readonly IIdeaService ideaService;

    #endregion


    #region Construction

    /// <summary>Creates the page over the idea and vote services.</summary>
    public RecentesModel(IIdeaService ideaService, IVoteService voteService)
        : base(voteService)
    {
        ArgumentNullException.ThrowIfNull(ideaService);

        this.ideaService = ideaService;
    }

    #endregion


    #region State

    /// <summary>The ideas to show, already ordered by the service.</summary>
    public IReadOnlyList<IdeaListItem> Ideas { get; private set; } = [];

    #endregion


    #region Handlers

    /// <summary>Reads the ideas in submission order.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Ideas = await ideaService.GetRecentAsync(cancellationToken);
    }

    #endregion


    #region Responses

    /// <inheritdoc />
    protected override IActionResult RedirectAfterVote(int ideaId)
    {
        return RedirectToPage("/Recentes", null, null, BuildIdeaAnchor(ideaId));
    }

    #endregion
}
