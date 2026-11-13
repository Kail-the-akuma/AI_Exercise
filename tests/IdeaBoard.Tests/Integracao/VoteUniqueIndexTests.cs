using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// The one vote per person per idea rule, checked at the level that enforces it.
///
/// Everything above this depends on the database refusing the second row. If it does not,
/// every higher level test still passes and the rule is gone, so this is checked by
/// inserting straight through the context, with no service in the way.
/// </summary>
public class VoteUniqueIndexTests
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


    #region The index itself

    /// <summary>
    /// A second vote by the same person on the same idea is refused by SQLite, not by
    /// anything in the application.
    /// </summary>
    [Fact]
    public async Task ASecondVoteByTheSamePersonIsRefusedByTheDatabase()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User voter = TestData.NewUser("Ana Dias");
        context.Users.Add(voter);
        await context.SaveChangesAsync(Cancellation);

        Idea idea = TestData.NewIdea(voter, "Shared calendar", TestData.Anchor);
        context.Ideas.Add(idea);
        await context.SaveChangesAsync(Cancellation);

        context.Votes.Add(TestData.NewVote(idea, voter, TestData.Anchor));
        await context.SaveChangesAsync(Cancellation);

        context.Votes.Add(TestData.NewVote(idea, voter, TestData.Anchor.AddMinutes(1)));

        DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await context.SaveChangesAsync(Cancellation);
        });

        SqliteException inner = Assert.IsType<SqliteException>(failure.InnerException);

        Assert.Equal(19, inner.SqliteErrorCode);
        Assert.Equal(2067, inner.SqliteExtendedErrorCode);
        Assert.True(
            SqliteUniqueConstraint.IsViolation(failure),
            "The shared detector must recognise the violation the index actually produces.");

        Assert.Equal(1, database.ExecuteScalarCount("SELECT COUNT(*) FROM \"Vote\";"));
    }


    /// <summary>
    /// The same person voting on a different idea, and a different person voting on the
    /// same idea, are both allowed. Without this the previous test would also pass against
    /// an index that was simply too wide.
    /// </summary>
    [Fact]
    public async Task TheIndexOnlyBlocksTheSamePairAndNothingElse()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User first = TestData.NewUser("Ana Dias");
        User second = TestData.NewUser("Bruno Lima");
        context.Users.Add(first);
        context.Users.Add(second);
        await context.SaveChangesAsync(Cancellation);

        Idea left = TestData.NewIdea(first, "Shared calendar", TestData.Anchor);
        Idea right = TestData.NewIdea(first, "Quiet room", TestData.Anchor);
        context.Ideas.Add(left);
        context.Ideas.Add(right);
        await context.SaveChangesAsync(Cancellation);

        context.Votes.Add(TestData.NewVote(left, first, TestData.Anchor));
        context.Votes.Add(TestData.NewVote(right, first, TestData.Anchor));
        context.Votes.Add(TestData.NewVote(left, second, TestData.Anchor));
        await context.SaveChangesAsync(Cancellation);

        Assert.Equal(3, database.ExecuteScalarCount("SELECT COUNT(*) FROM \"Vote\";"));
    }

    #endregion
}
