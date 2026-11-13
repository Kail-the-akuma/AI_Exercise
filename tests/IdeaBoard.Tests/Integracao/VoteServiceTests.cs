using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using IdeaBoard.App.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// Voting and withdrawing against a real database.
///
/// The claim under test is narrow and easy to get wrong in the generous direction: a
/// violation of the vote index is success, and every other write failure is not. A service
/// that caught every failure would pass the first half of this file and silently report a
/// broken database to the person as a vote that counted.
/// </summary>
public class VoteServiceTests
{
    #region Helpers

    private static CancellationToken Cancellation
    {
        get
        {
            return TestContext.Current.CancellationToken;
        }
    }


    private static VoteService CreateService(IdeaBoardDbContext context, ICurrentUser currentUser)
    {
        return new VoteService(
            context,
            currentUser,
            TimeProvider.System,
            NullLogger<VoteService>.Instance);
    }

    #endregion


    #region Idempotent success

    /// <summary>Asking twice to vote is not an error, and does not count twice.</summary>
    [Fact]
    public async Task VotingTwiceLeavesExactlyOneVote()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User voter = TestData.NewUser("Ana Dias");
        context.Users.Add(voter);
        await context.SaveChangesAsync(Cancellation);

        Idea idea = TestData.NewIdea(voter, "Shared calendar", TestData.Anchor);
        context.Ideas.Add(idea);
        await context.SaveChangesAsync(Cancellation);

        VoteService service = CreateService(context, new FakeCurrentUser(voter));

