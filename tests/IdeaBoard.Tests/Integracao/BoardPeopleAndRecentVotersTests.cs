using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using IdeaBoard.App.Pages;
using IdeaBoard.App.Services;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// The two facts the list pages carry besides the ideas themselves: how many people have
/// arrived at the board, and who voted most recently on each row.
/// </summary>
public class BoardPeopleAndRecentVotersTests
{
    #region Helpers

    private static CancellationToken Cancellation
    {
        get
        {
            return TestContext.Current.CancellationToken;
        }
    }

    #endregion


    #region People on the board

    /// <summary>
    /// The count is of rows in the user table, which is people who have arrived, and it
    /// moves as more of them do.
    ///
    /// What this also pins is what the number is NOT. A person exists here only once they
    /// have opened the application and a name is known for them, so this can never be the
    /// size of the team, and the wording beside it says so.
    /// </summary>
    [Fact]
    public async Task TheCountIsOfPeopleWhoHaveArrivedAndItMovesAsMoreArrive()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        BoardPeople people = new BoardPeople(context);

        Assert.Equal(0, await people.CountArrivedAsync(Cancellation));

        context.Users.Add(TestData.NewUser("Ana Dias"));
        await context.SaveChangesAsync(Cancellation);

        Assert.Equal(1, await people.CountArrivedAsync(Cancellation));

        context.Users.Add(TestData.NewUser("João Gonçalves"));
        context.Users.Add(TestData.NewUser("Bruno Lima"));
        await context.SaveChangesAsync(Cancellation);

        Assert.Equal(3, await people.CountArrivedAsync(Cancellation));
    }


    /// <summary>
    /// The wording never reads as a claim about everybody, and it has a singular. One
    /// person on the board is the first day, which is the day the sentence is most likely
    /// to be read.
    /// </summary>
    [Theory]
    [InlineData(0, "de 0 pessoas no quadro")]
    [InlineData(1, "de 1 pessoa no quadro")]
    [InlineData(8, "de 8 pessoas no quadro")]
    public void TheWordingSaysWhatTheNumberCounts(int peopleCount, string expected)
    {
        Assert.Equal(expected, UiText.FormatArrivedPeople(peopleCount));
    }

    #endregion


    #region Latest voters

    /// <summary>
    /// The latest voters arrive newest first and are cut to the limit, and an idea nobody
    /// voted on carries none rather than an absent value the view has to guard against.
    ///
    /// The fixture votes in a known order with more voters than the limit allows, so a
    /// list that kept the oldest, or kept them in the database's own order, is a different
    /// answer and not the same answer arranged differently.
    /// </summary>
    [Fact]
    public async Task TheLatestVotersComeBackNewestFirstAndCutToTheLimit()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User author = TestData.NewUser("Ana Dias");
        context.Users.Add(author);

        List<User> voters = new List<User>();
        int voterCount = IdeaListItem.RecentVoterLimit + 2;

        for (int index = 0; index < voterCount; index++)
        {
            User voter = TestData.NewUser(string.Concat("Votante ", index));
            context.Users.Add(voter);
            voters.Add(voter);
        }

        await context.SaveChangesAsync(Cancellation);

        Idea voted = TestData.NewIdea(author, "Ideia apoiada", TestData.Anchor);
        Idea untouched = TestData.NewIdea(author, "Ideia sem apoio", TestData.Anchor.AddMinutes(1));

        context.Ideas.Add(voted);
        context.Ideas.Add(untouched);
        await context.SaveChangesAsync(Cancellation);

        for (int index = 0; index < voterCount; index++)
        {
            context.Votes.Add(TestData.NewVote(voted, voters[index], TestData.Anchor.AddMinutes(index)));
        }

        await context.SaveChangesAsync(Cancellation);

        IdeaService service = new IdeaService(context, new FakeCurrentUser(author), TimeProvider.System);

        IReadOnlyList<IdeaListItem> ranking = await service.GetRankingAsync(null, Cancellation);

        IdeaListItem votedRow = RowTitled(ranking, "Ideia apoiada");
        IdeaListItem untouchedRow = RowTitled(ranking, "Ideia sem apoio");

        Assert.Equal(IdeaListItem.RecentVoterLimit, votedRow.RecentVoterNames.Count);

        List<string> expected = new List<string>();

        for (int index = voterCount - 1; index >= voterCount - IdeaListItem.RecentVoterLimit; index--)
        {
            expected.Add(string.Concat("Votante ", index));
        }

        Assert.Equal(expected, votedRow.RecentVoterNames);
        Assert.Empty(untouchedRow.RecentVoterNames);
    }


    /// <summary>
    /// The names belong to the row they were read for, and are never the whole board's
    /// latest voters repeated down the page.
    ///
    /// One query serves every row here, so putting the right names on the wrong row is the
    /// way this breaks, and it is invisible unless two rows have different voters.
    /// </summary>
    [Fact]
    public async Task EachRowCarriesItsOwnVotersAndNotTheBoards()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User author = TestData.NewUser("Ana Dias");
        User first = TestData.NewUser("Primeiro Votante");
        User second = TestData.NewUser("Segundo Votante");

        context.Users.Add(author);
        context.Users.Add(first);
        context.Users.Add(second);
        await context.SaveChangesAsync(Cancellation);

        Idea one = TestData.NewIdea(author, "Ideia um", TestData.Anchor);
        Idea two = TestData.NewIdea(author, "Ideia dois", TestData.Anchor.AddMinutes(1));

        context.Ideas.Add(one);
        context.Ideas.Add(two);
        await context.SaveChangesAsync(Cancellation);

        context.Votes.Add(TestData.NewVote(one, first, TestData.Anchor));
        context.Votes.Add(TestData.NewVote(two, second, TestData.Anchor.AddMinutes(5)));
        await context.SaveChangesAsync(Cancellation);

        IdeaService service = new IdeaService(context, new FakeCurrentUser(author), TimeProvider.System);

        IReadOnlyList<IdeaListItem> ranking = await service.GetRankingAsync(null, Cancellation);

        Assert.Equal(["Primeiro Votante"], RowTitled(ranking, "Ideia um").RecentVoterNames);
        Assert.Equal(["Segundo Votante"], RowTitled(ranking, "Ideia dois").RecentVoterNames);
    }


    private static IdeaListItem RowTitled(IReadOnlyList<IdeaListItem> items, string title)
    {
        foreach (IdeaListItem item in items)
        {
            if (string.Equals(item.Title, title, StringComparison.Ordinal))
            {
                return item;
            }
        }

        Assert.Fail(string.Concat("The list carries no idea titled ", title, "."));

        return new IdeaListItem();
    }

    #endregion
}
