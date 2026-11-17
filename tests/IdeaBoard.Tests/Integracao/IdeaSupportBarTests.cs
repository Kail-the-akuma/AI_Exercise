using System.Globalization;
using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using IdeaBoard.App.Services;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// How long each idea's support bar is, which is its own count measured against the leader
/// of the same list.
///
/// Every fixture here submits the ideas in the opposite order to the one their votes put
/// them in, so that the ranking and the recent tab disagree about which row comes first. A
/// fixture where the leader is also the newest idea cannot tell a bar measured against the
/// leader apart from a bar measured against the top row, and the second is wrong on the tab
/// that is not ordered by votes.
/// </summary>
public class IdeaSupportBarTests
{
    #region Fixtures

    /// <summary>One idea to seed, and how many different people voted on it.</summary>
    private sealed class IdeaSeed
    {
        /// <summary>Title, which is also how the assertions find the row again.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Number of distinct people who voted on it.</summary>
        public int VoteCount { get; set; }
    }


    private static CancellationToken Cancellation
    {
        get
        {
            return TestContext.Current.CancellationToken;
        }
    }


    /// <summary>
    /// Seeds the ideas in the given order, one minute apart, so the FIRST seed is the
    /// oldest, and casts each one's votes from as many different people as it asks for.
    /// One vote per person is the rule the database enforces, so the votes have to come
    /// from distinct voters and not from one person voting repeatedly.
    /// </summary>
    private static async Task<IdeaService> SeedAsync(IdeaBoardDbContext context, IReadOnlyList<IdeaSeed> seeds)
    {
        User author = TestData.NewUser("Ana Dias");
        context.Users.Add(author);

        int mostVotesWanted = 0;

        foreach (IdeaSeed seed in seeds)
        {
            if (seed.VoteCount > mostVotesWanted)
            {
                mostVotesWanted = seed.VoteCount;
            }
        }

        List<User> voters = new List<User>();

        for (int index = 0; index < mostVotesWanted; index++)
        {
            User voter = TestData.NewUser(
                string.Concat("Votante ", index.ToString(CultureInfo.InvariantCulture)));

            context.Users.Add(voter);
            voters.Add(voter);
        }

        await context.SaveChangesAsync(Cancellation);

        List<Idea> ideas = new List<Idea>();

        for (int index = 0; index < seeds.Count; index++)
        {
            Idea idea = TestData.NewIdea(author, seeds[index].Title, TestData.Anchor.AddMinutes(index));

            context.Ideas.Add(idea);
            ideas.Add(idea);
        }

        await context.SaveChangesAsync(Cancellation);

        for (int index = 0; index < seeds.Count; index++)
        {
            for (int voterIndex = 0; voterIndex < seeds[index].VoteCount; voterIndex++)
            {
                context.Votes.Add(
                    TestData.NewVote(ideas[index], voters[voterIndex], TestData.Anchor.AddMinutes(voterIndex)));
            }
        }

        await context.SaveChangesAsync(Cancellation);

        return new IdeaService(context, new FakeCurrentUser(author), TimeProvider.System);
    }


    /// <summary>Reads back the bar of one idea, by title.</summary>
    private static int BarOf(IReadOnlyList<IdeaListItem> items, string title)
    {
        foreach (IdeaListItem item in items)
        {
            if (string.Equals(item.Title, title, StringComparison.Ordinal))
            {
                return item.SupportPercent;
            }
        }

        Assert.Fail(string.Concat("The list carries no idea titled ", title, "."));

        return 0;
    }

    #endregion


    #region Proportion

    /// <summary>
    /// The leader fills the bar and everybody else is a fraction of it.
    ///
    /// The two middle values are asserted as 66 and 33 on purpose. Two thirds and one third
    /// of 3 votes are 66.6 and 33.3, and they are cut downwards rather than rounded, so a
    /// bar never draws more support than the idea actually has.
    /// </summary>
    [Fact]
    public async Task TheLeaderFillsTheBarAndTheOthersAreMeasuredAgainstIt()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        IdeaService service = await SeedAsync(
            context,
            [
                new IdeaSeed { Title = "Tres votos", VoteCount = 3 },
                new IdeaSeed { Title = "Dois votos", VoteCount = 2 },
                new IdeaSeed { Title = "Um voto", VoteCount = 1 },
                new IdeaSeed { Title = "Nenhum voto", VoteCount = 0 },
            ]);

