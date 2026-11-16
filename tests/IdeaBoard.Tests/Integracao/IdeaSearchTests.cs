using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using IdeaBoard.App.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// Searching the board by title and by who proposed the idea.
///
/// Every fixture here is accented, and that is the point rather than decoration. Seeded
/// data without accents makes this whole area pass under any implementation, including the
/// one that does not work, because the difference between the implementations only shows on
/// the letters that unaccented test data does not contain. An application written in
/// Portuguese, listing names of real colleagues, is made almost entirely of those letters.
/// </summary>
public class IdeaSearchTests
{
    #region Fixtures

    private const string AccentedTitle = "Sessões de partilha entre equipas";
    private const string PlainTitle = "Cafe no atrio";
    private const string AccentedAuthorName = "João Gonçalves";
    private const string PlainAuthorName = "Ana Dias";


    private static CancellationToken Cancellation
    {
        get
        {
            return TestContext.Current.CancellationToken;
        }
    }


    /// <summary>
    /// Seeds one idea by an accented author and one by a plain one, and hands back a
    /// service that reads as the plain author.
    /// </summary>
    private static async Task<IdeaService> SeedAsync(IdeaBoardDbContext context)
    {
        User joao = TestData.NewUser(AccentedAuthorName);
        User ana = TestData.NewUser(PlainAuthorName);

        context.Users.Add(joao);
        context.Users.Add(ana);
        await context.SaveChangesAsync(Cancellation);

        context.Ideas.Add(TestData.NewIdea(joao, AccentedTitle, TestData.Anchor));
        context.Ideas.Add(TestData.NewIdea(ana, PlainTitle, TestData.Anchor.AddMinutes(1)));
        await context.SaveChangesAsync(Cancellation);

        return new IdeaService(context, new FakeCurrentUser(ana), TimeProvider.System);
    }


    /// <summary>The titles a search returned, in the order it returned them.</summary>
    private static List<string> TitlesOf(IReadOnlyList<IdeaListItem> items)
    {
        List<string> titles = new List<string>();

        foreach (IdeaListItem item in items)
        {
            titles.Add(item.Title);
        }

        return titles;
    }

    #endregion


    #region Accents and case

    /// <summary>
    /// An accented title is found by somebody who did not type the accents, which is how
    /// most people type most of the time.
    /// </summary>
    [Theory]
    [InlineData("sessoes")]
    [InlineData("SESSOES")]
    [InlineData("Sessões")]
    [InlineData("SESSÕES")]
    [InlineData("sessões")]
    [InlineData("PARTILHA")]
    public async Task AnAccentedTitleIsFoundHoweverTheAccentsAndTheCaseAreTyped(string term)
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        IdeaService service = await SeedAsync(context);

        IReadOnlyList<IdeaListItem> found = await service.GetRankingAsync(term, Cancellation);

