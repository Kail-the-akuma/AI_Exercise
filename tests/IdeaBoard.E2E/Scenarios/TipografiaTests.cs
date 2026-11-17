using IdeaBoard.E2E.Fixtures;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using Xunit;

namespace IdeaBoard.E2E.Scenarios;

/// <summary>
/// The type faces the stylesheet declares are not merely served, they load.
///
/// WHY THIS EXISTS SEPARATELY FROM THE HOST TEST. PublishedHostTests proves the files come
/// back with a 200 and begin with the woff2 signature. That is as far as an HttpClient can
/// go, and it is not far enough: a woff2 carries its signature in the first four bytes and
/// everything that can be wrong with it is in the rest. A file was produced during this
/// work whose only fault was a length that was not a multiple of four, which the format
/// requires; it answered 200, it began with wOF2, and Chromium refused it outright. The
/// host test would have passed it.
///
/// AND THE FAILURE IS SILENT BY DESIGN. The stylesheet declares font-display: swap on
/// purpose, so a face that never loads leaves a page that is perfectly readable in the
/// system stack. Nothing goes red, nothing looks broken, and the only person who finds out
/// is somebody who already knows what the page is supposed to look like.
///
/// NOTHING HERE NAMES THE FACE. The faces are taken from the document's own font set,
/// which is built from the @font-face rules the stylesheet declared, so this goes on being
/// true after a rename and fails if the declarations disappear.
/// </summary>
[Collection(AppHostCollection.Name)]
public class TipografiaTests : ScenarioBase
{
    #region Construction

    /// <summary>Takes the running application from the collection fixture.</summary>
    public TipografiaTests(AppHostFixture host)
        : base(host)
    {
    }

    #endregion


    #region Scenarios

    /// <summary>
    /// Every face the stylesheet declares is asked for by a real browser and reaches the
    /// loaded state.
    ///
    /// It is done signed in and on the board, because the stylesheet under test belongs to
    /// the application. The person picker that an anonymous visitor lands on is served by
    /// the local host instead, carries a layout of its own, and declares no face at all:
    /// run against it, this passes or fails on the wrong document.
    /// </summary>
    [Fact]
    public async Task EveryDeclaredTypeFaceActuallyLoadsInTheBrowser()
    {
        await StartCleanAsync();

        IPage ana = await OpenAsAsync(People.AnaObjectId);

        await ana.GotoAsync(Url("/"));

        // Each face is asked for individually and its outcome recorded, rather than
        // awaiting the set as a whole. Awaiting the whole set reports one failure for an
        // unknown member, and the point of this test is to say WHICH file is bad.
        string report = await ana.EvaluateAsync<string>(
            """
            async () =>
            {
                const faces = [];
                document.fonts.forEach((face) => faces.push(face));

                if (faces.length === 0)
                {
                    return "NENHUMA";
                }

                const lines = [];

                for (const face of faces)
                {
                    const name = face.family + " " + face.weight + " " + face.style;

                    try
                    {
                        await face.load();
                        lines.push(name + " => " + face.status);
                    }
                    catch (error)
                    {
                        lines.push(name + " => " + error.name + ": " + error.message);
                    }
                }

                return lines.join("\n");
            }
            """);

        Assert.False(
            report == "NENHUMA",
            "The document declares no type face at all. Either the stylesheet lost its "
            + "@font-face rules or it is no longer reaching the page.");

        string[] lines = report.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Both of them. One rule covered every weight while the face was variable, and a
        // suite that counts nothing would not notice the day one of two static files stops
        // being declared.
        Assert.Equal(2, lines.Length);

        foreach (string line in lines)
        {
            Assert.True(
                line.EndsWith("=> loaded", StringComparison.Ordinal),
                string.Concat(
                    "A type face the stylesheet declares did not load in the browser, and "
                    + "font-display: swap means nothing on the page will show it: ",
                    line));
        }
    }

    #endregion
}
