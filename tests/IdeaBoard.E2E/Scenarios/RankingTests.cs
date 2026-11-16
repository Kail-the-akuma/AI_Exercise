using IdeaBoard.E2E.Fixtures;
using Microsoft.Playwright;
using Xunit;

namespace IdeaBoard.E2E.Scenarios;

/// <summary>
/// The two tabs over the same list, and the order each of them is specified to show.
/// </summary>
[Collection(AppHostCollection.Name)]
public class RankingTests : ScenarioBase
{
    #region Construction

    /// <inheritdoc />
    public RankingTests(AppHostFixture host)
        : base(host)
    {
    }

    #endregion


    #region Scenarios

    /// <summary>
    /// The main tab orders by support, and breaks a tie in favour of the more recent idea.
    ///
    /// Three ideas and two people, arranged so that no accident produces the right answer.
    /// The oldest idea has two votes, and the two newer ones have one each, so the ranking
    /// has to be oldest, newest, middle. That order matches neither the order the ideas
    /// were submitted in, nor the order the other tab shows, nor the reverse of either.
    ///
    /// It also pins the direction of the tie break, which is the part of the rule that is
    /// a deliberate product decision and not a technical detail. A tie broken the other
    /// way, in favour of the older idea, would put the middle one before the newest, and
    /// that order is the submission order, which is exactly the shape a careless
    /// implementation falls into.
    ///
    /// The third criterion, the identifier, is not covered. Reaching it needs two ideas
    /// with the same submission instant, which the application cannot be asked to produce
    /// from outside, and the specification itself says it is never visible in practice.
    /// </summary>
    [Fact]
    public async Task TheRankingOrdersByVotesAndBreaksTiesInFavourOfTheMoreRecent()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        int oldest = await SubmitIdeaAndReadIdAsync(ana, "Ideia mais antiga", "Descricao.");
        int middle = await SubmitIdeaAndReadIdAsync(ana, "Ideia do meio", "Descricao.");
        int newest = await SubmitIdeaAndReadIdAsync(ana, "Ideia mais recente", "Descricao.");

        await VoteAsync(ana, oldest);
        await VoteAsync(ana, middle);
        await VoteAsync(ana, newest);

        IPage bruno = await OpenAsAsync(People.BrunoObjectId);

        await VoteAsync(bruno, oldest);

        await ana.GotoAsync(Url("/"));

        ILocator ranked = ana.Locator("ul.lista-ideias > li");

        await Expect(ranked).ToHaveCountAsync(3);

        await Expect(ranked.Nth(0)).ToContainTextAsync("Ideia mais antiga");
        await Expect(ranked.Nth(0)).ToContainTextAsync("2 votos");

        await Expect(ranked.Nth(1)).ToContainTextAsync("Ideia mais recente");
        await Expect(ranked.Nth(1)).ToContainTextAsync("1 voto");

        await Expect(ranked.Nth(2)).ToContainTextAsync("Ideia do meio");
        await Expect(ranked.Nth(2)).ToContainTextAsync("1 voto");

        // The other tab, on the same three ideas, in a different order. Asserted here and
        // not only in its own scenario, because two tabs that agreed would mean the main
        // one is not ordering by support at all.
        await ana.GotoAsync(Url("/recentes"));

        ILocator recent = ana.Locator("ul.lista-ideias > li");

        await Expect(recent).ToHaveCountAsync(3);

        await Expect(recent.Nth(0)).ToContainTextAsync("Ideia mais recente");
        await Expect(recent.Nth(1)).ToContainTextAsync("Ideia do meio");
        await Expect(recent.Nth(2)).ToContainTextAsync("Ideia mais antiga");
    }


    /// <summary>
    /// The ranking moves when a vote is cast, from the same screen the vote was cast on.
    ///
    /// A list that is ordered correctly when it is built, and never again, is ordered
    /// correctly in every scenario that arranges the data first and looks afterwards.
    /// This one votes from inside the list and then asks for the list again, which is the
    /// sequence a person actually performs.
    /// </summary>
    [Fact]
    public async Task VotingFromTheListChangesWhereTheIdeaSitsInIt()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        int first = await SubmitIdeaAndReadIdAsync(ana, "Submetida primeiro", "Descricao.");

        await SubmitIdeaAndReadIdAsync(ana, "Submetida depois", "Descricao.");

        await ana.GotoAsync(Url("/"));

        ILocator ranked = ana.Locator("ul.lista-ideias > li");

        // With no votes anywhere, the tie break alone decides, and the newer one leads.
        await Expect(ranked.Nth(0)).ToContainTextAsync("Submetida depois");

        await ana.Locator($"#ideia-{first} [data-vote-button]").ClickAsync();

        await Expect(ana.Locator($"#ideia-{first} [data-vote-count]")).ToHaveTextAsync("1 voto");

        // The position does not change under the person's hands, by design: the script
        // rewrites the button and the count and nothing else. The order is the server's
        // answer to the next request for the list.
        await ana.GotoAsync(Url("/"));

        await Expect(ana.Locator("ul.lista-ideias > li").Nth(0))
            .ToContainTextAsync("Submetida primeiro");
    }

    #endregion


    #region Helpers

    /// <summary>Votes on one idea from its own page.</summary>
    private async Task VoteAsync(IPage page, int ideaId)
    {
        await page.GotoAsync(Url($"/ideias/{ideaId}"));

        await page.Locator("[data-vote-button]").ClickAsync();

        // Waited for on purpose. Leaving the page while the request is still on its way
        // would leave the next step racing a vote that may not have been recorded.
        await Expect(page.Locator("[data-vote-button]")).ToHaveTextAsync("Votado");
    }

    #endregion
}