        IReadOnlyList<IdeaListItem> ranking = await service.GetRankingAsync(null, Cancellation);

        Assert.Equal(100, BarOf(ranking, "Tres votos"));
        Assert.Equal(66, BarOf(ranking, "Dois votos"));
        Assert.Equal(33, BarOf(ranking, "Um voto"));
        Assert.Equal(0, BarOf(ranking, "Nenhum voto"));
    }


    /// <summary>
    /// A tie at the top fills the bar for every idea tied there, and the rest stay measured
    /// against that same number. There is one leading count, not one leading row.
    /// </summary>
    [Fact]
    public async Task ATieAtTheTopFillsTheBarForEveryIdeaTiedThere()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        IdeaService service = await SeedAsync(
            context,
            [
                new IdeaSeed { Title = "Empatada primeira", VoteCount = 2 },
                new IdeaSeed { Title = "Empatada segunda", VoteCount = 2 },
                new IdeaSeed { Title = "Metade", VoteCount = 1 },
            ]);

        IReadOnlyList<IdeaListItem> ranking = await service.GetRankingAsync(null, Cancellation);

        Assert.Equal(100, BarOf(ranking, "Empatada primeira"));
        Assert.Equal(100, BarOf(ranking, "Empatada segunda"));
        Assert.Equal(50, BarOf(ranking, "Metade"));
    }

    #endregion


    #region Degenerate lists

    /// <summary>
    /// With nothing voted on anywhere, every bar is empty.
    ///
    /// This is the list that divides by zero if the denominator is taken without looking,
    /// and it is also the list the product opens on, before anybody has voted at all. An
    /// empty bar is the honest picture: with no leader there is nothing to measure against,
    /// and three full bars would say every idea is leading.
    /// </summary>
    [Fact]
    public async Task WithNothingVotedOnEveryBarIsEmpty()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        IdeaService service = await SeedAsync(
            context,
            [
                new IdeaSeed { Title = "Primeira", VoteCount = 0 },
                new IdeaSeed { Title = "Segunda", VoteCount = 0 },
                new IdeaSeed { Title = "Terceira", VoteCount = 0 },
            ]);

        IReadOnlyList<IdeaListItem> ranking = await service.GetRankingAsync(null, Cancellation);

        Assert.Equal(3, ranking.Count);

        foreach (IdeaListItem item in ranking)
        {
            Assert.Equal(0, item.SupportPercent);
        }
    }


    /// <summary>An empty list is answered without going looking for a leader.</summary>
    [Fact]
    public async Task AnEmptyListIsAnsweredWithoutLookingForALeader()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        IdeaService service = await SeedAsync(context, []);

        IReadOnlyList<IdeaListItem> ranking = await service.GetRankingAsync(null, Cancellation);

        Assert.Empty(ranking);
    }

    #endregion


    #region Both tabs

    /// <summary>
    /// The recent tab measures against the leader of the list and not against its own first
    /// row.
    ///
    /// This is the assertion the whole fixture is built for. On the recent tab the newest
    /// idea comes first whatever its support, so an implementation that takes the
    /// denominator from the top row gives that idea a full bar and still passes every
    /// assertion made on the ranking. Here the newest idea has one vote out of four, and a
    /// full bar on it is the defect.
    /// </summary>
    [Fact]
    public async Task TheRecentTabMeasuresAgainstTheLeaderAndNotAgainstItsFirstRow()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        IdeaService service = await SeedAsync(
            context,
            [
                new IdeaSeed { Title = "Antiga e apoiada", VoteCount = 4 },
                new IdeaSeed { Title = "Recente e sozinha", VoteCount = 1 },
            ]);

        IReadOnlyList<IdeaListItem> recent = await service.GetRecentAsync(null, Cancellation);

        Assert.Equal("Recente e sozinha", recent[0].Title);
        Assert.Equal(25, recent[0].SupportPercent);
        Assert.Equal(100, BarOf(recent, "Antiga e apoiada"));

        // And the same idea carries the same bar on both tabs, because the two lists hold
        // the same ideas and the leader belongs to the set and not to the order.
        IReadOnlyList<IdeaListItem> ranking = await service.GetRankingAsync(null, Cancellation);

        Assert.Equal(BarOf(ranking, "Antiga e apoiada"), BarOf(recent, "Antiga e apoiada"));
        Assert.Equal(BarOf(ranking, "Recente e sozinha"), BarOf(recent, "Recente e sozinha"));
    }

    #endregion
}
