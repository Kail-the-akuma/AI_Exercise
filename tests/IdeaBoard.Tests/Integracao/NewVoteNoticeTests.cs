using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using IdeaBoard.App.Pages;
using IdeaBoard.App.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// The notice that somebody voted on an idea you proposed, and the marker that makes "new"
/// mean new to one person rather than recent in general.
/// </summary>
public class NewVoteNoticeTests
{
    #region Helpers

    private static CancellationToken Cancellation
    {
        get
        {
            return TestContext.Current.CancellationToken;
        }
    }


    /// <summary>A request that is authenticated, which is what the service checks for.</summary>
    private static IHttpContextAccessor SignedInRequest()
    {
        DefaultHttpContext httpContext = new DefaultHttpContext();

        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity("TestAuthentication"));

        return new HttpContextAccessor { HttpContext = httpContext };
    }


    /// <summary>Builds the service as the given person.</summary>
    private static NewVoteNotices NoticesFor(IdeaBoardDbContext context, User person, TimeProvider time)
    {
        return new NewVoteNotices(context, new FakeCurrentUser(person), time, SignedInRequest());
    }

    #endregion


    #region What counts as new

    /// <summary>
    /// Somebody who has never opened the page sees every vote their ideas ever received,
    /// and their own vote is not among them.
    ///
    /// The marker starting as null is the honest state and not a missing value: nothing has
    /// been seen because nobody has been here. The fixture also has the author voting on
    /// their own idea, so a count that reported the reader back to themselves would be two
    /// and not one.
    /// </summary>
    [Fact]
    public async Task SomebodyWhoNeverLookedSeesEverythingExceptTheirOwnVote()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User author = TestData.NewUser("Ana Dias");
        User other = TestData.NewUser("Bruno Lima");

        context.Users.Add(author);
        context.Users.Add(other);
        await context.SaveChangesAsync(Cancellation);

        Idea idea = TestData.NewIdea(author, "Ideia da Ana", TestData.Anchor);
        context.Ideas.Add(idea);
        await context.SaveChangesAsync(Cancellation);

        context.Votes.Add(TestData.NewVote(idea, other, TestData.Anchor.AddMinutes(1)));
        context.Votes.Add(TestData.NewVote(idea, author, TestData.Anchor.AddMinutes(2)));
        await context.SaveChangesAsync(Cancellation);

        Assert.Null(author.LastNotificationsSeenAt);

        NewVoteNotice notice = await NoticesFor(context, author, TimeProvider.System)
            .ReadAsync(Cancellation);

        Assert.Equal(1, notice.VoteCount);
        Assert.Equal(1, notice.IdeaCount);
        Assert.True(notice.HasAnything);
        Assert.Equal(1, notice.VoteCountByIdea[idea.Id]);
    }


    /// <summary>
    /// Only votes after the marker count, and they are counted per idea.
    ///
    /// The fixture puts one vote on each side of the marker, so a count that ignored the
    /// marker would be three rather than two, and a count that ignored the idea would not
    /// split them.
    /// </summary>
    [Fact]
    public async Task OnlyVotesAfterTheMarkerCountAndTheyAreSplitByIdea()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User author = TestData.NewUser("Ana Dias");
        User first = TestData.NewUser("Bruno Lima");
        User second = TestData.NewUser("Carla Nunes");

        context.Users.Add(author);
        context.Users.Add(first);
        context.Users.Add(second);
        await context.SaveChangesAsync(Cancellation);

        Idea one = TestData.NewIdea(author, "Ideia um", TestData.Anchor);
        Idea two = TestData.NewIdea(author, "Ideia dois", TestData.Anchor);
        context.Ideas.Add(one);
        context.Ideas.Add(two);
        await context.SaveChangesAsync(Cancellation);

        // Before the marker.
        context.Votes.Add(TestData.NewVote(one, first, TestData.Anchor.AddMinutes(1)));

        // After it.
        context.Votes.Add(TestData.NewVote(one, second, TestData.Anchor.AddMinutes(10)));
        context.Votes.Add(TestData.NewVote(two, first, TestData.Anchor.AddMinutes(11)));
        await context.SaveChangesAsync(Cancellation);

        author.LastNotificationsSeenAt = TestData.Anchor.AddMinutes(5);
        await context.SaveChangesAsync(Cancellation);

        NewVoteNotice notice = await NoticesFor(context, author, TimeProvider.System)
            .ReadAsync(Cancellation);

        Assert.Equal(2, notice.VoteCount);
        Assert.Equal(2, notice.IdeaCount);
        Assert.Equal(1, notice.VoteCountByIdea[one.Id]);
        Assert.Equal(1, notice.VoteCountByIdea[two.Id]);
    }


    /// <summary>
    /// Votes on somebody else's ideas are not this person's news, however recent they are.
    /// </summary>
    [Fact]
    public async Task VotesOnOtherPeoplesIdeasAreNotNews()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User reader = TestData.NewUser("Ana Dias");
        User stranger = TestData.NewUser("Bruno Lima");

        context.Users.Add(reader);
        context.Users.Add(stranger);
        await context.SaveChangesAsync(Cancellation);

        Idea theirs = TestData.NewIdea(stranger, "Ideia do Bruno", TestData.Anchor);
        context.Ideas.Add(theirs);
        await context.SaveChangesAsync(Cancellation);

        context.Votes.Add(TestData.NewVote(theirs, reader, TestData.Anchor.AddMinutes(1)));
        await context.SaveChangesAsync(Cancellation);

        NewVoteNotice notice = await NoticesFor(context, reader, TimeProvider.System)
            .ReadAsync(Cancellation);

        Assert.Equal(0, notice.VoteCount);
        Assert.False(notice.HasAnything);
    }

    #endregion


    #region Clearing

    /// <summary>
    /// Marking the news as read clears it, and marking it again writes nothing.
    ///
    /// The second half is the one that matters, because this is called from a GET. A page
    /// that wrote unconditionally would write once per visit forever to record a fact that
    /// has not changed, and a repeated GET would keep having a consequence.
    /// </summary>
    [Fact]
    public async Task ClearingIsConditionalSoARepeatedVisitWritesNothing()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User author = TestData.NewUser("Ana Dias");
        User other = TestData.NewUser("Bruno Lima");

        context.Users.Add(author);
        context.Users.Add(other);
        await context.SaveChangesAsync(Cancellation);

        Idea idea = TestData.NewIdea(author, "Ideia da Ana", TestData.Anchor);
        context.Ideas.Add(idea);
        await context.SaveChangesAsync(Cancellation);

        context.Votes.Add(TestData.NewVote(idea, other, TestData.Anchor.AddMinutes(1)));
        await context.SaveChangesAsync(Cancellation);

        FakeTimeProvider time = new FakeTimeProvider(new DateTimeOffset(TestData.Anchor.AddHours(1), TimeSpan.Zero));
        NewVoteNotices notices = NoticesFor(context, author, time);

        Assert.True(await notices.ReadAsync(Cancellation) is { HasAnything: true });

        Assert.True(await notices.MarkSeenAsync(Cancellation));

        NewVoteNotice afterClearing = await notices.ReadAsync(Cancellation);

        Assert.Equal(0, afterClearing.VoteCount);
        Assert.False(afterClearing.HasAnything);

        // Nothing left to clear, so nothing is written.
        Assert.False(await notices.MarkSeenAsync(Cancellation));

        User stored = await context.Users
            .AsNoTracking()
            .FirstAsync(candidate => candidate.Id == author.Id, Cancellation);

        Assert.Equal(TestData.Anchor.AddHours(1), stored.LastNotificationsSeenAt);
    }


    /// <summary>
    /// A vote that arrives after the marker was cleared is news again, which is what makes
    /// the notice worth looking at a second time.
    /// </summary>
    [Fact]
    public async Task AVoteArrivingAfterClearingIsNewsAgain()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User author = TestData.NewUser("Ana Dias");
        User first = TestData.NewUser("Bruno Lima");
        User second = TestData.NewUser("Carla Nunes");

        context.Users.Add(author);
        context.Users.Add(first);
        context.Users.Add(second);
        await context.SaveChangesAsync(Cancellation);

        Idea idea = TestData.NewIdea(author, "Ideia da Ana", TestData.Anchor);
        context.Ideas.Add(idea);
        await context.SaveChangesAsync(Cancellation);

        context.Votes.Add(TestData.NewVote(idea, first, TestData.Anchor.AddMinutes(1)));
        await context.SaveChangesAsync(Cancellation);

        FakeTimeProvider time = new FakeTimeProvider(new DateTimeOffset(TestData.Anchor.AddMinutes(5), TimeSpan.Zero));
        NewVoteNotices notices = NoticesFor(context, author, time);

        await notices.MarkSeenAsync(Cancellation);
        Assert.False((await notices.ReadAsync(Cancellation)).HasAnything);

        context.Votes.Add(TestData.NewVote(idea, second, TestData.Anchor.AddMinutes(9)));
        await context.SaveChangesAsync(Cancellation);

        NewVoteNotice notice = await notices.ReadAsync(Cancellation);

        Assert.Equal(1, notice.VoteCount);
    }

    #endregion


    #region A request the browser made on its own

    /// <summary>
    /// A request that announces itself as something the browser fetched ahead of time is
    /// recognised, whichever of the three headers it uses.
    ///
    /// This is the guard on a write inside a GET. The link to the personal page is in the
    /// layout, so it is on every page, and a prefetch of it would clear news nobody read.
    /// Nothing would error and nothing would be logged.
    /// </summary>
    [Theory]
    [InlineData("Sec-Purpose", "prefetch")]
    [InlineData("Sec-Purpose", "prefetch;prerender")]
    [InlineData("Purpose", "prefetch")]
    [InlineData("X-moz", "prefetch")]
    [InlineData("Sec-Purpose", "PREFETCH")]
    public void ARequestTheBrowserAnnouncedAsSpeculativeIsRecognised(string header, string value)
    {
        DefaultHttpContext httpContext = new DefaultHttpContext();

        httpContext.Request.Headers[header] = value;

        Assert.True(RequestIntent.IsSpeculative(httpContext.Request));
    }


    /// <summary>An ordinary request is not mistaken for one.</summary>
    [Fact]
    public void AnOrdinaryRequestIsNotMistakenForOne()
    {
        DefaultHttpContext httpContext = new DefaultHttpContext();

        Assert.False(RequestIntent.IsSpeculative(httpContext.Request));

        httpContext.Request.Headers["Sec-Purpose"] = "";
        Assert.False(RequestIntent.IsSpeculative(httpContext.Request));

        httpContext.Request.Headers.Accept = "text/html";
        Assert.False(RequestIntent.IsSpeculative(httpContext.Request));
    }

    #endregion


    #region Wording

    /// <summary>The two sentences, with their singulars, in European Portuguese.</summary>
    [Theory]
    [InlineData(1, "1 voto novo")]
    [InlineData(4, "4 votos novos")]
    public void TheCountOfNewVotesIsWorded(int voteCount, string expected)
    {
        Assert.Equal(expected, UiText.FormatNewVotes(voteCount));
    }


    [Theory]
    [InlineData(1, "1 ideia sua recebeu votos novos")]
    [InlineData(3, "3 ideias suas receberam votos novos")]
    public void TheCountOfIdeasWithNewVotesIsWorded(int ideaCount, string expected)
    {
        Assert.Equal(expected, UiText.FormatIdeasWithNewVotes(ideaCount));
    }

    #endregion
}

