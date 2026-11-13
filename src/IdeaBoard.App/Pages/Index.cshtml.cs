using IdeaBoard.App.Services;
using Microsoft.AspNetCore.Mvc;

namespace IdeaBoard.App.Pages;

/// <summary>
/// The ranking, which is the main tab: most votes first, then most recent, then highest
/// identifier. The ordering itself belongs to the service, so that the two list pages
/// differ in one call and in nothing else.
/// </summary>
public class IndexModel : VotingPageModel
{
    #region Fields

    private readonly IIdeaService ideaService;

    #endregion


    #region Construction

    /// <summary>Creates the page over the idea and vote services.</summary>
    public IndexModel(IIdeaService ideaService, IVoteService voteService)
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

    /// <summary>Reads the ranking. There is no paging: every idea is on this page.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Ideas = await ideaService.GetRankingAsync(cancellationToken);
    }

    #endregion


    #region Responses

    /// <inheritdoc />
    protected override IActionResult RedirectAfterVote(int ideaId)
    {
        return RedirectToPage("/Index", null, null, BuildIdeaAnchor(ideaId));
    }

    #endregion
}
