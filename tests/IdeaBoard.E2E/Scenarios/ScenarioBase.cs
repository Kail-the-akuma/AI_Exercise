using System.Globalization;
using System.Text.RegularExpressions;
using IdeaBoard.App.Pages;
using IdeaBoard.E2E.Fixtures;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;

namespace IdeaBoard.E2E.Scenarios;

/// <summary>
/// Shared ground for every scenario: a clean database, and a way to be somebody.
/// </summary>
public abstract class ScenarioBase : PageTest
{
    #region Constants

    /// <summary>Cookie the picker sets, and the only way in for a scenario.</summary>
    private const string PersonCookieName = "ideaboard-local-person";

    /// <summary>Shape of the address an idea's own page is served at.</summary>
    private const string IdeaPagePattern = @"/ideias/(\d+)$";

    #endregion


    #region Fields

    private readonly AppHostFixture host;

    #endregion


    #region Construction

    /// <summary>Takes the running application from the collection fixture.</summary>
    protected ScenarioBase(AppHostFixture host)
    {
        ArgumentNullException.ThrowIfNull(host);

        this.host = host;
    }

    #endregion


    #region Properties

    /// <summary>The running application.</summary>
    protected AppHostFixture Host
    {
        get
        {
            return host;
        }
    }

    #endregion


    #region Arrangement

    /// <summary>
    /// Empties the database. Every scenario starts with this rather than relying on the
    /// order scenarios happen to run in.
    /// </summary>
    protected async Task StartCleanAsync()
    {
        await host.ResetStateAsync();
    }


    /// <summary>
    /// Opens a page signed in as the given person, in a context of its own.
    ///
    /// A context per person is what makes two people in one run possible, which is the
    /// whole reason the vote count and the nominal list can be verified at all.
    /// </summary>
    protected async Task<IPage> OpenAsAsync(string objectId)
    {
        return await OpenAsAsync(objectId, true);
    }


    /// <summary>
    /// Opens a page signed in as the given person, with the browser's script engine turned
    /// off.
    ///
    /// The specification requires the vote button to work as a plain form, and says the
    /// behaviour has to be identical either way. Turning the engine off is the only way to
    /// reach that path, because the enhancement script is served unconditionally and a
    /// scenario cannot ask the application not to send it.
    /// </summary>
    protected async Task<IPage> OpenWithoutScriptAsAsync(string objectId)
    {
        return await OpenAsAsync(objectId, false);
    }


    /// <summary>Builds an address inside the application under test.</summary>
    protected string Url(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return host.BaseAddress.TrimEnd('/') + path;
    }

    #endregion


    #region Actions

    /// <summary>
    /// Fills the submission form and submits it, the way a person does.
    ///
    /// It deliberately does not wait for the result. What follows a submission is the
    /// thing several scenarios are there to assert, and a helper that waited for it would
    /// be doing the assertion's job and turning a failed redirect into a timeout inside a
    /// helper rather than a failure on the line that says what was expected.
    /// </summary>
    protected async Task SubmitIdeaAsync(IPage page, string title, string description)
    {
        ArgumentNullException.ThrowIfNull(page);

        await page.GotoAsync(Url("/nova"));

        await page.GetByLabel("Título").FillAsync(title);
        await page.GetByLabel("Descrição").FillAsync(description);

        await page.GetByRole(AriaRole.Button, new() { Name = UiText.SubmitIdeaButton }).ClickAsync();
    }


    /// <summary>
    /// Submits an idea and reports the identifier the application gave it, read from the
    /// address of the page the author lands on.
    /// </summary>
    protected async Task<int> SubmitIdeaAndReadIdAsync(IPage page, string title, string description)
    {
        await SubmitIdeaAsync(page, title, description);

        await page.WaitForURLAsync(new Regex(IdeaPagePattern));

        Match match = Regex.Match(page.Url, IdeaPagePattern);

        if (!match.Success)
        {
            throw new InvalidOperationException(
                $"Submitting did not land on an idea's own page. Address: {page.Url}");
        }

        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    #endregion


    #region Internals

    /// <summary>Opens a page as the given person, with or without a script engine.</summary>
    private async Task<IPage> OpenAsAsync(string objectId, bool scriptEnabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        IBrowserContext context = await Browser.NewContextAsync(
            new BrowserNewContextOptions { JavaScriptEnabled = scriptEnabled });

        await context.AddCookiesAsync(
        [
            new Cookie
            {
                Name = PersonCookieName,
                Value = objectId,
                Domain = "127.0.0.1",
                Path = "/",
            },
        ]);

        return await context.NewPageAsync();
    }

    #endregion
}
