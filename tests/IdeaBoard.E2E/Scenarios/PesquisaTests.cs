using IdeaBoard.App.Pages;
using IdeaBoard.E2E.Fixtures;
using Microsoft.Playwright;
using Xunit;

namespace IdeaBoard.E2E.Scenarios;

/// <summary>
/// Searching the board, and the personal area, through a real browser.
///
/// The search is a plain form submitted with GET, so the scenarios that matter most here
/// are the ones that prove the address IS the search: one of them never touches the form at
/// all and goes straight to the address, and one of them turns the browser's scripting off
/// entirely.
/// </summary>
[Collection(AppHostCollection.Name)]
public class PesquisaTests : ScenarioBase
{
    #region Construction

    /// <inheritdoc />
    public PesquisaTests(AppHostFixture host)
        : base(host)
    {
    }

    #endregion


    #region Scenarios

    /// <summary>
    /// Searching narrows the list to the matching idea, and the term ends up in the
    /// address, where it can be bookmarked and sent to somebody else.
    /// </summary>
    [Fact]
    public async Task SearchingNarrowsTheListAndPutsTheTermInTheAddress()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        await SubmitIdeaAsync(ana, "Sessões de partilha", "Descricao.");
        await ana.WaitForURLAsync(url => !url.EndsWith("/nova", StringComparison.Ordinal));

        await SubmitIdeaAsync(ana, "Bicicletas para todos", "Descricao.");
        await ana.WaitForURLAsync(url => !url.EndsWith("/nova", StringComparison.Ordinal));

        await ana.GotoAsync(Url("/"));

        await Expect(Rows(ana)).ToHaveCountAsync(2);

        await ana.GetByLabel(UiText.SearchLabel).FillAsync("sessoes");
        await ana.GetByRole(AriaRole.Button, new() { Name = UiText.SearchButton }).ClickAsync();

        await Expect(Rows(ana)).ToHaveCountAsync(1);
        await Expect(Rows(ana).First).ToContainTextAsync("Sessões de partilha");

