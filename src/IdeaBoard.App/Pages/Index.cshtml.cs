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
    private readonly IBoardPeople boardPeople;

    #endregion


    #region Construction

    /// <summary>Creates the page over the idea, vote and people services.</summary>
    public IndexModel(IIdeaService ideaService, IVoteService voteService, IBoardPeople boardPeople)
        : base(voteService)
    {
        ArgumentNullException.ThrowIfNull(ideaService);
        ArgumentNullException.ThrowIfNull(boardPeople);

        this.ideaService = ideaService;
        this.boardPeople = boardPeople;
    }

    #endregion


    #region State

    /// <summary>The ideas to show, already ordered and already narrowed by the service.</summary>
    public IReadOnlyList<IdeaListItem> Ideas { get; private set; } = [];

    /// <summary>
    /// What was typed in the search box, as it will be put back into it.
    ///
    /// It is bound from the query string and never from a posted field, because the search
    /// is a plain form submitted with GET. That is what makes a search an address: it can
    /// be bookmarked, reloaded, and sent to somebody else, and it works with the browser's
    /// scripting turned off like the rest of the application.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = IdeaSearch.QueryParameterName)]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// How many people have arrived at the board so far. See <see cref="IBoardPeople"/>
    /// for what this does and does not count: it is arrivals, never the size of the team.
    /// </summary>
    public int ArrivedPeopleCount { get; private set; }

    /// <summary>Whether this page is showing the result of a search rather than everything.</summary>
    public bool IsSearching
    {
        get
        {
            return IdeaSearch.Clean(SearchTerm) is not null;
        }
    }

    #endregion


    #region Handlers

    /// <summary>
    /// Reads the ranking and how many people have arrived.
    ///
    /// There is no paging: every idea that matches is on this page, and with no search term
    /// that is every idea there is. The service relies on that to take the leading count
    /// from memory and to narrow the list there as well.
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Ideas = await ideaService.GetRankingAsync(SearchTerm, cancellationToken);
        ArrivedPeopleCount = await boardPeople.CountArrivedAsync(cancellationToken);
    }

    #endregion


    #region Responses

    /// <inheritdoc />
    protected override IActionResult RedirectAfterVote(int ideaId)
    {
        // The search travels back with the redirect. Without it, voting from a narrowed
        // list throws the person back to the whole board and loses the row they were
        // looking at, which is the one thing a vote must not do.
        //
        // The name is spelled out here instead of using IdeaSearch.QueryParameterName
        // because a member of an anonymous type has to be an identifier written at the call
        // site. The two are pinned together by a test rather than by the compiler.
        return RedirectToPage(
            "/Index",
            null,
            new { procura = IdeaSearch.Clean(SearchTerm) },
            BuildIdeaAnchor(ideaId));
    }

    #endregion
}
