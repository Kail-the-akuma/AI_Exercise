using System.Text.RegularExpressions;
using IdeaBoard.E2E.Fixtures;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using Xunit;

namespace IdeaBoard.E2E.Scenarios;

/// <summary>
/// Proves the harness itself: a real socket, the real pipeline, and a real browser.
/// It asserts nothing about the product beyond the page arriving.
/// </summary>
[Collection(AppHostCollection.Name)]
public class ArnesTests : PageTest
{
    #region Fields

    private readonly AppHostFixture host;

    #endregion


    #region Construction

    /// <summary>Takes the running application from the collection fixture.</summary>
    public ArnesTests(AppHostFixture host)
    {
        ArgumentNullException.ThrowIfNull(host);

        this.host = host;
    }

    #endregion


    #region Scenarios

    /// <summary>
    /// Opens the application and confirms a page comes back over a real connection.
    ///
    /// The picker is the landing point because the local host sends anyone without a chosen
    /// person there, so this is what the first request of any run actually produces.
    /// </summary>
    [Fact]
    public async Task ApplicationAnswersOverARealConnection()
    {
        await host.ResetStateAsync();

        IResponse? response = await Page.GotoAsync(host.BaseAddress);

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);

        await Expect(Page).ToHaveURLAsync(new Regex("/pessoa"));
    }

    #endregion
}
