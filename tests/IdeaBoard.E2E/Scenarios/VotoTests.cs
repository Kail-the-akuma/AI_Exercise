using IdeaBoard.App.Data;
using IdeaBoard.App.Pages;
using IdeaBoard.E2E.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit;

namespace IdeaBoard.E2E.Scenarios;

/// <summary>
/// Voting, withdrawing a vote, and the rule the whole product rests on: one vote per
/// person, however many times the request arrives.
///
/// The counts are asserted as the literal words the specification fixes, and not through
/// the application's own formatter. Reading the number back from the code that wrote it
/// would make every assertion here agree with whatever that code happens to say.
/// </summary>
[Collection(AppHostCollection.Name)]
public class VotoTests : ScenarioBase
{
    #region Constants

    private const string NoVotes = "Sem votos";
    private const string OneVote = "1 voto";
    private const string TwoVotes = "2 votos";

    #endregion


    #region Construction

    /// <inheritdoc />
    public VotoTests(AppHostFixture host)
        : base(host)
    {
    }

    #endregion


    #region Scenarios

    /// <summary>
    /// A vote is still there after leaving the page and coming back, and is still there on
    /// a different page.
    ///
    /// The second half is what makes this more than a test of the script. An in place
    /// update proves only that something was written on screen, and a vote that never
    /// reached the database looks exactly the same until the page is loaded again.
    /// </summary>
    [Fact]
    public async Task AVoteSurvivesLeavingThePageAndComingBack()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        int ideaId = await SubmitIdeaAndReadIdAsync(ana, "Ideia para votar", "Descricao.");

        await Expect(VoteCount(ana)).ToHaveTextAsync(NoVotes);
        await Expect(VoteButton(ana)).ToHaveTextAsync(UiText.VoteButton);

        await VoteButton(ana).ClickAsync();

        await Expect(VoteCount(ana)).ToHaveTextAsync(OneVote);
        await Expect(VoteButton(ana)).ToHaveTextAsync(UiText.VotedButton);

        // A fresh render by the server, on another page, from another request.
        await ana.GotoAsync(Url("/"));

        ILocator row = ana.Locator($"#ideia-{ideaId}");

        await Expect(row.Locator("[data-vote-count]")).ToHaveTextAsync(OneVote);
        await Expect(row.Locator("[data-vote-button]")).ToHaveTextAsync(UiText.VotedButton);

