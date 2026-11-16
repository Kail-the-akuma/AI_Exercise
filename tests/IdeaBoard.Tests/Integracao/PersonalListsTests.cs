using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using IdeaBoard.App.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// The two lists behind the personal tab: what a person submitted, and what they voted on.
///
/// Every fixture here has at least two people with ideas and votes, because a list that
/// filters by nobody returns the right answer in a database where only one person exists.
/// </summary>
public class PersonalListsTests
{
    #region Helpers

    private static CancellationToken Cancellation
    {
        get
        {
            return TestContext.Current.CancellationToken;
        }
    }


    private static IReadOnlyList<string> TitlesOf(IReadOnlyList<IdeaListItem> items)
    {
        List<string> titles = new List<string>();

        foreach (IdeaListItem item in items)
        {
            titles.Add(item.Title);
        }

        return titles;
    }

    #endregion


    #region What belongs to whom

    /// <summary>
    /// Each person's list holds their own ideas and nobody else's, and the two people in
    /// the fixture have one each so that a list that forgot to filter looks right in size
    /// and wrong in content.
    /// </summary>
    [Fact]
    public async Task EachPersonSeesOnlyTheIdeasTheySubmitted()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User ana = TestData.NewUser("Ana Dias");
        User bruno = TestData.NewUser("Bruno Lima");

        context.Users.Add(ana);
        context.Users.Add(bruno);
        await context.SaveChangesAsync(Cancellation);

        context.Ideas.Add(TestData.NewIdea(ana, "Ideia da Ana", TestData.Anchor));
        context.Ideas.Add(TestData.NewIdea(bruno, "Ideia do Bruno", TestData.Anchor.AddMinutes(1)));
        await context.SaveChangesAsync(Cancellation);

        FakeCurrentUser signedIn = new FakeCurrentUser(ana);
        IdeaService service = new IdeaService(context, signedIn, TimeProvider.System);

        Assert.Equal(["Ideia da Ana"], TitlesOf(await service.GetMineAsync(Cancellation)));

        signedIn.SignInAs(bruno);