        VoteState? first = await service.AddAsync(idea.Id, Cancellation);
        VoteState? second = await service.AddAsync(idea.Id, Cancellation);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.True(second.HasVoted);
        Assert.Equal(1, second.VoteCount);
        Assert.Equal(1, database.ExecuteScalarCount("SELECT COUNT(*) FROM \"Vote\";"));
    }


    /// <summary>
    /// Losing the race on the unique index is reported as success, and the end state is the
    /// one the person asked for.
    ///
    /// The competing row is written by an interceptor between the service deciding to
    /// insert and its insert reaching SQLite, because that is the only sequence that
    /// reaches the handler. Two plain calls never do: the second finds the first one's row.
    /// </summary>
    [Fact]
    public async Task LosingTheRaceOnTheVoteIndexIsTreatedAsSuccess()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        int ideaId;
        int voterId;

        await using (IdeaBoardDbContext seedContext = database.CreateContext())
        {
            User seedVoter = TestData.NewUser("Ana Dias");
            seedContext.Users.Add(seedVoter);
            await seedContext.SaveChangesAsync(Cancellation);

            Idea seedIdea = TestData.NewIdea(seedVoter, "Shared calendar", TestData.Anchor);
            seedContext.Ideas.Add(seedIdea);
            await seedContext.SaveChangesAsync(Cancellation);

            ideaId = seedIdea.Id;
            voterId = seedVoter.Id;
        }

        Dictionary<string, object> competingValues = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            { "$ideaId", ideaId },
            { "$userId", voterId },
            { "$createdAt", "2026-01-01 09:00:00" },
        };

        RacingInsertInterceptor racer = new RacingInsertInterceptor(
            database.ConnectionString,
            "Vote",
            "INSERT INTO \"Vote\" (\"ideaId\", \"userId\", \"createdAt\") " +
            "VALUES ($ideaId, $userId, $createdAt);",
            competingValues);

        await using IdeaBoardDbContext context = database.CreateContext(racer);

        User voter = await context.Users.FirstAsync(user => user.Id == voterId, Cancellation);

        VoteService service = CreateService(context, new FakeCurrentUser(voter));

        VoteState? state = await service.AddAsync(ideaId, Cancellation);

        Assert.True(racer.Raced, "The competing vote must actually have been written.");
        Assert.NotNull(state);
        Assert.True(state.HasVoted);
        Assert.Equal(1, state.VoteCount);
        Assert.Equal(1, database.ExecuteScalarCount("SELECT COUNT(*) FROM \"Vote\";"));
    }


    /// <summary>Withdrawing a vote that is not there changes nothing and is not an error.</summary>
    [Fact]
    public async Task WithdrawingAVoteThatIsNotThereChangesNothing()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User voter = TestData.NewUser("Ana Dias");
        User other = TestData.NewUser("Bruno Lima");
        context.Users.Add(voter);
        context.Users.Add(other);
        await context.SaveChangesAsync(Cancellation);

        Idea idea = TestData.NewIdea(voter, "Shared calendar", TestData.Anchor);
        context.Ideas.Add(idea);
        await context.SaveChangesAsync(Cancellation);

        context.Votes.Add(TestData.NewVote(idea, other, TestData.Anchor));
        await context.SaveChangesAsync(Cancellation);

        VoteService service = CreateService(context, new FakeCurrentUser(voter));

        VoteState? state = await service.RemoveAsync(idea.Id, Cancellation);

        Assert.NotNull(state);
        Assert.False(state.HasVoted);
        Assert.Equal(1, state.VoteCount);
        Assert.Equal(1, database.ExecuteScalarCount("SELECT COUNT(*) FROM \"Vote\";"));
    }


    /// <summary>Withdrawing a vote that is there removes the row and nothing else.</summary>
    [Fact]
    public async Task WithdrawingAVoteRemovesOnlyThatVote()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User voter = TestData.NewUser("Ana Dias");
        User other = TestData.NewUser("Bruno Lima");
        context.Users.Add(voter);
        context.Users.Add(other);
        await context.SaveChangesAsync(Cancellation);

        Idea idea = TestData.NewIdea(voter, "Shared calendar", TestData.Anchor);
        context.Ideas.Add(idea);
        await context.SaveChangesAsync(Cancellation);

        context.Votes.Add(TestData.NewVote(idea, voter, TestData.Anchor));
        context.Votes.Add(TestData.NewVote(idea, other, TestData.Anchor));
        await context.SaveChangesAsync(Cancellation);

        VoteService service = CreateService(context, new FakeCurrentUser(voter));

        VoteState? state = await service.RemoveAsync(idea.Id, Cancellation);

        Assert.NotNull(state);
        Assert.False(state.HasVoted);
        Assert.Equal(1, state.VoteCount);
        Assert.Equal(1, database.ExecuteScalarCount("SELECT COUNT(*) FROM \"Vote\";"));
    }

    #endregion


    #region Unknown ideas

    /// <summary>Voting on an idea that does not exist answers null rather than inventing one.</summary>
    [Fact]
    public async Task VotingOnAnUnknownIdeaAnswersNull()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User voter = TestData.NewUser("Ana Dias");
        context.Users.Add(voter);
        await context.SaveChangesAsync(Cancellation);

        VoteService service = CreateService(context, new FakeCurrentUser(voter));

        VoteState? added = await service.AddAsync(4242, Cancellation);
        VoteState? removed = await service.RemoveAsync(4242, Cancellation);
        VoteState? read = await service.GetStateAsync(4242, Cancellation);

        Assert.Null(added);
        Assert.Null(removed);
        Assert.Null(read);
        Assert.Equal(0, database.ExecuteScalarCount("SELECT COUNT(*) FROM \"Vote\";"));
    }

    #endregion


    #region Failures that are not the unique index

    /// <summary>
    /// A write that fails for a reason other than the vote index rises as an exception and
    /// is never reported as a vote that counted.
    ///
    /// The failure used here is a foreign key violation: the signed-in person has an
    /// identifier that no row carries, which is what a stale session or a deleted row would
    /// look like. A service that caught DbUpdateException wholesale would swallow it and
    /// answer a cheerful state with a count of zero.
    /// </summary>
    [Fact]
    public async Task AWriteFailureThatIsNotTheVoteIndexRises()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User author = TestData.NewUser("Ana Dias");
        context.Users.Add(author);
        await context.SaveChangesAsync(Cancellation);

        Idea idea = TestData.NewIdea(author, "Shared calendar", TestData.Anchor);
        context.Ideas.Add(idea);
        await context.SaveChangesAsync(Cancellation);

        User ghost = TestData.NewUser("Ghost");
        ghost.Id = 987654;

        VoteService service = CreateService(context, new FakeCurrentUser(ghost));

        DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await service.AddAsync(idea.Id, Cancellation);
        });

        SqliteException inner = Assert.IsType<SqliteException>(failure.InnerException);

        // SQLITE_CONSTRAINT_FOREIGNKEY, next door to SQLITE_CONSTRAINT_UNIQUE under the same
        // primary code. Telling them apart is exactly what the extended code is for.
        Assert.Equal(19, inner.SqliteErrorCode);
        Assert.Equal(787, inner.SqliteExtendedErrorCode);
        Assert.False(SqliteUniqueConstraint.IsViolation(failure));
        Assert.Equal(0, database.ExecuteScalarCount("SELECT COUNT(*) FROM \"Vote\";"));
    }

    #endregion
}
