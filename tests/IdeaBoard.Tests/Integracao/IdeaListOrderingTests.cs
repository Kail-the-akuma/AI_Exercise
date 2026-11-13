using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using IdeaBoard.App.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// The order the two lists come back in.
///
/// Every fixture here is built so that the date and the identifier disagree: the row
/// inserted first, with the lowest identifier, carries the most recent date. If they
/// agreed, ordering by date and ordering by identifier would produce the same list, and a
/// test that cannot tell them apart is checking the identifier while claiming to check the
/// tie break.
/// </summary>
public class IdeaListOrderingTests
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


    #region Ordering

    /// <summary>
    /// The recent list is ordered by submission date, not by the order the rows went in.
    /// The fixture inverts the two completely, so an implementation ordering by identifier
    /// returns this list backwards.
    /// </summary>
    [Fact]
    public async Task TheRecentListFollowsTheDateAndNotTheIdentifier()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User author = TestData.NewUser("Ana Dias");
        context.Users.Add(author);
        await context.SaveChangesAsync(Cancellation);

        FakeTimeProvider clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));
        IdeaService service = new IdeaService(context, new FakeCurrentUser(author), clock);

        await service.SubmitAsync("Newest and first in", "Description", Cancellation);

        clock.SetUtcNow(new DateTimeOffset(2026, 1, 9, 12, 0, 0, TimeSpan.Zero));
        await service.SubmitAsync("Middle", "Description", Cancellation);

        clock.SetUtcNow(new DateTimeOffset(2026, 1, 8, 12, 0, 0, TimeSpan.Zero));
        await service.SubmitAsync("Oldest and last in", "Description", Cancellation);

        IReadOnlyList<IdeaListItem> recent = await service.GetRecentAsync(Cancellation);

        Assert.Equal(
            new List<string> { "Newest and first in", "Middle", "Oldest and last in" },
            TitlesOf(recent));

        // The identifiers run the other way, which is what makes the assertion above a test
        // of the date rather than of the identifier.
        Assert.True(recent[0].Id < recent[1].Id);
        Assert.True(recent[1].Id < recent[2].Id);
    }


    /// <summary>
    /// The ranking puts the most voted first, and settles everything below that by date
    /// before it ever reaches the identifier. The most voted idea here is also the oldest
    /// and the last one in, so neither of the lower criteria can produce this order.
    /// </summary>
    [Fact]
    public async Task TheRankingPutsVotesFirstAndBreaksTiesByDate()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User author = TestData.NewUser("Ana Dias");
        User voter = TestData.NewUser("Bruno Lima");
        context.Users.Add(author);
        context.Users.Add(voter);
        await context.SaveChangesAsync(Cancellation);

        FakeTimeProvider clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));
        IdeaService service = new IdeaService(context, new FakeCurrentUser(author), clock);

        await service.SubmitAsync("Newest and first in", "Description", Cancellation);

        clock.SetUtcNow(new DateTimeOffset(2026, 1, 9, 12, 0, 0, TimeSpan.Zero));
        await service.SubmitAsync("Middle", "Description", Cancellation);

        clock.SetUtcNow(new DateTimeOffset(2026, 1, 8, 12, 0, 0, TimeSpan.Zero));
        await service.SubmitAsync("Oldest and last in", "Description", Cancellation);

        clock.SetUtcNow(new DateTimeOffset(2026, 1, 7, 12, 0, 0, TimeSpan.Zero));
        Idea voted = await service.SubmitAsync("Older still, but voted", "Description", Cancellation);

        context.Votes.Add(TestData.NewVote(voted, voter, TestData.Anchor));
        await context.SaveChangesAsync(Cancellation);

        IReadOnlyList<IdeaListItem> ranking = await service.GetRankingAsync(Cancellation);

        Assert.Equal(
            new List<string>
            {
                "Older still, but voted",
                "Newest and first in",
                "Middle",
                "Oldest and last in",
            },
            TitlesOf(ranking));

        Assert.Equal(1, ranking[0].VoteCount);
        Assert.Equal(0, ranking[1].VoteCount);
    }


    /// <summary>
    /// Only when two ideas carry the very same instant does the identifier decide, and then
    /// the higher one wins.
    /// </summary>
    [Fact]
    public async Task AnExactDateTieIsSettledByTheHigherIdentifier()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User author = TestData.NewUser("Ana Dias");
        context.Users.Add(author);
        await context.SaveChangesAsync(Cancellation);

        FakeTimeProvider clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));
        IdeaService service = new IdeaService(context, new FakeCurrentUser(author), clock);

        Idea first = await service.SubmitAsync("Same instant, lower identifier", "Description", Cancellation);
        Idea second = await service.SubmitAsync("Same instant, higher identifier", "Description", Cancellation);

        Assert.Equal(first.CreatedAt, second.CreatedAt);

        IReadOnlyList<IdeaListItem> recent = await service.GetRecentAsync(Cancellation);
        IReadOnlyList<IdeaListItem> ranking = await service.GetRankingAsync(Cancellation);

        Assert.Equal(second.Id, recent[0].Id);
        Assert.Equal(first.Id, recent[1].Id);
        Assert.Equal(second.Id, ranking[0].Id);
        Assert.Equal(first.Id, ranking[1].Id);
    }


    /// <summary>The author shown on a row is read from the user table, not from the token.</summary>
    [Fact]
    public async Task EachRowCarriesTheAuthorNameFromTheUserTable()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User author = TestData.NewUser("Ana Dias");
        User reader = TestData.NewUser("Bruno Lima");
        context.Users.Add(author);
        context.Users.Add(reader);
        await context.SaveChangesAsync(Cancellation);

        FakeCurrentUser currentUser = new FakeCurrentUser(author);
        FakeTimeProvider clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));
        IdeaService service = new IdeaService(context, currentUser, clock);

        await service.SubmitAsync("Shared calendar", "Description", Cancellation);

        currentUser.SignInAs(reader);

        IReadOnlyList<IdeaListItem> recent = await service.GetRecentAsync(Cancellation);

        Assert.Single(recent);
        Assert.Equal("Ana Dias", recent[0].AuthorDisplayName);
    }

    #endregion
}