        Assert.Equal(["Ideia do Bruno"], TitlesOf(await service.GetMineAsync(Cancellation)));
    }


    /// <summary>
    /// The submitted list is ordered by date, most recent first. The fixture inserts the
    /// rows in the opposite order to their dates, so an implementation ordering by
    /// identifier returns this list backwards.
    /// </summary>
    [Fact]
    public async Task TheSubmittedListIsOrderedByDateAndNotByIdentifier()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User ana = TestData.NewUser("Ana Dias");
        context.Users.Add(ana);
        await context.SaveChangesAsync(Cancellation);

        context.Ideas.Add(TestData.NewIdea(ana, "Escrita primeiro, datada depois", TestData.Anchor.AddMinutes(10)));
        context.Ideas.Add(TestData.NewIdea(ana, "Escrita depois, datada antes", TestData.Anchor));
        await context.SaveChangesAsync(Cancellation);

        IdeaService service = new IdeaService(context, new FakeCurrentUser(ana), TimeProvider.System);

        Assert.Equal(
            ["Escrita primeiro, datada depois", "Escrita depois, datada antes"],
            TitlesOf(await service.GetMineAsync(Cancellation)));
    }

    #endregion


    #region What was voted on

    /// <summary>
    /// The voted list holds exactly what this person voted on, and is ordered by the VOTE
    /// and not by the idea.
    ///
    /// The fixture makes the two orders disagree completely: the oldest idea is voted on
    /// last. A list ordered by the idea's date comes back inverted, and a list ordered by
    /// the vote's date comes back as asserted here.
    /// </summary>
    [Fact]
    public async Task TheVotedListFollowsTheOrderOfTheVotesAndNotOfTheIdeas()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User ana = TestData.NewUser("Ana Dias");
        User bruno = TestData.NewUser("Bruno Lima");

        context.Users.Add(ana);
        context.Users.Add(bruno);
        await context.SaveChangesAsync(Cancellation);

        Idea oldest = TestData.NewIdea(bruno, "Ideia antiga", TestData.Anchor);
        Idea newest = TestData.NewIdea(bruno, "Ideia recente", TestData.Anchor.AddMinutes(10));
        Idea untouched = TestData.NewIdea(bruno, "Ideia sem voto da Ana", TestData.Anchor.AddMinutes(5));

        context.Ideas.Add(oldest);
        context.Ideas.Add(newest);
        context.Ideas.Add(untouched);
        await context.SaveChangesAsync(Cancellation);

        // Ana votes on the NEWER idea first and on the OLDER one afterwards.
        context.Votes.Add(TestData.NewVote(newest, ana, TestData.Anchor.AddHours(1)));
        context.Votes.Add(TestData.NewVote(oldest, ana, TestData.Anchor.AddHours(2)));
        context.Votes.Add(TestData.NewVote(untouched, bruno, TestData.Anchor.AddHours(3)));
        await context.SaveChangesAsync(Cancellation);

        IdeaService service = new IdeaService(context, new FakeCurrentUser(ana), TimeProvider.System);

        Assert.Equal(
            ["Ideia antiga", "Ideia recente"],
            TitlesOf(await service.GetVotedByMeAsync(Cancellation)));
    }


    /// <summary>
    /// Every row on the voted list says the person has voted, without a query being spent
    /// to establish what the filter already guarantees.
    /// </summary>
    [Fact]
    public async Task EveryIdeaOnTheVotedListIsMarkedAsVoted()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User ana = TestData.NewUser("Ana Dias");
        User bruno = TestData.NewUser("Bruno Lima");

        context.Users.Add(ana);
        context.Users.Add(bruno);
        await context.SaveChangesAsync(Cancellation);

        Idea supported = TestData.NewIdea(bruno, "Ideia apoiada", TestData.Anchor);
        context.Ideas.Add(supported);
        await context.SaveChangesAsync(Cancellation);

        context.Votes.Add(TestData.NewVote(supported, ana, TestData.Anchor.AddMinutes(1)));
        await context.SaveChangesAsync(Cancellation);

        IdeaService service = new IdeaService(context, new FakeCurrentUser(ana), TimeProvider.System);

        IReadOnlyList<IdeaListItem> voted = await service.GetVotedByMeAsync(Cancellation);

        IdeaListItem only = Assert.Single(voted);

        Assert.True(only.HasVoted);
        Assert.Equal(1, only.VoteCount);
    }


    /// <summary>
    /// Withdrawing a vote takes the idea off the list, because withdrawing deletes the row
    /// and this list is built from the rows that exist.
    /// </summary>
    [Fact]
    public async Task WithdrawingAVoteTakesTheIdeaOffTheVotedList()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User ana = TestData.NewUser("Ana Dias");
        User bruno = TestData.NewUser("Bruno Lima");

        context.Users.Add(ana);
        context.Users.Add(bruno);
        await context.SaveChangesAsync(Cancellation);

        Idea idea = TestData.NewIdea(bruno, "Ideia que a Ana deixa de apoiar", TestData.Anchor);
        context.Ideas.Add(idea);
        await context.SaveChangesAsync(Cancellation);

        Vote vote = TestData.NewVote(idea, ana, TestData.Anchor.AddMinutes(1));
        context.Votes.Add(vote);
        await context.SaveChangesAsync(Cancellation);

        IdeaService service = new IdeaService(context, new FakeCurrentUser(ana), TimeProvider.System);

        Assert.Single(await service.GetVotedByMeAsync(Cancellation));

        context.Votes.Remove(vote);
        await context.SaveChangesAsync(Cancellation);

        Assert.Empty(await service.GetVotedByMeAsync(Cancellation));
    }

    #endregion


    #region Where the two lists meet

    /// <summary>
    /// An idea somebody wrote AND voted on is on both lists, and the data says so plainly
    /// rather than picking one. Which of the two the page shows it in, or whether it shows
    /// it twice, is a decision about the page.
    /// </summary>
    [Fact]
    public async Task AnIdeaSomebodyWroteAndVotedOnIsOnBothLists()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User ana = TestData.NewUser("Ana Dias");
        context.Users.Add(ana);
        await context.SaveChangesAsync(Cancellation);

        Idea own = TestData.NewIdea(ana, "Ideia da Ana, votada pela Ana", TestData.Anchor);
        context.Ideas.Add(own);
        await context.SaveChangesAsync(Cancellation);

        context.Votes.Add(TestData.NewVote(own, ana, TestData.Anchor.AddMinutes(1)));
        await context.SaveChangesAsync(Cancellation);

        IdeaService service = new IdeaService(context, new FakeCurrentUser(ana), TimeProvider.System);

        Assert.Equal(["Ideia da Ana, votada pela Ana"], TitlesOf(await service.GetMineAsync(Cancellation)));
        Assert.Equal(["Ideia da Ana, votada pela Ana"], TitlesOf(await service.GetVotedByMeAsync(Cancellation)));
    }


    /// <summary>
    /// The two ways of being empty are told apart, which is what the page needs in order to
    /// say two different things. The person in this fixture has submitted something and
    /// voted on nothing, and the fixture is then inverted on the other person.
    /// </summary>
    [Fact]
    public async Task TheTwoWaysOfBeingEmptyAreTellableApart()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User writer = TestData.NewUser("Ana Dias");
        User voter = TestData.NewUser("Bruno Lima");

        context.Users.Add(writer);
        context.Users.Add(voter);
        await context.SaveChangesAsync(Cancellation);

        Idea idea = TestData.NewIdea(writer, "A única ideia", TestData.Anchor);
        context.Ideas.Add(idea);
        await context.SaveChangesAsync(Cancellation);

        context.Votes.Add(TestData.NewVote(idea, voter, TestData.Anchor.AddMinutes(1)));
        await context.SaveChangesAsync(Cancellation);

        FakeCurrentUser signedIn = new FakeCurrentUser(writer);
        IdeaService service = new IdeaService(context, signedIn, TimeProvider.System);

        Assert.Single(await service.GetMineAsync(Cancellation));
        Assert.Empty(await service.GetVotedByMeAsync(Cancellation));

        signedIn.SignInAs(voter);

        Assert.Empty(await service.GetMineAsync(Cancellation));
        Assert.Single(await service.GetVotedByMeAsync(Cancellation));
    }

    #endregion


    #region Bars on a subset

    /// <summary>
    /// The bars on the personal tab are measured against the leading idea of the WHOLE
    /// table, and not against the best idea this person happens to have.
    ///
    /// This is the case a comment in the service foresaw and the first list to show a
    /// subset. The fixture gives this person a single idea with one vote while somebody
    /// else leads with four, so a tab that measured against its own rows would draw a full
    /// bar and tell this person they are leading.
    /// </summary>
    [Fact]
    public async Task TheBarsOnAPersonalListMeasureAgainstTheWholeTable()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User ana = TestData.NewUser("Ana Dias");
        User bruno = TestData.NewUser("Bruno Lima");
        User carla = TestData.NewUser("Carla Nunes");
        User diogo = TestData.NewUser("Diogo Sá");
        User eva = TestData.NewUser("Eva Pinto");

        context.Users.Add(ana);
        context.Users.Add(bruno);
        context.Users.Add(carla);
        context.Users.Add(diogo);
        context.Users.Add(eva);
        await context.SaveChangesAsync(Cancellation);

        Idea hers = TestData.NewIdea(ana, "Ideia da Ana", TestData.Anchor);
        Idea leading = TestData.NewIdea(bruno, "Ideia que lidera", TestData.Anchor.AddMinutes(1));

        context.Ideas.Add(hers);
        context.Ideas.Add(leading);
        await context.SaveChangesAsync(Cancellation);

        context.Votes.Add(TestData.NewVote(hers, bruno, TestData.Anchor.AddMinutes(2)));

        context.Votes.Add(TestData.NewVote(leading, bruno, TestData.Anchor.AddMinutes(3)));
        context.Votes.Add(TestData.NewVote(leading, carla, TestData.Anchor.AddMinutes(4)));
        context.Votes.Add(TestData.NewVote(leading, diogo, TestData.Anchor.AddMinutes(5)));
        context.Votes.Add(TestData.NewVote(leading, eva, TestData.Anchor.AddMinutes(6)));
        await context.SaveChangesAsync(Cancellation);

        IdeaService service = new IdeaService(context, new FakeCurrentUser(ana), TimeProvider.System);

        IdeaListItem onlyMine = Assert.Single(await service.GetMineAsync(Cancellation));

        Assert.Equal(1, onlyMine.VoteCount);
        Assert.Equal(25, onlyMine.SupportPercent);

        // And the ranking, which holds every idea, agrees about the same idea. The two
        // numbers coming from different code paths is exactly why this is asserted.
        IReadOnlyList<IdeaListItem> ranking = await service.GetRankingAsync(null, Cancellation);

        foreach (IdeaListItem item in ranking)
        {
            if (item.Id == hers.Id)
            {
                Assert.Equal(onlyMine.SupportPercent, item.SupportPercent);
            }
        }
    }


    /// <summary>
    /// With nothing voted on anywhere, a personal list comes back with empty bars and no
    /// division by zero, which is the state of the whole team on the first day.
    /// </summary>
    [Fact]
    public async Task WithNothingVotedOnAnywhereThePersonalBarsAreEmpty()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User ana = TestData.NewUser("Ana Dias");
        context.Users.Add(ana);
        await context.SaveChangesAsync(Cancellation);

        context.Ideas.Add(TestData.NewIdea(ana, "Primeira ideia", TestData.Anchor));
        context.Ideas.Add(TestData.NewIdea(ana, "Segunda ideia", TestData.Anchor.AddMinutes(1)));
        await context.SaveChangesAsync(Cancellation);

        IdeaService service = new IdeaService(context, new FakeCurrentUser(ana), TimeProvider.System);

        IReadOnlyList<IdeaListItem> mine = await service.GetMineAsync(Cancellation);

        Assert.Equal(2, mine.Count);

        foreach (IdeaListItem item in mine)
        {
            Assert.Equal(0, item.SupportPercent);
        }
    }

    #endregion
}