        Assert.Contains("procura=sessoes", ana.Url, StringComparison.Ordinal);
    }


    /// <summary>
    /// The search works with the browser's scripting turned off, which is the arrangement
    /// the whole application is built on and the one this feature could most easily break.
    ///
    /// It is also the scenario that pins the accents. The term has none and the title has
    /// them, which is the comparison the database itself cannot do.
    /// </summary>
    [Fact]
    public async Task SearchingWorksWithoutScriptingAndIgnoresAccents()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        await SubmitIdeaAsync(ana, "Sessões de partilha", "Descricao.");
        await ana.WaitForURLAsync(url => !url.EndsWith("/nova", StringComparison.Ordinal));

        await SubmitIdeaAsync(ana, "Bicicletas para todos", "Descricao.");
        await ana.WaitForURLAsync(url => !url.EndsWith("/nova", StringComparison.Ordinal));

        IPage plain = await OpenWithoutScriptAsAsync(People.BrunoObjectId);

        await plain.GotoAsync(Url("/"));
        await Expect(Rows(plain)).ToHaveCountAsync(2);

        await plain.GetByLabel(UiText.SearchLabel).FillAsync("SESSOES");
        await plain.GetByRole(AriaRole.Button, new() { Name = UiText.SearchButton }).ClickAsync();

        await Expect(Rows(plain)).ToHaveCountAsync(1);
        await Expect(Rows(plain).First).ToContainTextAsync("Sessões de partilha");
    }


    /// <summary>
    /// A search that matches nothing says so, and does not say the board is empty.
    ///
    /// The wrong sentence here is not a cosmetic slip. Telling somebody whose search found
    /// nothing that there are no ideas yet, and inviting them to be the first to propose
    /// one, is false on a board that has ideas in it.
    /// </summary>
    [Fact]
    public async Task ASearchThatMatchesNothingDoesNotClaimTheBoardIsEmpty()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        await SubmitIdeaAsync(ana, "Bicicletas para todos", "Descricao.");
        await ana.WaitForURLAsync(url => !url.EndsWith("/nova", StringComparison.Ordinal));

        await ana.GotoAsync(Url("/?procura=comboios"));

        await Expect(Rows(ana)).ToHaveCountAsync(0);
        await Expect(ana.GetByText(UiText.NoSearchResults)).ToBeVisibleAsync();
        await Expect(ana.GetByText(UiText.EmptyList)).ToHaveCountAsync(0);
    }


    /// <summary>
    /// Voting from a narrowed list keeps the person on the narrowed list.
    ///
    /// Losing the term here does not look like a defect: the vote works, the count is
    /// right, and the person is simply somewhere else, looking at a board they did not ask
    /// for and without the row they were reading.
    /// </summary>
    [Fact]
    public async Task VotingFromASearchStaysInTheSearch()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        await SubmitIdeaAsync(ana, "Sessões de partilha", "Descricao.");
        await ana.WaitForURLAsync(url => !url.EndsWith("/nova", StringComparison.Ordinal));

        await SubmitIdeaAsync(ana, "Bicicletas para todos", "Descricao.");
        await ana.WaitForURLAsync(url => !url.EndsWith("/nova", StringComparison.Ordinal));

        IPage plain = await OpenWithoutScriptAsAsync(People.BrunoObjectId);

        await plain.GotoAsync(Url("/?procura=sessoes"));
        await Expect(Rows(plain)).ToHaveCountAsync(1);

        await plain.Locator("[data-vote-button]").ClickAsync();

        // Without scripting this is a real round trip through the server's redirect, which
        // is the path that loses the term if nothing carries it.
        await Expect(Rows(plain)).ToHaveCountAsync(1);
        Assert.Contains("procura=sessoes", plain.Url, StringComparison.Ordinal);
    }


    /// <summary>
    /// The personal area shows what this person proposed and what they supported, and says
    /// the two different things when there is neither.
    /// </summary>
    [Fact]
    public async Task ThePersonalAreaSeparatesWhatWasProposedFromWhatWasSupported()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        // Nothing yet, and the two absences are named separately.
        await ana.GotoAsync(Url("/pessoal"));

        await Expect(ana.GetByText(UiText.NoIdeasSubmitted)).ToBeVisibleAsync();
        await Expect(ana.GetByText(UiText.NoIdeasSupported)).ToBeVisibleAsync();

        int ideaId = await SubmitIdeaAndReadIdAsync(ana, "Ideia da Ana", "Descricao.");

        IPage bruno = await OpenAsAsync(People.BrunoObjectId);

        await bruno.GotoAsync(Url($"/ideias/{ideaId}"));
        await bruno.Locator("[data-vote-button]").ClickAsync();
        await Expect(bruno.Locator("[data-vote-count]")).ToHaveTextAsync(UiText.FormatVoteCount(1));

        // Ana proposed one and supported none. Bruno supported one and proposed none.
        await ana.GotoAsync(Url("/pessoal"));

        await Expect(ana.GetByText(UiText.NoIdeasSubmitted)).ToHaveCountAsync(0);
        await Expect(ana.GetByText(UiText.NoIdeasSupported)).ToBeVisibleAsync();

        await bruno.GotoAsync(Url("/pessoal"));

        await Expect(bruno.GetByText(UiText.NoIdeasSubmitted)).ToBeVisibleAsync();
        await Expect(bruno.GetByText(UiText.NoIdeasSupported)).ToHaveCountAsync(0);
    }


    /// <summary>
    /// A vote on your idea shows up as news on every page, and opening the personal page is
    /// what makes it go away.
    ///
    /// Both halves matter. A notice that never clears stops meaning anything within a week,
    /// and one that clears on a timer claims to have been seen by somebody who was not
    /// there. This clears because a person went and looked.
    /// </summary>
    [Fact]
    public async Task AVoteOnYourIdeaIsNewsUntilYouGoAndLook()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        int ideaId = await SubmitIdeaAndReadIdAsync(ana, "Ideia da Ana", "Descricao.");

        // Nothing has happened to it yet.
        await ana.GotoAsync(Url("/"));
        await Expect(ana.GetByText(UiText.FormatIdeasWithNewVotes(1))).ToHaveCountAsync(0);

        IPage bruno = await OpenAsAsync(People.BrunoObjectId);

        await bruno.GotoAsync(Url($"/ideias/{ideaId}"));
        await bruno.Locator("[data-vote-button]").ClickAsync();
        await Expect(bruno.Locator("[data-vote-count]")).ToHaveTextAsync(UiText.FormatVoteCount(1));

        // The news reaches Ana on whatever page she is on, because it lives in the layout.
        await ana.GotoAsync(Url("/recentes"));
        await Expect(ana.GetByText(UiText.FormatIdeasWithNewVotes(1))).ToBeVisibleAsync();

        // And the idea itself says how many of its votes are new to her.
        await ana.GotoAsync(Url("/pessoal"));
        await Expect(ana.GetByText(UiText.FormatNewVotes(1))).ToBeVisibleAsync();

        // Having looked, it is no longer news, and it stays that way on a reload.
        await ana.GotoAsync(Url("/"));
        await Expect(ana.GetByText(UiText.FormatIdeasWithNewVotes(1))).ToHaveCountAsync(0);

        await ana.ReloadAsync();
        await Expect(ana.GetByText(UiText.FormatIdeasWithNewVotes(1))).ToHaveCountAsync(0);
    }


    /// <summary>
    /// A request the browser made on its own does not clear the news.
    ///
    /// The link to the personal page is in the layout, so it is on every page, and a
    /// browser that fetches it ahead of time would clear news that nobody read. Nothing
    /// errors, nothing is logged, and the person simply never finds out. The request here
    /// announces itself the way a real prefetch does.
    /// </summary>
    [Fact]
    public async Task APrefetchOfThePersonalPageDoesNotClearTheNews()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        int ideaId = await SubmitIdeaAndReadIdAsync(ana, "Ideia por descobrir", "Descricao.");

        IPage bruno = await OpenAsAsync(People.BrunoObjectId);

        await bruno.GotoAsync(Url($"/ideias/{ideaId}"));
        await bruno.Locator("[data-vote-button]").ClickAsync();
        await Expect(bruno.Locator("[data-vote-count]")).ToHaveTextAsync(UiText.FormatVoteCount(1));

        await ana.GotoAsync(Url("/"));
        await Expect(ana.GetByText(UiText.FormatIdeasWithNewVotes(1))).ToBeVisibleAsync();

        // The personal page is fetched exactly as a browser prefetching it would, carrying
        // Ana's own session, and never shown to her.
        IAPIResponse prefetch = await ana.Context.APIRequest.GetAsync(
            Url("/pessoal"),
            new APIRequestContextOptions
            {
                Headers = new Dictionary<string, string> { ["Sec-Purpose"] = "prefetch" },
            });

        Assert.True(prefetch.Ok, $"The prefetch answered {prefetch.Status}.");

        // She never looked, so it is still news.
        await ana.ReloadAsync();
        await Expect(ana.GetByText(UiText.FormatIdeasWithNewVotes(1))).ToBeVisibleAsync();
    }

    #endregion


    #region Helpers

    /// <summary>The rows of whichever list is on the page.</summary>
    private static ILocator Rows(IPage page)
    {
        return page.Locator("ul.lista-ideias > li");
    }

    #endregion
}
