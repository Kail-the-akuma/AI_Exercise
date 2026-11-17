using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using IdeaBoard.App.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// Comments: writing them, changing your own, removing your own, and being refused
/// everybody else's.
///
/// The refusals are the reason this file matters more than it looks. Hiding a button is
/// not authorisation, and the difference between hiding and refusing is invisible on
/// screen: both look like a control that is not there.
/// </summary>
public class CommentTests
{
    #region Fixtures

    private static CancellationToken Cancellation
    {
        get
        {
            return TestContext.Current.CancellationToken;
        }
    }


    /// <summary>An idea by Ana, with Ana and Bruno both on the board.</summary>
    private sealed class Board
    {
        public User Ana { get; set; } = null!;

        public User Bruno { get; set; } = null!;

        public Idea Idea { get; set; } = null!;
    }


    private static async Task<Board> SeedAsync(IdeaBoardDbContext context)
    {
        User ana = TestData.NewUser("Ana Dias");
        User bruno = TestData.NewUser("Bruno Lima");

        context.Users.Add(ana);
        context.Users.Add(bruno);
        await context.SaveChangesAsync(Cancellation);

        Idea idea = TestData.NewIdea(ana, "Ideia comentada", TestData.Anchor);
        context.Ideas.Add(idea);
        await context.SaveChangesAsync(Cancellation);

        return new Board { Ana = ana, Bruno = bruno, Idea = idea };
    }


    private static CommentService ServiceFor(IdeaBoardDbContext context, User person, DateTime now)
    {
        return new CommentService(
            context,
            new FakeCurrentUser(person),
            new FakeTimeProvider(new DateTimeOffset(now, TimeSpan.Zero)));
    }

    #endregion


    #region Writing and reading

    /// <summary>
    /// A comment is stored against its idea and its author, with no change date, and comes
    /// back in the order things were said.
    /// </summary>
    [Fact]
    public async Task CommentsComeBackInTheOrderTheyWereWritten()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        Board board = await SeedAsync(context);

        await ServiceFor(context, board.Ana, TestData.Anchor.AddMinutes(1))
            .AddAsync(board.Idea.Id, "Primeiro", Cancellation);

        await ServiceFor(context, board.Bruno, TestData.Anchor.AddMinutes(2))
            .AddAsync(board.Idea.Id, "Segundo", Cancellation);

        IdeaService ideas = new IdeaService(context, new FakeCurrentUser(board.Ana), TimeProvider.System);

        IdeaDetail detail = (await ideas.GetDetailAsync(board.Idea.Id, Cancellation))!;

        Assert.Equal(2, detail.Comments.Count);
        Assert.Equal("Primeiro", detail.Comments[0].Text);
        Assert.Equal("Segundo", detail.Comments[1].Text);

        // Whose is whose is answered for the person looking, and not for whoever wrote last.
        Assert.True(detail.Comments[0].IsMine);
        Assert.False(detail.Comments[1].IsMine);

