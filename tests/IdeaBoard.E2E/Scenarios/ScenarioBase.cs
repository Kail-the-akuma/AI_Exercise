using System.Diagnostics;
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
        await SubmitIdeaAndWaitAsync(page, title, description);

        Match match = Regex.Match(page.Url, IdeaPagePattern);

        if (!match.Success)
        {
            throw new InvalidOperationException(
                $"Submitting did not land on an idea's own page. Address: {page.Url}");
        }

        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }


    /// <summary>
    /// Submits an idea and waits until the browser is on the idea's own page.
    ///
    /// Every scenario that needs a board with ideas on it comes through here, so this is
    /// the single place that knows how to wait for a submission to land.
    /// </summary>
    protected async Task SubmitIdeaAndWaitAsync(IPage page, string title, string description)
    {
        ArgumentNullException.ThrowIfNull(page);

        // Every response and every navigation is timed, because the state of the page after
        // a timeout cannot say WHEN it got there. A page sitting correctly on the idea at
        // the end of thirty seconds looks identical whether the server answered in two
        // tenths and the wait missed it, or the server took twenty nine seconds. Only the
        // instants tell those apart, and they are gone by the time anybody asks.
        List<string> timeline = new List<string>();
        Stopwatch clock = Stopwatch.StartNew();

        void RecordResponse(object? sender, IResponse response)
        {
            lock (timeline)
            {
                timeline.Add(Moment(clock) + " resposta " + response.Status + " " + response.Url);
            }
        }

        void RecordNavigation(object? sender, IFrame frame)
        {
            lock (timeline)
            {
                timeline.Add(Moment(clock) + " navegou para " + frame.Url);
            }
        }

        page.Response += RecordResponse;
        page.FrameNavigated += RecordNavigation;

        try
        {
            await SubmitIdeaAsync(page, title, description);

            try
            {
                // This asserts the address by polling rather than waiting for a navigation
                // event, and the difference is the whole reason this method exists.
                //
                // An event wait subscribes AFTER the click that causes the navigation has
                // already been awaited. When the lifecycle finishes on that boundary the
                // event has already been and gone, nothing wakes the wait, and it sits
                // until its timeout although the page is sitting on the right address the
                // entire time. A poll cannot miss a state that is already true.
                //
                // This is measured and not deduced. Timing the responses showed the server
                // answering the submission, redirecting and serving the idea in about 130
                // milliseconds, after which the wait sat for its full thirty seconds and
                // failed. The rate was 4 failed runs in 85, roughly 0,2% per call, which is
                // low enough to look like bad luck and often enough to break a suite weekly.
                //
                // The observable trigger, if this ever comes back: a failure here whose
                // timeline below shows the idea page already served, early. That means the
                // wait missed it again and the cure is a stricter poll, never a longer
                // timeout and never a retry, both of which hide the fault instead of
                // removing it.
                //
                // The timeout is deliberately the same thirty seconds the event wait had,
                // so that this change moves only HOW the waiting is done and not how long
                // it is given.
                await Expect(page).ToHaveURLAsync(
                    new Regex(IdeaPagePattern),
                    new PageAssertionsToHaveURLOptions { Timeout = 30000 });
            }
            catch (Exception failure)
            {
                // Broad on purpose, and it widens nothing: the exception is rethrown with
                // more information, never swallowed. Which type arrives is the library's
                // own business and it changes with the way of waiting. Measured in
                // Playwright 1.64.1: the waiter throws System.TimeoutException and the
                // assertion throws PlaywrightException, and this version declares no
                // Microsoft.Playwright.TimeoutException at all. Naming one of them here
                // would silently stop reporting the other.
                //
                // A failure here says only that the address never became the idea's own
                // page, and leaves the possible reasons indistinguishable. The state of the
                // page and the timeline of what happened are reported together, rather than
                // left to be guessed at from a stack trace.
                throw new InvalidOperationException(
                    await DescribeStuckSubmissionAsync(page, timeline),
                    failure);
            }
        }
        finally
        {
            page.Response -= RecordResponse;
            page.FrameNavigated -= RecordNavigation;
        }
    }

    /// <summary>
    /// Says what the page was showing when a submission failed to land on the idea.
    ///
    /// It is deliberately wordy. This runs once in a few dozen runs, usually on somebody
    /// else's machine, and the cost of a long message is nothing against the cost of
    /// reproducing the failure a second time to find out which of two causes it was.
    /// </summary>
    private static string Moment(Stopwatch clock)
    {
        return clock.Elapsed.TotalSeconds.ToString("00.000", CultureInfo.InvariantCulture) + "s";
    }


    private static async Task<string> DescribeStuckSubmissionAsync(IPage page, List<string> timeline)
    {
        string address = page.Url;
        string heading = "nao foi possivel ler";
        string errors = "nenhum";
        string bodyStart = "nao foi possivel ler";

        try
        {
            ILocator errorParagraphs = page.Locator("p.erro");

            int errorCount = await errorParagraphs.CountAsync();

            if (errorCount > 0)
            {
                string[] messages = (await errorParagraphs.AllTextContentsAsync()).ToArray();

                errors = string.Join(" | ", messages);
            }

            heading = await page.TitleAsync();

            string body = await page.Locator("body").InnerTextAsync();

            if (body.Length > 400)
            {
                body = body.Substring(0, 400);
            }

            bodyStart = body.Replace("\r", " ").Replace("\n", " ");
        }
        catch (PlaywrightException)
        {
            // The page itself may be unusable, which is worth reporting as such rather than
            // replacing the original failure with a second one.
        }

        string events;

        lock (timeline)
        {
            if (timeline.Count == 0)
            {
                events = "nada, nem um pedido saiu";
            }
            else
            {
                events = Environment.NewLine + "    " + string.Join(Environment.NewLine + "    ", timeline);
            }
        }

        return
            "Submitting did not land on an idea\'s own page within the timeout." + Environment.NewLine +
            "  Address: " + address + Environment.NewLine +
            "  Title:   " + heading + Environment.NewLine +
            "  Errors:  " + errors + Environment.NewLine +
            "  Body:    " + bodyStart + Environment.NewLine +
            "  Timeline:" + events;
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
