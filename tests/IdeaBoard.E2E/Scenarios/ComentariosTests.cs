using System.Net;
using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using IdeaBoard.App.Pages;
using IdeaBoard.E2E.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit;

namespace IdeaBoard.E2E.Scenarios;

/// <summary>
/// Commenting on an idea, changing your own, removing your own, and being refused
/// everybody else's.
///
/// The refusal scenarios do not touch a button. They build the request by hand and send
/// it, because that is the only way to tell apart a control that is not drawn from an
/// action that is not allowed. On screen those two are the same thing, and only one of
/// them is protection.
/// </summary>
[Collection(AppHostCollection.Name)]
public class ComentariosTests : ScenarioBase
{
    #region Construction

    /// <inheritdoc />
    public ComentariosTests(AppHostFixture host)
        : base(host)
    {
    }

    #endregion


    #region Writing

    /// <summary>
    /// Somebody writes a comment with the browser's scripting turned off, and it shows up
    /// under the idea with their name on it.
    ///
    /// Scripting is off because the whole application is built that way and a form is the
    /// easiest thing to accidentally make depend on it.
    /// </summary>
    [Fact]
    public async Task ACommentIsWrittenAndShownWithoutScripting()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        int ideaId = await SubmitIdeaAndReadIdAsync(ana, "Ideia para comentar", "Descricao.");

        IPage plain = await OpenWithoutScriptAsAsync(People.BrunoObjectId);

        await plain.GotoAsync(DetailUrl(ideaId));

        await Expect(plain.GetByText(UiText.NoComments)).ToBeVisibleAsync();

        await plain.GetByLabel(UiText.CommentLabel, new() { Exact = true }).FillAsync("Parece-me bem pensado.");
        await plain.GetByRole(AriaRole.Button, new() { Name = UiText.AddCommentButton, Exact = true }).ClickAsync();

        ILocator comments = Comments(plain);

        await Expect(comments).ToHaveCountAsync(1);
        await Expect(comments.First).ToContainTextAsync("Parece-me bem pensado.");
        await Expect(comments.First).ToContainTextAsync(People.BrunoDisplayName);

