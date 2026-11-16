using System.Text.RegularExpressions;
using IdeaBoard.App.Domain;
using IdeaBoard.App.Pages;
using IdeaBoard.E2E.Fixtures;
using Microsoft.Playwright;
using Xunit;

namespace IdeaBoard.E2E.Scenarios;

/// <summary>
/// Submitting an idea, the empty list, and the validation that has to hold on the server.
/// </summary>
[Collection(AppHostCollection.Name)]
public class SubmissaoTests : ScenarioBase
{
    /// <inheritdoc />
    public SubmissaoTests(AppHostFixture host)
        : base(host)
    {
    }


    /// <summary>
    /// Submitting lands on the new idea's own page, and the idea then appears in Recentes
    /// under the author's name, in the documented order.
    ///
    /// Two ideas and not one, deliberately. With a single idea in the list any ordering
    /// whatsoever satisfies "it appears in Recentes", so the order the tab exists to
    /// provide would not be observable at all.
    ///
    /// The landing page is asserted because Docs/02 sends the author to the detail of what
    /// they just wrote, and not back to a list. A scenario that only checked the list would
    /// be satisfied by a redirect to the list, which is the very thing that would be wrong.
    /// </summary>
    [Fact]
    public async Task SubmittedIdeaLandsOnItsOwnPageAndAppearsInRecentes()
    {
        await StartCleanAsync();

        IPage page = await OpenAsAsync(People.AnaObjectId);

        await SubmitIdeaAsync(page, "Primeira ideia da equipa", "Descricao da primeira.");

        await Expect(page).ToHaveURLAsync(new Regex(@"/ideias/\d+$"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Primeira ideia da equipa" }))
            .ToBeVisibleAsync();

        await SubmitIdeaAsync(page, "Segunda ideia da equipa", "Descricao da segunda.");

        await page.GotoAsync(Url("/recentes"));

        ILocator items = page.Locator("ul.lista-ideias > li");

        await Expect(items).ToHaveCountAsync(2);

        // Most recent first, which is the whole point of this tab.
        await Expect(items.Nth(0)).ToContainTextAsync("Segunda ideia da equipa");
        await Expect(items.Nth(1)).ToContainTextAsync("Primeira ideia da equipa");

        // The author is public, and is shown as the name the person is known by.
        await Expect(items.Nth(0)).ToContainTextAsync(People.AnaDisplayName);
    }


    /// <summary>
    /// An empty list invites the first idea, and stops inviting once there is one.
    ///
    /// The second half is what makes the first an assertion rather than a decoration.
    /// Without it the scenario is equally satisfied by an invitation that is rendered
    /// always, which is a single misplaced condition away and would leave every reader of a
    /// full list being asked to be the first person to propose something.
    /// </summary>
    [Fact]
    public async Task EmptyListInvitesTheFirstIdeaAndStopsOnceThereIsOne()
    {
        await StartCleanAsync();

        IPage page = await OpenAsAsync(People.AnaObjectId);

        foreach (string path in new[] { "/", "/recentes" })
        {
            await page.GotoAsync(Url(path));

            await Expect(page.GetByText(UiText.EmptyList)).ToBeVisibleAsync();

            // Scoped to the main region on purpose. The same link sits in the navigation on
            // every page, so an unscoped assertion would be satisfied by the navigation and
            // would go on passing with the invitation's own button gone.
            await Expect(page.GetByRole(AriaRole.Main)
                    .GetByRole(AriaRole.Link, new() { Name = UiText.SubmitIdeaButton }))
                .ToBeVisibleAsync();
        }

        await SubmitIdeaAsync(page, "Uma ideia qualquer", "Para a lista deixar de estar vazia.");

        foreach (string path in new[] { "/", "/recentes" })
        {
            await page.GotoAsync(Url(path));

            await Expect(page.GetByText(UiText.EmptyList)).Not.ToBeVisibleAsync();
        }
    }


    /// <summary>
    /// The warning that submission is final is on screen before the button is pressed.
    ///
    /// Only the presence is asserted here. Whether it is placed where a person reads it
    /// before acting is a judgement no selector makes, and stays on the manual list.
    /// </summary>
    [Fact]
    public async Task PermanenceWarningIsVisibleBeforeSubmitting()
    {
        await StartCleanAsync();

        IPage page = await OpenAsAsync(People.AnaObjectId);

        await page.GotoAsync(Url("/nova"));

        await Expect(page.GetByText(UiText.PermanenceWarning)).ToBeVisibleAsync();
    }


    /// <summary>
    /// An empty title is refused by the server and the description survives the refusal.
    ///
    /// This one goes through the form on purpose. There is no client validation framework
    /// in this application, only the native maxlength attribute, so an empty title reaches
    /// the server exactly as it would for a person. Keeping what was typed is asserted
    /// because Docs/02 requires it, and a refusal that empties a long description is a
    /// defect that the message alone would hide.
    /// </summary>
    [Fact]
    public async Task EmptyTitleIsRefusedAndTypedContentSurvives()
    {
        await StartCleanAsync();

        IPage page = await OpenAsAsync(People.AnaObjectId);

        await page.GotoAsync(Url("/nova"));

        await page.GetByLabel("Descrição").FillAsync("Uma descrição que não se deve perder.");
        await page.GetByRole(AriaRole.Button, new() { Name = UiText.SubmitIdeaButton }).ClickAsync();

        await Expect(page.GetByText(IdeaSubmission.MissingTitleMessage)).ToBeVisibleAsync();
        await Expect(page.GetByLabel("Descrição"))
            .ToHaveValueAsync("Uma descrição que não se deve perder.");
    }


    /// <summary>
    /// A title longer than the limit is refused by the server even when the browser is
    /// taken out of the way.
    ///
    /// This one does NOT go through the form, and that is the point. The input carries
    /// maxlength, so typing can never exceed the limit and a scenario that typed would be
    /// verifying the browser. Docs/02 is explicit that the server is what counts and that a
    /// submission bypassing the browser has to be refused all the same, so the request is
    /// sent directly, with the antiforgery token the page itself issued.
    /// </summary>
    [Fact]
    public async Task OverlongTitleIsRefusedWhenTheBrowserIsBypassed()
    {
        await StartCleanAsync();

        IPage page = await OpenAsAsync(People.AnaObjectId);

        await page.GotoAsync(Url("/nova"));

        string token = await ReadAntiforgeryTokenAsync(page);

        Assert.NotEqual(string.Empty, token);

        string overlongTitle = new string('a', IdeaLimits.TitleMaximumLength + 1);

        IFormData form = page.Context.APIRequest.CreateFormData();
        form.Set("Title", overlongTitle);
        form.Set("Description", "Descricao valida.");
        form.Set("__RequestVerificationToken", token);

        IAPIResponse response = await page.Context.APIRequest.PostAsync(
            Url("/nova"),
            new APIRequestContextOptions { Form = form });

        string body = await response.TextAsync();

        // Asserted through the DOM and not against the raw body, and this is not fussiness.
        // Razor encodes every non-ASCII character as a numeric entity, so the message comes
        // down the wire as "O t&#xED;tulo n&#xE3;o pode...". A plain string comparison
        // against the message constant fails on a response that is in fact perfectly
        // correct, which looks exactly like a product defect and is not one.
        await page.SetContentAsync(body);

        await Expect(page.GetByText(IdeaSubmission.TitleTooLongMessage)).ToBeVisibleAsync();

        // And nothing was stored, which is the half that a message on screen does not prove.
        // Read from the database rather than from the list, so that a list that happened to
        // be filtering could not make this pass.
        using IdeaBoard.App.Data.IdeaBoardDbContext context = Host.CreateContext();

        int storedIdeas = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .CountAsync(context.Ideas, TestContext.Current.CancellationToken);

        Assert.Equal(0, storedIdeas);
    }


    /// <summary>Reads the antiforgery token the rendered form carries.</summary>
    private static async Task<string> ReadAntiforgeryTokenAsync(IPage page)
    {
        string selector = "input[name='__RequestVerificationToken']";

        return await page.Locator(selector).First.GetAttributeAsync("value") ?? string.Empty;
    }
}
