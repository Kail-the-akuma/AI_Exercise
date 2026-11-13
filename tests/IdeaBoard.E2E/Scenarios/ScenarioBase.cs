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
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        IBrowserContext context = await Browser.NewContextAsync();

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


    /// <summary>Builds an address inside the application under test.</summary>
    protected string Url(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return host.BaseAddress.TrimEnd('/') + path;
    }

    #endregion
}