        Assert.Equal(1, await CountVotesAsync());
    }


    /// <summary>
    /// The number that appears after voting is the one the server counted, and not
    /// arithmetic done in the browser on a number that has since gone stale.
    ///
    /// The construction is the point. Ana's page has been sitting on zero while Bruno
    /// voted somewhere else, so when Ana votes the right answer is two. Any count worked
    /// out locally, from the zero on Ana's screen, can only produce one. The assertion is
    /// therefore unreachable except by taking the number the server sent, which is what
    /// makes it a real check rather than a restatement of what the page already said.
    /// </summary>
    [Fact]
    public async Task TheCountAfterVotingIsTheServersAndNotArithmeticOnAStalePage()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        int ideaId = await SubmitIdeaAndReadIdAsync(ana, "Ideia disputada", "Descricao.");

        await Expect(VoteCount(ana)).ToHaveTextAsync(NoVotes);

        IPage bruno = await OpenAsAsync(People.BrunoObjectId);

        await bruno.GotoAsync(DetailUrl(ideaId));
        await VoteButton(bruno).ClickAsync();

        await Expect(VoteCount(bruno)).ToHaveTextAsync(OneVote);

        // Ana's page has not been touched and still says nobody voted.
        await Expect(VoteCount(ana)).ToHaveTextAsync(NoVotes);

        await VoteButton(ana).ClickAsync();

        await Expect(VoteCount(ana)).ToHaveTextAsync(TwoVotes);

        Assert.Equal(2, await CountVotesAsync());
    }


    /// <summary>
    /// The same thing on the way down, because a count can be the server's going up and
    /// the browser's coming back.
    ///
    /// Ana's page says two while Bruno has already withdrawn. When Ana withdraws, the
    /// right answer is none at all. Subtracting one from the two on her screen gives one,
    /// so the assertion again cannot be satisfied by local arithmetic.
    /// </summary>
    [Fact]
    public async Task TheCountAfterWithdrawingIsAlsoTheServers()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        int ideaId = await SubmitIdeaAndReadIdAsync(ana, "Ideia a esvaziar", "Descricao.");

        await VoteButton(ana).ClickAsync();
        await Expect(VoteCount(ana)).ToHaveTextAsync(OneVote);

        IPage bruno = await OpenAsAsync(People.BrunoObjectId);

        await bruno.GotoAsync(DetailUrl(ideaId));
        await VoteButton(bruno).ClickAsync();
        await Expect(VoteCount(bruno)).ToHaveTextAsync(TwoVotes);

        // Ana sees the two, and from here her page goes stale.
        await ana.ReloadAsync();
        await Expect(VoteCount(ana)).ToHaveTextAsync(TwoVotes);
        await Expect(VoteButton(ana)).ToHaveTextAsync(UiText.VotedButton);

        await VoteButton(bruno).ClickAsync();
        await Expect(VoteCount(bruno)).ToHaveTextAsync(OneVote);

        await VoteButton(ana).ClickAsync();

        await Expect(VoteCount(ana)).ToHaveTextAsync(NoVotes);
        await Expect(VoteButton(ana)).ToHaveTextAsync(UiText.VoteButton);

        Assert.Equal(0, await CountVotesAsync());
    }


    /// <summary>
    /// A second tab left open on the same idea, pressed after the first one already voted,
    /// does not produce a second vote and does not produce an error.
    ///
    /// This is two tabs on the same page, one of the three failure modes ADR-010 names.
    /// The second tab still shows the button that votes, so pressing it asks the server to
    /// vote again, which is exactly the request the rule has to absorb.
    ///
    /// What this does NOT establish is which mechanism absorbs it. The two requests are
    /// sequential, so the unique index's race window is never entered, and a plain check
    /// in application code would pass this scenario identically. ADR-010 says the index is
    /// what makes it correct, and that claim belongs to the integration level, not here.
    /// </summary>
    [Fact]
    public async Task ASecondStaleTabVotingAgainLeavesOneVoteAndNoError()
    {
        await StartCleanAsync();

        IPage first = await OpenAsAsync(People.AnaObjectId);

        int ideaId = await SubmitIdeaAndReadIdAsync(
            first,
            "Ideia em dois separadores",
            "Descricao.");

        IPage second = await first.Context.NewPageAsync();

        await second.GotoAsync(DetailUrl(ideaId));
        await Expect(VoteButton(second)).ToHaveTextAsync(UiText.VoteButton);

        await VoteButton(first).ClickAsync();
        await Expect(VoteCount(first)).ToHaveTextAsync(OneVote);

        // The second tab knows nothing about that, and asks to vote.
        await VoteButton(second).ClickAsync();

        await Expect(VoteCount(second)).ToHaveTextAsync(OneVote);
        await Expect(VoteButton(second)).ToHaveTextAsync(UiText.VotedButton);

        // Nothing was shown to the person that looks like a failure. A refused request
        // would have sent the script back to an ordinary post, which navigates away, so
        // the address still being the idea's own page is the evidence here, rather than
        // the absence of some particular word on screen.
        await Expect(second).ToHaveURLAsync(DetailUrl(ideaId));

        Assert.Equal(1, await CountVotesAsync());

        await second.ReloadAsync();

        await Expect(second.Locator("ul.lista-votantes > li")).ToHaveCountAsync(1);
    }


    /// <summary>
    /// A double click, with the answer to the first press still on its way, leaves one
    /// vote.
    ///
    /// The answer is held back deliberately. Without that, the two halves of a double
    /// click race the server over a loopback address: the first answer can land in
    /// between, and the second press is then a deliberate withdrawal rather than a
    /// duplicate, which is correct behaviour and a different scenario. Holding the answer
    /// pins the situation ADR-010 actually describes, and removes a race that would
    /// otherwise make this scenario report different results on different days.
    /// </summary>
    [Fact]
    public async Task ADoubleClickWhileTheAnswerIsOnItsWayLeavesOneVote()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        int ideaId = await SubmitIdeaAndReadIdAsync(ana, "Ideia de duplo clique", "Descricao.");

        CancellationToken cancellation = TestContext.Current.CancellationToken;

        await ana.RouteAsync("**", async route =>
        {
            if (route.Request.Method == "POST")
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellation);
            }

            await route.ContinueAsync();
        });

        await VoteButton(ana).DblClickAsync();

        await Expect(VoteCount(ana)).ToHaveTextAsync(
            OneVote,
            new LocatorAssertionsToHaveTextOptions { Timeout = 15000 });

        await Expect(VoteButton(ana)).ToHaveTextAsync(UiText.VotedButton);

        Assert.Equal(1, await CountVotesAsync());

        await ana.UnrouteAsync("**");
        await ana.ReloadAsync();

        await Expect(ana.Locator("ul.lista-votantes > li")).ToHaveCountAsync(1);
    }


    /// <summary>
    /// The same vote request sent twice leaves one vote and answers as success both times.
    ///
    /// This one does not go through the browser's buttons, because the specification does
    /// not only say a double click has to be absorbed. It says a duplicated request has to
    /// be, and a request that arrives twice is not something any sequence of clicks can be
    /// relied on to produce. The second answer being a success and not an error is half
    /// the rule: the person asked for a state, and the state is the one they asked for.
    /// </summary>
    [Fact]
    public async Task ARepeatedVoteRequestLeavesOneVoteAndAnswersAsSuccess()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        int ideaId = await SubmitIdeaAndReadIdAsync(ana, "Ideia pedida duas vezes", "Descricao.");

        string token = await ReadAntiforgeryTokenAsync(ana);

        Assert.NotEqual(string.Empty, token);

        IAPIResponse firstAnswer = await PostVoteAsync(ana, ideaId, token);
        IAPIResponse secondAnswer = await PostVoteAsync(ana, ideaId, token);

        Assert.True(firstAnswer.Ok, $"First vote answered {firstAnswer.Status}.");
        Assert.True(secondAnswer.Ok, $"Repeated vote answered {secondAnswer.Status}.");

        Assert.Equal(1, await CountVotesAsync());

        await ana.ReloadAsync();

        await Expect(VoteCount(ana)).ToHaveTextAsync(OneVote);
        await Expect(VoteButton(ana)).ToHaveTextAsync(UiText.VotedButton);
        await Expect(ana.Locator("ul.lista-votantes > li")).ToHaveCountAsync(1);
    }


    /// <summary>
    /// Two people voting on the same idea are counted once each, and both appear by name,
    /// oldest vote first.
    ///
    /// The assertion that carries the per person rule is the one in the middle: Bruno's
    /// button offers to vote while the count beside it already says one. A count that was
    /// global, or a voted state read from the idea rather than from the person, would show
    /// Bruno a button saying he had voted for a vote he never cast.
    /// </summary>
    [Fact]
    public async Task TwoPeopleVotingAreCountedOnceEachAndListedByName()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        int ideaId = await SubmitIdeaAndReadIdAsync(ana, "Ideia de duas pessoas", "Descricao.");

        await VoteButton(ana).ClickAsync();
        await Expect(VoteCount(ana)).ToHaveTextAsync(OneVote);

        IPage bruno = await OpenAsAsync(People.BrunoObjectId);

        await bruno.GotoAsync(DetailUrl(ideaId));

        await Expect(VoteCount(bruno)).ToHaveTextAsync(OneVote);
        await Expect(VoteButton(bruno)).ToHaveTextAsync(UiText.VoteButton);

        await VoteButton(bruno).ClickAsync();
        await Expect(VoteCount(bruno)).ToHaveTextAsync(TwoVotes);

        // The nominal list, from a fresh render, in the order the specification fixes.
        await bruno.ReloadAsync();

        ILocator voters = bruno.Locator("ul.lista-votantes > li");

        await Expect(voters).ToHaveCountAsync(2);
        await Expect(voters.Nth(0)).ToContainTextAsync(People.AnaDisplayName);
        await Expect(voters.Nth(1)).ToContainTextAsync(People.BrunoDisplayName);

        // And Ana's own vote is untouched by somebody else's.
        await ana.ReloadAsync();

        await Expect(VoteCount(ana)).ToHaveTextAsync(TwoVotes);
        await Expect(VoteButton(ana)).ToHaveTextAsync(UiText.VotedButton);

        Assert.Equal(2, await CountVotesAsync());
    }

    #endregion


    #region Helpers

    /// <summary>The vote button on the page, wherever the page puts it.</summary>
    private static ILocator VoteButton(IPage page)
    {
        return page.Locator("[data-vote-button]");
    }


    /// <summary>The count beside the vote button.</summary>
    private static ILocator VoteCount(IPage page)
    {
        return page.Locator("[data-vote-count]");
    }


    /// <summary>Reads the antiforgery token the rendered form carries.</summary>
    private static async Task<string> ReadAntiforgeryTokenAsync(IPage page)
    {
        string selector = "input[name='__RequestVerificationToken']";

        return await page.Locator(selector).First.GetAttributeAsync("value") ?? string.Empty;
    }


    /// <summary>Address of one idea's own page.</summary>
    private string DetailUrl(int ideaId)
    {
        return Url($"/ideias/{ideaId}");
    }


    /// <summary>Posts a vote the way the form does, without going through the button.</summary>
    private async Task<IAPIResponse> PostVoteAsync(IPage page, int ideaId, string token)
    {
        IFormData form = page.Context.APIRequest.CreateFormData();

        form.Set("ideaId", ideaId);
        form.Set("__RequestVerificationToken", token);

        return await page.Context.APIRequest.PostAsync(
            DetailUrl(ideaId) + "?handler=" + VotingPageModel.VoteHandlerName,
            new APIRequestContextOptions { Form = form });
    }


    /// <summary>
    /// Counts the votes in the database, which is the only place a vote either exists or
    /// does not. A count read from a page is the page's claim about itself.
    /// </summary>
    private async Task<int> CountVotesAsync()
    {
        using IdeaBoardDbContext context = Host.CreateContext();

        return await context.Votes.CountAsync(TestContext.Current.CancellationToken);
    }

    #endregion
}