        // It is in the database and not only on the page that just wrote it.
        Assert.Equal(1, await CountCommentsAsync());
    }


    /// <summary>
    /// An empty comment is refused by the server with the message the specification fixes,
    /// and nothing is stored.
    ///
    /// Scripting is off so that the browser's own required-field handling cannot be what
    /// refuses it. What is being checked is the server.
    /// </summary>
    [Fact]
    public async Task AnEmptyCommentIsRefusedByTheServer()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        int ideaId = await SubmitIdeaAndReadIdAsync(ana, "Ideia para comentar", "Descricao.");

        IPage plain = await OpenWithoutScriptAsAsync(People.AnaObjectId);

        await plain.GotoAsync(DetailUrl(ideaId));

        await plain.GetByLabel(UiText.CommentLabel, new() { Exact = true }).FillAsync("    ");
        await plain.GetByRole(AriaRole.Button, new() { Name = UiText.AddCommentButton, Exact = true }).ClickAsync();

        await Expect(plain.GetByText(CommentSubmission.MissingTextMessage)).ToBeVisibleAsync();

        Assert.Equal(0, await CountCommentsAsync());
    }

    #endregion


    #region Your own

    /// <summary>
    /// Changing your own comment replaces what it says and marks it as changed, and
    /// removing it leaves nothing at all in its place.
    ///
    /// The last assertion is the one worth having. A removed comment that left a line
    /// saying a comment was removed would be moderation arriving through the back door of
    /// a product that deliberately has none.
    /// </summary>
    [Fact]
    public async Task YourOwnCommentCanBeChangedAndThenRemovedWithoutATrace()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        int ideaId = await SubmitIdeaAndReadIdAsync(ana, "Ideia para comentar", "Descricao.");

        IPage plain = await OpenWithoutScriptAsAsync(People.AnaObjectId);

        await plain.GotoAsync(DetailUrl(ideaId));

        await plain.GetByLabel(UiText.CommentLabel, new() { Exact = true }).FillAsync("Como estava");
        await plain.GetByRole(AriaRole.Button, new() { Name = UiText.AddCommentButton, Exact = true }).ClickAsync();

        await Expect(Comments(plain)).ToHaveCountAsync(1);
        await Expect(plain.GetByText(UiText.EditedMarker)).ToHaveCountAsync(0);

        await plain.GetByRole(AriaRole.Link, new() { Name = UiText.EditCommentLink, Exact = true }).ClickAsync();

        await plain.GetByLabel(UiText.CommentLabel, new() { Exact = true }).FillAsync("Como ficou");
        await plain.GetByRole(AriaRole.Button, new() { Name = UiText.SaveCommentButton, Exact = true }).ClickAsync();

        await Expect(Comments(plain).First).ToContainTextAsync("Como ficou");
        await Expect(Comments(plain).First).Not.ToContainTextAsync("Como estava");
        await Expect(plain.GetByText(UiText.EditedMarker)).ToBeVisibleAsync();

        // Removing asks first, because it cannot be undone and there is no scripting here
        // to put a dialogue in the way.
        await plain.GetByRole(AriaRole.Link, new() { Name = UiText.DeleteCommentLink, Exact = true }).ClickAsync();

        await Expect(plain.GetByText(UiText.ConfirmDeleteComment)).ToBeVisibleAsync();

        Assert.Equal(1, await CountCommentsAsync());

        await plain.GetByRole(AriaRole.Button, new() { Name = UiText.ConfirmDeleteCommentButton, Exact = true }).ClickAsync();

        await Expect(Comments(plain)).ToHaveCountAsync(0);
        await Expect(plain.GetByText(UiText.NoComments)).ToBeVisibleAsync();

        Assert.Equal(0, await CountCommentsAsync());
    }

    #endregion


    #region Somebody else's

    /// <summary>
    /// THE ONE THAT MATTERS. Bruno is not shown the controls over Ana's comment, and that
    /// proves nothing, so he sends the requests anyway.
    ///
    /// Both are built by hand with his own session and posted straight at the handlers,
    /// which is what anybody can do with the page open and the developer tools in front of
    /// them. The server answers 403 to both, and Ana's comment is untouched afterwards.
    ///
    /// The check after the refusal is not belt and braces. An answer saying no, over a row
    /// that was changed anyway, is exactly what an ownership check written after the write
    /// looks like, and the status code alone cannot tell the two apart.
    /// </summary>
    [Fact]
    public async Task SomebodyElsesCommentCannotBeChangedOrRemovedBySubmittingDirectly()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        int ideaId = await SubmitIdeaAndReadIdAsync(ana, "Ideia da Ana", "Descricao.");

        await ana.GotoAsync(DetailUrl(ideaId));
        await ana.GetByLabel(UiText.CommentLabel, new() { Exact = true }).FillAsync("Palavras da Ana");
        await ana.GetByRole(AriaRole.Button, new() { Name = UiText.AddCommentButton, Exact = true }).ClickAsync();

        await Expect(Comments(ana)).ToHaveCountAsync(1);

        int commentId = await ReadOnlyCommentIdAsync();

        IPage bruno = await OpenAsAsync(People.BrunoObjectId);

        await bruno.GotoAsync(DetailUrl(ideaId));

        // He sees it, and is offered nothing to do to it.
        await Expect(Comments(bruno)).ToHaveCountAsync(1);
        await Expect(bruno.GetByRole(AriaRole.Link, new() { Name = UiText.EditCommentLink, Exact = true })).ToHaveCountAsync(0);
        await Expect(bruno.GetByRole(AriaRole.Link, new() { Name = UiText.DeleteCommentLink, Exact = true })).ToHaveCountAsync(0);

        string token = await ReadAntiforgeryTokenAsync(bruno);

        Assert.NotEqual(string.Empty, token);

        IAPIResponse edit = await PostAsync(
            bruno,
            ideaId,
            "EditComment",
            token,
            commentId,
            "Palavras do Bruno");

        Assert.Equal(
            (int)HttpStatusCode.Forbidden,
            edit.Status);

        IAPIResponse delete = await PostAsync(bruno, ideaId, "DeleteComment", token, commentId, null);

        Assert.Equal(
            (int)HttpStatusCode.Forbidden,
            delete.Status);

        // Nothing happened to it, which is the half a status code cannot prove.
        Comment stored = await ReadOnlyCommentAsync();

        Assert.Equal("Palavras da Ana", stored.Text);
        Assert.Null(stored.UpdatedAt);
        Assert.Equal(1, await CountCommentsAsync());

        await ana.ReloadAsync();
        await Expect(Comments(ana).First).ToContainTextAsync("Palavras da Ana");
    }


    /// <summary>
    /// Opening the edit form over somebody else's comment is possible and useless, which is
    /// the honest consequence of the form being a link.
    ///
    /// The address is a query string anybody can type. What it does is change what is
    /// drawn, and what is drawn decides nothing, so the page comes back with Ana's comment
    /// as text and no form over it.
    /// </summary>
    [Fact]
    public async Task OpeningTheEditFormOverSomebodyElsesCommentChangesNothing()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        int ideaId = await SubmitIdeaAndReadIdAsync(ana, "Ideia da Ana", "Descricao.");

        await ana.GotoAsync(DetailUrl(ideaId));
        await ana.GetByLabel(UiText.CommentLabel, new() { Exact = true }).FillAsync("Palavras da Ana");
        await ana.GetByRole(AriaRole.Button, new() { Name = UiText.AddCommentButton, Exact = true }).ClickAsync();

        int commentId = await ReadOnlyCommentIdAsync();

        IPage bruno = await OpenAsAsync(People.BrunoObjectId);

        await bruno.GotoAsync(
            $"{DetailUrl(ideaId)}?{DetalheModel.EditingQueryParameter}={commentId}");

        await Expect(Comments(bruno)).ToHaveCountAsync(1);
        await Expect(bruno.GetByRole(AriaRole.Button, new() { Name = UiText.SaveCommentButton, Exact = true })).ToHaveCountAsync(0);
        await Expect(Comments(bruno).First).ToContainTextAsync("Palavras da Ana");
    }

    #endregion


    #region Helpers

    /// <summary>The comments on the page, as the list draws them.</summary>
    private static ILocator Comments(IPage page)
    {
        return page.Locator("ul.lista-comentarios > li");
    }


    /// <summary>Address of one idea's own page.</summary>
    private string DetailUrl(int ideaId)
    {
        return Url($"/ideias/{ideaId}");
    }


    /// <summary>Reads the antiforgery token the rendered page carries.</summary>
    private static async Task<string> ReadAntiforgeryTokenAsync(IPage page)
    {
        string selector = "input[name='__RequestVerificationToken']";

        return await page.Locator(selector).First.GetAttributeAsync("value") ?? string.Empty;
    }


    /// <summary>Posts at one of the comment handlers the way the form does, without it.</summary>
    private async Task<IAPIResponse> PostAsync(
        IPage page,
        int ideaId,
        string handler,
        string token,
        int commentId,
        string? text)
    {
        IFormData form = page.Context.APIRequest.CreateFormData();

        form.Set("commentId", commentId);
        form.Set("__RequestVerificationToken", token);

        if (text is not null)
        {
            form.Set("Text", text);
        }

        return await page.Context.APIRequest.PostAsync(
            $"{DetailUrl(ideaId)}?handler={handler}",
            new APIRequestContextOptions { Form = form, MaxRedirects = 0 });
    }


    /// <summary>The only comment in the database, which the scenarios seed exactly one of.</summary>
    private async Task<Comment> ReadOnlyCommentAsync()
    {
        using IdeaBoardDbContext context = Host.CreateContext();

        return await context.Comments
            .AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
    }


    /// <summary>The identifier of that comment.</summary>
    private async Task<int> ReadOnlyCommentIdAsync()
    {
        Comment comment = await ReadOnlyCommentAsync();

        return comment.Id;
    }


    /// <summary>
    /// Counts the comments in the database, which is the only place a comment either
    /// exists or does not. A count read from a page is the page's claim about itself.
    /// </summary>
    private async Task<int> CountCommentsAsync()
    {
        using IdeaBoardDbContext context = Host.CreateContext();

        return await context.Comments.CountAsync(TestContext.Current.CancellationToken);
    }

    #endregion
}
