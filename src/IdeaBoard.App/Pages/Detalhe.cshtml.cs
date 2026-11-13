using IdeaBoard.App.Services;
using Microsoft.AspNetCore.Mvc;

namespace IdeaBoard.App.Pages;

/// <summary>
/// One idea in full, with the public list of everybody who voted for it. The names are
/// there on purpose: transparency is a decision of the briefing, not an oversight.
/// </summary>
public class DetalheModel : VotingPageModel
{
    #region Fields

    private readonly IIdeaService ideaService;

    #endregion


    #region Construction

    /// <summary>Creates the page over the idea and vote services.</summary>
    public DetalheModel(IIdeaService ideaService, IVoteService voteService)
        : base(voteService)
    {
        ArgumentNullException.ThrowIfNull(ideaService);

        this.ideaService = ideaService;
    }

    #endregion


    #region State

    /// <summary>
    /// The idea being shown. It is only ever read by the view after the handler has found
    /// it, because an identifier that matches nothing answers with a not found instead of
    /// rendering the page.
    /// </summary>
    public IdeaDetail Idea { get; private set; } = null!;

    #endregion


    #region Handlers

    /// <summary>Reads one idea, or answers 404 when no idea has that identifier.</summary>
    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        IdeaDetail? detail = await ideaService.GetDetailAsync(id, cancellationToken);

        if (detail is null)
        {
            return NotFound();
        }

        Idea = detail;

        return Page();
    }

    #endregion


    #region Responses

    /// <inheritdoc />
    protected override IActionResult RedirectAfterVote(int ideaId)
    {
        return RedirectToPage("/Detalhe", null, new { id = ideaId }, BuildIdeaAnchor(ideaId));
    }

    #endregion
}
