using IdeaBoard.E2E.Fixtures;
using Microsoft.Playwright;
using Xunit;

namespace IdeaBoard.E2E.Scenarios;

/// <summary>
/// At the bottom of the board, nothing a person can press is underneath the floating
/// button.
///
/// WHY THE BOTTOM AND NOT EVERYWHERE. An element fixed to the foot of the window covers a
/// band of the page whatever the scroll, so at most scroll positions something is behind
/// it. Everywhere but one, a scroll moves it out. At the very bottom there is no scroll
/// left, so whatever is under the button there cannot be reached at all, by any means.
///
/// That is the line this draws, and it is the line that matters: the button is the one
/// that submits an idea, and the control most often behind it is the one that votes. A
/// press that lands on the wrong one of those does not fail, it does something else.
///
/// The same thing was already measured once on a phone, which is why the narrow layout
/// puts this button back in the bar. Nobody checked the wide layout at the time, and it
/// was wrong there too, by fourteen pixels of luck.
/// </summary>
[Collection(AppHostCollection.Name)]
public class BotaoFlutuanteTests : ScenarioBase
{
    #region Construction

    /// <summary>Takes the running application from the collection fixture.</summary>
    public BotaoFlutuanteTests(AppHostFixture host)
        : base(host)
    {
    }

    #endregion


    #region Scenarios

    /// <summary>
    /// With a board long enough to scroll, no control sits under the floating button once
    /// the page is scrolled as far as it goes.
    ///
    /// Several window shapes, because the button is placed against the window and the
    /// board reflows with its width: the number of columns, and therefore where the last
    /// row's buttons land, changes from one of these to the next.
    /// </summary>
    [Theory]
    [InlineData(1280, 800)]
    [InlineData(1280, 1000)]
    [InlineData(1366, 900)]
    [InlineData(1600, 800)]
    [InlineData(1600, 1000)]
    public async Task NothingPressableHidesUnderTheFloatingButtonAtTheFootOfTheBoard(
        int width,
        int height)
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        // Enough of them that the board runs past the bottom of the window at every shape
        // above, because a page that does not scroll cannot show this failing.
        for (int i = 1; i <= 9; i = i + 1)
        {
            await SubmitIdeaAndWaitAsync(ana, string.Concat("Ideia numero ", i), "Descricao.");
        }

        await ana.SetViewportSizeAsync(width, height);
        await ana.GotoAsync(Url("/"));

        await ana.EvaluateAsync("window.scrollTo(0, document.documentElement.scrollHeight)");

        // The button is found by its own class rather than by its words, so that this goes
        // on working if the wording of the action changes.
        //
        // TWO ANSWERS, because one of them is not enough on its own. Whether anything is
        // behind the button right now depends on where the last row's cards happen to
        // land, and the grid reflows: with nine ideas the final row holds a single card in
        // the FIRST column, so the button has empty grid beside it and an overlap test
        // passes while the page has no clearance at all. Measured, after this test was
        // first written the wrong way round.
        //
        // So the reserved band is read off the live button and compared against the real
        // gap below the lowest control on the page. That comparison does not care how the
        // cards fell.
        string report = await ana.EvaluateAsync<string>(
            """
            () =>
            {
                const floating = document.querySelector(".botao-submeter");

                if (floating === null)
                {
                    return "SEM BOTAO";
                }

                const style = getComputedStyle(floating);

                if (style.position !== "fixed" || style.display === "none")
                {
                    return "NAO FLUTUA";
                }

                const f = floating.getBoundingClientRect();

                // How far up from the foot of the window the button reaches: its height
                // plus however far it sits off the bottom. Taken from the button itself,
                // so no number here has to be kept in step with the stylesheet.
                const band = Math.round(window.innerHeight - f.top);

                const controls = Array.from(document.querySelectorAll(".lista-ideias a, .lista-ideias button"));
                const hits = [];
                let lowest = 0;

                for (const control of controls)
                {
                    const r = control.getBoundingClientRect();
                    const bottomInPage = r.bottom + window.scrollY;

                    if (bottomInPage > lowest)
                    {
                        lowest = bottomInPage;
                    }

                    if (r.left < f.right && r.right > f.left && r.top < f.bottom && r.bottom > f.top)
                    {
                        hits.push((control.textContent || "").trim().slice(0, 40));
                    }
                }

                const clearance = Math.round(document.documentElement.scrollHeight - lowest);

                return band + ";" + clearance + ";" + hits.join(" | ");
            }
            """);

        Assert.False(
            report == "SEM BOTAO",
            "The board no longer carries the floating action at all, so either it was "
            + "removed or this is looking for the wrong thing.");

        Assert.False(
            report == "NAO FLUTUA",
            "The floating action is no longer fixed to the window at this width, which "
            + "either undoes a decision or means this test is checking the wrong width.");

        string[] parts = report.Split(';');

        int band = int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
        int clearance = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
        string hits = parts[2];

        Assert.True(
            clearance >= band,
            string.Concat(
                "At ",
                width.ToString(),
                "x",
                height.ToString(),
                ", the floating button reaches ",
                band.ToString(),
                "px up from the foot of the window and the page leaves only ",
                clearance.ToString(),
                "px below its lowest control. Scrolled all the way down there is nowhere "
                + "left to go, so whatever falls in that band cannot be reached at all."));

        Assert.True(
            hits.Length == 0,
            string.Concat(
                "At ",
                width.ToString(),
                "x",
                height.ToString(),
                ", with the board scrolled as far as it goes, the floating button covers a "
                + "control that cannot be scrolled out from under it: ",
                hits));
    }

    #endregion
}
