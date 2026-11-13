using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using IdeaBoard.App.Services;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// One idea in full, with the public list of who voted for it.
///
/// The votes are inserted in an order that disagrees with their instants, so that the
/// expected list cannot be produced by returning the rows in the order they were written.
/// </summary>
public class IdeaDetailTests
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


    #region Detail

    /// <summary>
    /// The detail carries the idea, the author's name, the count, and the voters from the
    /// oldest vote to the most recent.
    /// </summary>
    [Fact]
    public async Task TheDetailListsVotersFromTheOldestVoteOnwards()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User author = TestData.NewUser("Ana Dias");
        User early = TestData.NewUser("Bruno Lima");
        User middle = TestData.NewUser("Carla Nunes");
        User late = TestData.NewUser("Duarte Reis");

        context.Users.Add(author);
        context.Users.Add(early);
        context.Users.Add(middle);
        context.Users.Add(late);
        await context.SaveChangesAsync(Cancellation);

        Idea idea = TestData.NewIdea(author, "Shared calendar", TestData.Anchor);
        context.Ideas.Add(idea);
        await context.SaveChangesAsync(Cancellation);

        // Written last to first on purpose: the row with the lowest identifier carries the
        // latest instant, so the expected order can only come from the date.
        context.Votes.Add(TestData.NewVote(idea, late, TestData.Anchor.AddHours(3)));
        await context.SaveChangesAsync(Cancellation);

        context.Votes.Add(TestData.NewVote(idea, early, TestData.Anchor.AddHours(1)));
        await context.SaveChangesAsync(Cancellation);

        context.Votes.Add(TestData.NewVote(idea, middle, TestData.Anchor.AddHours(2)));
        await context.SaveChangesAsync(Cancellation);

        IdeaService service = new IdeaService(context, new FakeCurrentUser(middle), TimeProvider.System);

        IdeaDetail? detail = await service.GetDetailAsync(idea.Id, Cancellation);

        Assert.NotNull(detail);
        Assert.Equal(idea.Id, detail.Id);
        Assert.Equal("Shared calendar", detail.Title);
        Assert.Equal("Ana Dias", detail.AuthorDisplayName);
        Assert.Equal(3, detail.VoteCount);
        Assert.True(detail.HasVoted);

        List<string> voterNames = new List<string>();

        foreach (IdeaVoter voter in detail.Voters)
        {
            voterNames.Add(voter.DisplayName);
        }

        Assert.Equal(
            new List<string> { "Bruno Lima", "Carla Nunes", "Duarte Reis" },
            voterNames);
    }


    /// <summary>Somebody who has not voted sees the same idea with the button unset.</summary>
    [Fact]
    public async Task TheDetailReportsNoVoteForSomebodyWhoHasNotVoted()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User author = TestData.NewUser("Ana Dias");
        User voter = TestData.NewUser("Bruno Lima");
        User bystander = TestData.NewUser("Carla Nunes");

        context.Users.Add(author);
        context.Users.Add(voter);
        context.Users.Add(bystander);
        await context.SaveChangesAsync(Cancellation);

        Idea idea = TestData.NewIdea(author, "Shared calendar", TestData.Anchor);
        context.Ideas.Add(idea);
        await context.SaveChangesAsync(Cancellation);

        context.Votes.Add(TestData.NewVote(idea, voter, TestData.Anchor.AddHours(1)));
        await context.SaveChangesAsync(Cancellation);

        IdeaService service = new IdeaService(context, new FakeCurrentUser(bystander), TimeProvider.System);

        IdeaDetail? detail = await service.GetDetailAsync(idea.Id, Cancellation);

        Assert.NotNull(detail);
        Assert.False(detail.HasVoted);
        Assert.Equal(1, detail.VoteCount);
        Assert.Single(detail.Voters);
    }


    /// <summary>An identifier nobody holds answers null rather than an empty idea.</summary>
    [Fact]
    public async Task AnUnknownIdentifierAnswersNull()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User reader = TestData.NewUser("Ana Dias");
        context.Users.Add(reader);
        await context.SaveChangesAsync(Cancellation);

        IdeaService service = new IdeaService(context, new FakeCurrentUser(reader), TimeProvider.System);

        IdeaDetail? detail = await service.GetDetailAsync(4242, Cancellation);

        Assert.Null(detail);
    }

    #endregion
}
