using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using IdeaBoard.App.Services;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// Whether the vote state of a whole list is resolved in one go, for every list the
/// product has.
///
/// This is the one place in the product where asking the database once per row is easy to
/// write and impossible to see: the page looks identical either way, and only the number of
/// commands tells them apart. So the commands are counted, and the count is compared
/// between a short list and a long one. A count that grows with the list is the defect.
/// </summary>
public class IdeaRankingQueryCountTests
{
    #region Helpers

    private static CancellationToken Cancellation
    {
        get
        {
            return TestContext.Current.CancellationToken;
        }
    }


    /// <summary>
    /// Fills a database with a given number of ideas and votes from two different people,
    /// then counts the commands one ranking costs.
    /// </summary>
    private static async Task<int> MeasureRankingCommandsAsync(int ideaCount)
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        CountingCommandInterceptor counter = new CountingCommandInterceptor();

        await using IdeaBoardDbContext context = database.CreateContext(counter);

        User author = TestData.NewUser("Ana Dias");
        User reader = TestData.NewUser("Bruno Lima");
        User other = TestData.NewUser("Carla Nunes");

        context.Users.Add(author);
        context.Users.Add(reader);
        context.Users.Add(other);
        await context.SaveChangesAsync(Cancellation);

        List<Idea> ideas = SeedIdeas(context, author, ideaCount);
        await context.SaveChangesAsync(Cancellation);

        SeedVotes(context, ideas, reader, other);
        await context.SaveChangesAsync(Cancellation);

        IdeaService service = new IdeaService(context, new FakeCurrentUser(reader), TimeProvider.System);

        // One pass to warm the connection and the compiled query, so that what is counted
        // afterwards is the cost of the page and not the cost of the first page ever.
        await service.GetRankingAsync(Cancellation);

        counter.Reset();

        IReadOnlyList<IdeaListItem> ranking = await service.GetRankingAsync(Cancellation);

        Assert.Equal(ideaCount, ranking.Count);