        Assert.False(detail.Comments[0].WasEdited);
        Assert.Null(detail.Comments[0].UpdatedAt);
    }


    /// <summary>
    /// The same comments read as Bruno answer the ownership question differently, which is
    /// the whole point of it being a fact about the reader.
    /// </summary>
    [Fact]
    public async Task WhoseCommentItIsDependsOnWhoIsLooking()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        Board board = await SeedAsync(context);

        await ServiceFor(context, board.Ana, TestData.Anchor.AddMinutes(1))
            .AddAsync(board.Idea.Id, "Da Ana", Cancellation);

        IdeaService asBruno = new IdeaService(context, new FakeCurrentUser(board.Bruno), TimeProvider.System);

        IdeaDetail detail = (await asBruno.GetDetailAsync(board.Idea.Id, Cancellation))!;

        Assert.False(Assert.Single(detail.Comments).IsMine);
    }

    #endregion


    #region Changing your own

    /// <summary>
    /// Editing your own comment replaces the text and records when, leaving the original
    /// date alone so the comment keeps its place in the conversation.
    /// </summary>
    [Fact]
    public async Task EditingYourOwnCommentReplacesTheTextAndRecordsWhen()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        Board board = await SeedAsync(context);

        Comment written = await ServiceFor(context, board.Ana, TestData.Anchor.AddMinutes(1))
            .AddAsync(board.Idea.Id, "Como estava", Cancellation);

        CommentChangeOutcome outcome = await ServiceFor(context, board.Ana, TestData.Anchor.AddHours(2))
            .EditAsync(written.Id, "Como ficou", Cancellation);

        Assert.Equal(CommentChangeOutcome.Done, outcome);

        Comment stored = await context.Comments
            .AsNoTracking()
            .FirstAsync(candidate => candidate.Id == written.Id, Cancellation);

        Assert.Equal("Como ficou", stored.Text);
        Assert.Equal(TestData.Anchor.AddMinutes(1), stored.CreatedAt);
        Assert.Equal(TestData.Anchor.AddHours(2), stored.UpdatedAt);
    }


    /// <summary>
    /// Removing your own comment removes it. Nothing is left behind saying something was
    /// here.
    ///
    /// The absence of a tombstone is asserted and not assumed, because a row kept with a
    /// flag would satisfy any test that only counted what the page shows, and would turn
    /// this product into one that has moderation without anybody deciding to.
    /// </summary>
    [Fact]
    public async Task RemovingYourOwnCommentLeavesNothingBehind()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        Board board = await SeedAsync(context);

        Comment written = await ServiceFor(context, board.Ana, TestData.Anchor.AddMinutes(1))
            .AddAsync(board.Idea.Id, "A apagar", Cancellation);

        CommentChangeOutcome outcome = await ServiceFor(context, board.Ana, TestData.Anchor.AddHours(2))
            .DeleteAsync(written.Id, Cancellation);

        Assert.Equal(CommentChangeOutcome.Done, outcome);

        Assert.Equal(0, await context.Comments.CountAsync(Cancellation));

        IdeaService ideas = new IdeaService(context, new FakeCurrentUser(board.Ana), TimeProvider.System);

        IdeaDetail detail = (await ideas.GetDetailAsync(board.Idea.Id, Cancellation))!;

        Assert.Empty(detail.Comments);
    }

    #endregion


    #region Somebody else's

    /// <summary>
    /// Bruno cannot change Ana's comment, and the text is untouched afterwards.
    ///
    /// THE SECOND ASSERTION IS THE ONE THAT MATTERS. An answer saying no, over a row that
    /// was changed anyway, is the exact shape of an authorisation check written after the
    /// write instead of before it, and the answer alone cannot tell the two apart.
    /// </summary>
    [Fact]
    public async Task SomebodyElsesCommentCannotBeEditedAndIsNotTouched()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        Board board = await SeedAsync(context);

        Comment hers = await ServiceFor(context, board.Ana, TestData.Anchor.AddMinutes(1))
            .AddAsync(board.Idea.Id, "Palavras da Ana", Cancellation);

        CommentChangeOutcome outcome = await ServiceFor(context, board.Bruno, TestData.Anchor.AddHours(2))
            .EditAsync(hers.Id, "Palavras do Bruno", Cancellation);

        Assert.Equal(CommentChangeOutcome.NotYours, outcome);

        Comment stored = await context.Comments
            .AsNoTracking()
            .FirstAsync(candidate => candidate.Id == hers.Id, Cancellation);

        Assert.Equal("Palavras da Ana", stored.Text);
        Assert.Null(stored.UpdatedAt);
    }


    /// <summary>Bruno cannot remove Ana's comment, and it is still there afterwards.</summary>
    [Fact]
    public async Task SomebodyElsesCommentCannotBeRemovedAndIsStillThere()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        Board board = await SeedAsync(context);

        Comment hers = await ServiceFor(context, board.Ana, TestData.Anchor.AddMinutes(1))
            .AddAsync(board.Idea.Id, "Palavras da Ana", Cancellation);

        CommentChangeOutcome outcome = await ServiceFor(context, board.Bruno, TestData.Anchor.AddHours(2))
            .DeleteAsync(hers.Id, Cancellation);

        Assert.Equal(CommentChangeOutcome.NotYours, outcome);

        Assert.Equal(1, await context.Comments.CountAsync(Cancellation));
    }


    /// <summary>
    /// A comment that is not there at all is told apart from one that belongs to somebody
    /// else, because the two are different things to say and different things to find in a
    /// log afterwards.
    /// </summary>
    [Fact]
    public async Task ACommentThatDoesNotExistIsNotTheSameAsOneThatIsNotYours()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        Board board = await SeedAsync(context);

        CommentService asBruno = ServiceFor(context, board.Bruno, TestData.Anchor.AddHours(2));

        Assert.Equal(
            CommentChangeOutcome.NotFound,
            await asBruno.EditAsync(4242, "Nada", Cancellation));

        Assert.Equal(
            CommentChangeOutcome.NotFound,
            await asBruno.DeleteAsync(4242, Cancellation));
    }

    #endregion


    #region Length and emptiness

    /// <summary>
    /// The rules mirror the ones a submitted idea already obeys: trimmed first, so that a
    /// comment of nothing but spaces is an empty comment, and measured after trimming.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t ")]
    public void ACommentOfNothingIsRefused(string? text)
    {
        CommentSubmissionResult result = CommentSubmission.Validate(text);

        Assert.False(result.IsValid);
        Assert.Equal(CommentSubmission.MissingTextMessage, result.Errors[CommentLimits.TextField]);
    }


    /// <summary>
    /// The limit is the one that applies, and it applies to the trimmed text. SQLite does
    /// not enforce column lengths, so nothing below this catches a comment that is too
    /// long.
    /// </summary>
    [Fact]
    public void ACommentLongerThanTheLimitIsRefusedAndOneExactlyAtItIsNot()
    {
        string atTheLimit = new string('a', CommentLimits.TextMaximumLength);
        string overIt = new string('a', CommentLimits.TextMaximumLength + 1);

        Assert.True(CommentSubmission.Validate(atTheLimit).IsValid);

        CommentSubmissionResult refused = CommentSubmission.Validate(overIt);

        Assert.False(refused.IsValid);
        Assert.Equal(CommentSubmission.TextTooLongMessage, refused.Errors[CommentLimits.TextField]);
    }


    /// <summary>
    /// Whitespace around a comment is trimmed before it is measured, so padding cannot be
    /// used to push a comment over the limit and a padded comment is stored tidy.
    /// </summary>
    [Fact]
    public void ACommentIsTrimmedBeforeItIsMeasuredAndStored()
    {
        string padded = string.Concat("   ", new string('a', CommentLimits.TextMaximumLength), "   ");

        CommentSubmissionResult result = CommentSubmission.Validate(padded);

        Assert.True(result.IsValid);
        Assert.Equal(CommentLimits.TextMaximumLength, result.Text.Length);
    }


    /// <summary>
    /// The service refuses to store something the domain rejected, rather than trusting the
    /// page to have asked first.
    /// </summary>
    [Fact]
    public async Task TheServiceRefusesTextTheDomainWouldReject()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        Board board = await SeedAsync(context);

        CommentService service = ServiceFor(context, board.Ana, TestData.Anchor);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AddAsync(board.Idea.Id, "   ", Cancellation));
    }

    #endregion
}