        Assert.Equal([AccentedTitle], TitlesOf(found));
    }


    /// <summary>
    /// The same for a person's name, which is the half that matters most. Spelling a
    /// colleague's name correctly, in capitals, is the case the database itself gets wrong.
    /// </summary>
    [Theory]
    [InlineData("joao")]
    [InlineData("JOAO")]
    [InlineData("João")]
    [InlineData("JOÃO")]
    [InlineData("gonçalves")]
    [InlineData("GONCALVES")]
    public async Task AnAccentedNameIsFoundHoweverTheAccentsAndTheCaseAreTyped(string term)
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        IdeaService service = await SeedAsync(context);

        IReadOnlyList<IdeaListItem> found = await service.GetRankingAsync(term, Cancellation);

        Assert.Equal([AccentedTitle], TitlesOf(found));
    }


    /// <summary>
    /// THE MEASUREMENT THIS DESIGN RESTS ON, pinned so that moving the filtering back into
    /// the query cannot happen quietly.
    ///
    /// Every assertion below is a thing SQLite does and the application must not inherit.
    /// If a later change pushes the search into a LIKE and this file is deleted along the
    /// way, the tests above go red. If it is kept and the search is pushed into a LIKE, the
    /// tests above go red and these stay green, which names the cause.
    /// </summary>
    [Fact]
    public async Task SqliteItselfCannotDoThisSearchAndTheseAreTheExactWaysItFails()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        await SeedAsync(context);

        // Case folds for unaccented letters, which is the part that works.
        Assert.Equal(1, await CountTitlesLike(context, "%cafe%"));
        Assert.Equal(1, await CountTitlesLike(context, "%CAFE%"));

        // The accented word is not found by its unaccented spelling.
        Assert.Equal(0, await CountTitlesLike(context, "%sessoes%"));

        // And it is not found by its own spelling in capitals either, although the row
        // contains exactly that word. The uppercase O with tilde does not fold.
        Assert.Equal(0, await CountTitlesLike(context, "%SESSÕES%"));

        // Only the exact case of the accented letter matches.
        Assert.Equal(1, await CountTitlesLike(context, "%Sessões%"));

        // Names behave the same way, which is where it hurts.
        Assert.Equal(0, await CountNamesLike(context, "%joao%"));
        Assert.Equal(0, await CountNamesLike(context, "%JOÃO%"));

        // The usual rescue does not work: upper() is ASCII only as well, so the accented
        // letter survives in lower case and the comparison still fails.
        int upperTrick = await context.Ideas
            .Where(idea => idea.Title.ToUpper().Contains("SESSÕES"))
            .CountAsync(Cancellation);

        Assert.Equal(0, upperTrick);
    }


    private static async Task<int> CountTitlesLike(IdeaBoardDbContext context, string pattern)
    {
        return await context.Ideas
            .Where(idea => EF.Functions.Like(idea.Title, pattern))
            .CountAsync(Cancellation);
    }


    private static async Task<int> CountNamesLike(IdeaBoardDbContext context, string pattern)
    {
        return await context.Users
            .Where(person => EF.Functions.Like(person.DisplayName, pattern))
            .CountAsync(Cancellation);
    }

    #endregion


    #region What a search does not change

    /// <summary>
    /// A search narrows the list and changes nothing else. The bars keep measuring against
    /// the board's leader, so an idea that leads a search result does not claim to lead the
    /// board.
    ///
    /// The fixture is built so that the two answers differ: the matching idea has one vote
    /// while the idea that leads the board, and does not match, has four. Measured against
    /// the result the bar would be full and say this idea leads. Measured against the board
    /// it is a quarter.
    /// </summary>
    [Fact]
    public async Task TheBarsStillMeasureAgainstTheBoardAndNotAgainstTheResult()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        User author = TestData.NewUser(PlainAuthorName);
        context.Users.Add(author);

        List<User> voters = new List<User>();

        for (int index = 0; index < 4; index++)
        {
            User voter = TestData.NewUser(string.Concat("Votante ", index));
            context.Users.Add(voter);
            voters.Add(voter);
        }

        await context.SaveChangesAsync(Cancellation);

        Idea leader = TestData.NewIdea(author, "Bicicletas para todos", TestData.Anchor);
        Idea matching = TestData.NewIdea(author, "Sessões de partilha", TestData.Anchor.AddMinutes(1));

        context.Ideas.Add(leader);
        context.Ideas.Add(matching);
        await context.SaveChangesAsync(Cancellation);

        for (int index = 0; index < 4; index++)
        {
            context.Votes.Add(TestData.NewVote(leader, voters[index], TestData.Anchor.AddMinutes(index)));
        }

        context.Votes.Add(TestData.NewVote(matching, voters[0], TestData.Anchor.AddMinutes(9)));
        await context.SaveChangesAsync(Cancellation);

        IdeaService service = new IdeaService(context, new FakeCurrentUser(author), TimeProvider.System);

        IReadOnlyList<IdeaListItem> found = await service.GetRankingAsync("sessoes", Cancellation);

        IdeaListItem only = Assert.Single(found);

        Assert.Equal(1, only.VoteCount);
        Assert.Equal(25, only.SupportPercent);
    }


    /// <summary>A term that matches nothing gives an empty list and not every idea.</summary>
    [Fact]
    public async Task ATermThatMatchesNothingGivesNothing()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        IdeaService service = await SeedAsync(context);

        IReadOnlyList<IdeaListItem> found = await service.GetRankingAsync("bicicletas", Cancellation);

        Assert.Empty(found);
    }


    /// <summary>
    /// Nothing, blank and whitespace are all the unsearched page. They are collapsed in one
    /// place so that a stray space in a bookmarked address does not empty the board.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnAbsentOrBlankTermIsNotASearch(string? term)
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        IdeaService service = await SeedAsync(context);

        IReadOnlyList<IdeaListItem> found = await service.GetRankingAsync(term, Cancellation);

        Assert.Equal(2, found.Count);
        Assert.Null(IdeaSearch.Clean(term));
    }


    /// <summary>Surrounding spaces are trimmed rather than taken as part of the term.</summary>
    [Fact]
    public async Task ATermIsTrimmedBeforeItIsUsed()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        IdeaService service = await SeedAsync(context);

        IReadOnlyList<IdeaListItem> found = await service.GetRankingAsync("  sessoes  ", Cancellation);

        Assert.Equal([AccentedTitle], TitlesOf(found));
    }


    /// <summary>The recent tab searches the same way, so the two tabs cannot drift apart.</summary>
    [Fact]
    public async Task TheRecentTabSearchesTheSameWay()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        await using IdeaBoardDbContext context = database.CreateContext();

        IdeaService service = await SeedAsync(context);

        IReadOnlyList<IdeaListItem> found = await service.GetRecentAsync("JOAO", Cancellation);

        Assert.Equal([AccentedTitle], TitlesOf(found));
    }

    #endregion
}