        return counter.ExecutedCommandCount;
    }


    /// <summary>
    /// The same fixture as above, with the ideas written BY the person doing the looking,
    /// so that both personal lists come back full rather than empty.
    /// </summary>
    private static async Task<int> MeasurePersonalCommandsAsync(int ideaCount, bool votedList)
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        CountingCommandInterceptor counter = new CountingCommandInterceptor();

        await using IdeaBoardDbContext context = database.CreateContext(counter);

        User reader = TestData.NewUser("Ana Dias");
        User other = TestData.NewUser("Bruno Lima");

        context.Users.Add(reader);
        context.Users.Add(other);
        await context.SaveChangesAsync(Cancellation);

        List<Idea> ideas = SeedIdeas(context, reader, ideaCount);
        await context.SaveChangesAsync(Cancellation);

        SeedVotes(context, ideas, reader, other);
        await context.SaveChangesAsync(Cancellation);

        IdeaService service = new IdeaService(context, new FakeCurrentUser(reader), TimeProvider.System);

        await ReadPersonalListAsync(service, votedList);

        counter.Reset();

        IReadOnlyList<IdeaListItem> list = await ReadPersonalListAsync(service, votedList);

        Assert.NotEmpty(list);

        return counter.ExecutedCommandCount;
    }


    private static async Task<IReadOnlyList<IdeaListItem>> ReadPersonalListAsync(IdeaService service, bool votedList)
    {
        if (votedList)
        {
            return await service.GetVotedByMeAsync(Cancellation);
        }

        return await service.GetMineAsync(Cancellation);
    }


    private static List<Idea> SeedIdeas(IdeaBoardDbContext context, User author, int ideaCount)
    {
        List<Idea> ideas = new List<Idea>();

        for (int index = 0; index < ideaCount; index++)
        {
            Idea idea = TestData.NewIdea(
                author,
                string.Concat("Idea ", index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                TestData.Anchor.AddMinutes(-index));

            context.Ideas.Add(idea);
            ideas.Add(idea);
        }

        return ideas;
    }


    private static void SeedVotes(IdeaBoardDbContext context, List<Idea> ideas, User reader, User other)
    {
        for (int index = 0; index < ideas.Count; index++)
        {
            context.Votes.Add(TestData.NewVote(ideas[index], other, TestData.Anchor.AddMinutes(index)));

            if (index % 3 != 0)
            {
                continue;
            }

            context.Votes.Add(TestData.NewVote(ideas[index], reader, TestData.Anchor.AddMinutes(index)));
        }
    }

    #endregion


    #region Command count

    /// <summary>
    /// Ten times the ideas cost the same number of commands. A list that asked once per row
    /// would cost ten times more here, which is the whole point of comparing two sizes
    /// rather than asserting a number for one.
    /// </summary>
    [Fact]
    public async Task TheRankingCostsTheSameNumberOfCommandsWhateverTheNumberOfIdeas()
    {
        int costOfThree = await MeasureRankingCommandsAsync(3);
        int costOfThirty = await MeasureRankingCommandsAsync(30);

        Assert.Equal(costOfThree, costOfThirty);
        Assert.True(
            costOfThirty <= 3,
            string.Concat(
                "A ranking should cost a small, fixed number of commands. It cost ",
                costOfThirty.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "."));
    }


    /// <summary>
    /// The single extra command still answers the question correctly for every row: the
    /// state belongs to the person looking, and never to whoever voted last.
    /// </summary>
    [Fact]
    public async Task TheRankingMarksExactlyTheRowsTheCurrentPersonVotedOn()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User author = TestData.NewUser("Ana Dias");
        User reader = TestData.NewUser("Bruno Lima");
        User other = TestData.NewUser("Carla Nunes");

        context.Users.Add(author);
        context.Users.Add(reader);
        context.Users.Add(other);
        await context.SaveChangesAsync(Cancellation);

        List<Idea> ideas = SeedIdeas(context, author, 9);
        await context.SaveChangesAsync(Cancellation);

        SeedVotes(context, ideas, reader, other);
        await context.SaveChangesAsync(Cancellation);

        IdeaService service = new IdeaService(context, new FakeCurrentUser(reader), TimeProvider.System);

        IReadOnlyList<IdeaListItem> ranking = await service.GetRankingAsync(Cancellation);

        HashSet<int> expectedVotedIds = new HashSet<int>();

        for (int index = 0; index < ideas.Count; index += 3)
        {
            expectedVotedIds.Add(ideas[index].Id);
        }

        foreach (IdeaListItem item in ranking)
        {
            Assert.Equal(expectedVotedIds.Contains(item.Id), item.HasVoted);
        }

        Assert.Equal(3, expectedVotedIds.Count);
    }

    #endregion


    #region Personal lists

    /// <summary>
    /// The submitted list costs the same for three ideas and for thirty.
    ///
    /// It carries one cost the two main tabs do not: the bars on a list that shows a subset
    /// have to be measured against the whole table, so there is a command here that the
    /// ranking does not pay. What matters is that it is ONE command and not one per row,
    /// and the only way to see that is to compare two sizes.
    /// </summary>
    [Fact]
    public async Task TheSubmittedListCostsTheSameNumberOfCommandsWhateverItsSize()
    {
        int costOfThree = await MeasurePersonalCommandsAsync(3, false);
        int costOfThirty = await MeasurePersonalCommandsAsync(30, false);

        Assert.Equal(costOfThree, costOfThirty);
        Assert.True(
            costOfThirty <= 3,
            string.Concat(
                "The submitted list should cost a small, fixed number of commands. It cost ",
                costOfThirty.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "."));
    }


    /// <summary>
    /// The voted list costs the same for three ideas and for thirty, and costs LESS than it
    /// looks like it should: every row on it was voted on by definition, so the question
    /// the other lists ask the database is already answered by the filter.
    /// </summary>
    [Fact]
    public async Task TheVotedListCostsTheSameNumberOfCommandsWhateverItsSize()
    {
        int costOfThree = await MeasurePersonalCommandsAsync(3, true);
        int costOfThirty = await MeasurePersonalCommandsAsync(30, true);

        Assert.Equal(costOfThree, costOfThirty);
        Assert.True(
            costOfThirty <= 3,
            string.Concat(
                "The voted list should cost a small, fixed number of commands. It cost ",
                costOfThirty.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "."));
    }

    #endregion
}
