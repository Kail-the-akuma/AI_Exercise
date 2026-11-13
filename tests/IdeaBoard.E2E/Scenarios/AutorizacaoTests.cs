using IdeaBoard.App.Pages;
using IdeaBoard.E2E.Fixtures;
using Microsoft.Playwright;
using Xunit;

namespace IdeaBoard.E2E.Scenarios;

/// <summary>
/// The one scenario that asserts a request is refused rather than served.
/// </summary>
[Collection(AppHostCollection.Name)]
public class AutorizacaoTests : ScenarioBase
{
    /// <inheritdoc />
    public AutorizacaoTests(AppHostFixture host)
        : base(host)
    {
    }


    /// <summary>
    /// A request carrying no usable identity must be refused by the authorisation policy,
    /// not served.
    ///
    /// WHY THE COOKIE IS PRESENT AND NOT ABSENT, and do not "simplify" this away. The local
    /// host redirects anyone without the cookie to the picker, in middleware that runs
    /// BEFORE authorisation. A request with no cookie therefore exercises the picker and
    /// says nothing at all about the policy. A cookie holding an identifier no configured
    /// person has gets past the middleware, which only checks that the cookie exists,
    /// reaches the handler, which returns no result, and lands on the policy, which is the
    /// thing being verified.
    ///
    /// This also detects, for free, a default person being added to the shared identity
    /// handler: the moment one exists, this request starts being served.
    ///
    /// The assertion is that the request was REFUSED, and deliberately not that the status
    /// is any particular number. Production challenges through a different scheme, so a
    /// hard-coded 401 would describe this host rather than the rule. What it does assert is
    /// that the response is a refusal and not some other failure, because a 500 also fails
    /// to serve the page and must not pass for success here.
    /// </summary>
    [Fact]
    public async Task RequestWithoutAUsableIdentityIsRefused()
    {
        await StartCleanAsync();

        IPage page = await OpenAsAsync(People.UnknownObjectId);

        IResponse? response = await page.GotoAsync(Url("/"));

        Assert.NotNull(response);

        bool isChallenge = response.Status == 401
            || response.Status == 403
            || (response.Status >= 300 && response.Status <= 399);

        Assert.True(
            isChallenge,
            "Expected the request to be refused by the authorisation policy, but the status was "
                + response.Status.ToString() + ".");

        // Guards this scenario against being quietly degraded into a test of the picker
        // middleware. If somebody drops the cookie, the middleware redirects to /pessoa,
        // which is a 302 and would satisfy the check above while verifying nothing about
        // the policy. Measured on this host, the refusal keeps the requested address.
        Assert.DoesNotContain("/pessoa", page.Url, StringComparison.Ordinal);

        // And the protected page's own content must not have arrived. On a clean database
        // the ranking shows the invitation to submit the first idea, so its absence is what
        // separates a refusal from a page that was served after all.
        await Expect(page.GetByText(UiText.EmptyList)).Not.ToBeVisibleAsync();
    }
}
