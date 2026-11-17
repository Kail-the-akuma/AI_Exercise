using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.Extensions.Options;
using IdeaBoard.App.Pages;
using IdeaBoard.App.Pipeline;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// The composition root that actually gets published, started in process.
///
/// Nothing else executes it. Without this it would be composed for the first time on the
/// day it is deployed, and the first thing anyone would learn about a missing registration
/// would be a stack trace on a server.
///
/// The sign-in values are fictitious, which is enough: the validator checks that they are
/// present and never that they are real, and static files are served before authorisation,
/// so the request below never goes near Entra ID.
/// </summary>
[Collection(HostTestCollection.Name)]
public class PublishedHostTests
{
    #region Helpers

    private static CancellationToken Cancellation
    {
        get
        {
            return TestContext.Current.CancellationToken;
        }
    }


    private static void ConfigureValidEnvironment(
        EnvironmentVariableScope environment,
        TemporarySqliteDatabase database)
    {
        environment.Set("ASPNETCORE_ENVIRONMENT", "Development");
        environment.Set("ConnectionStrings__IdeaBoard", database.ConnectionString);
        environment.Set("Localization__TimeZoneId", "Europe/Lisbon");
    }

    #endregion


    #region Startup

    /// <summary>
    /// The host reaches the running state against a provisioned database, which is the
    /// proof that the whole service graph composes.
    /// </summary>
    [Fact]
    public void ThePublishedHostStartsAgainstAProvisionedDatabase()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        Assert.Equal("wal", database.EnableWriteAheadLogging());

        using EnvironmentVariableScope environment = new EnvironmentVariableScope();
        ConfigureValidEnvironment(environment, database);

        using IdeaBoardWebFactory factory = new IdeaBoardWebFactory();
        using HttpClient client = factory.CreateClient();

        IHostApplicationLifetime lifetime = factory.Services.GetRequiredService<IHostApplicationLifetime>();

        Assert.True(
            lifetime.ApplicationStarted.IsCancellationRequested,
            "The host must have reached the running state.");
    }


    /// <summary>
    /// A static file of the Razor class library is served under the _content prefix.
    ///
    /// That prefix is how a referenced library's assets are addressed, and it is the part
    /// nothing else exercises: a page rendered in another test would reference the file by
    /// name without anyone noticing that the URL answers nothing.
    ///
    /// Two hundred and not a redirect: the application deliberately composes no HTTPS
    /// redirection, because that belongs to the site in front of it.
    /// </summary>
    [Fact]
    public async Task AStaticFileOfTheRazorClassLibraryIsServed()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        Assert.Equal("wal", database.EnableWriteAheadLogging());

        using EnvironmentVariableScope environment = new EnvironmentVariableScope();
        ConfigureValidEnvironment(environment, database);

        using IdeaBoardWebFactory factory = new IdeaBoardWebFactory();

        // No redirect is followed, so that a redirect shows up as a status code instead of
        // being chased quietly and reported as whatever sits at the other end.
        WebApplicationFactoryClientOptions options = new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        };

        using HttpClient client = factory.CreateClient(options);

        using HttpResponseMessage response = await client.GetAsync(
            "/_content/IdeaBoard.App/css/site.css",
            Cancellation);

        Assert.NotEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }


    /// <summary>
    /// The type face the stylesheet asks for is actually served, and what comes back is
    /// actually a font.
    ///
    /// THIS IS THE ONE STATIC FILE WHOSE ABSENCE IS INVISIBLE. The stylesheet declares
    /// font-display: swap, deliberately, so that the page is readable before the face
    /// arrives and stays readable if it never does. That is right for a reader and it is
    /// also what hides the failure: a 404 on the font and a font that loaded look the same
    /// to every other test in this solution, to a smoke test, and to anybody opening the
    /// page who does not already know what it should look like.
    ///
    /// THE PATH IS READ OUT OF THE STYLESHEET AND NOT WRITTEN HERE. A test that named the
    /// address itself would be a third copy of it, next to the one in the stylesheet and
    /// the one in the folder, and it would go on passing on the day the stylesheet started
    /// asking for something else. What is checked is the agreement between what the page
    /// requests and what the host answers, which is the thing that actually breaks when a
    /// project is renamed, a file is moved, or an asset does not make it into the publish
    /// output.
    ///
    /// The first four bytes are checked as well, because a 200 is not proof of a font. A
    /// request that falls through to something else, or a rewrite that answers every
    /// unknown path with a page, both return 200 with a body that is not a type face.
    ///
    /// EVERY FACE THE STYLESHEET DECLARES, and not the first one. This used to take the
    /// first match and stop, which was right while one variable file covered every weight.
    /// It stopped being right the day the sheet started declaring a regular and a bold:
    /// the bold could have been missing from the publish output and this would have gone
    /// on passing, having proved something true about the other file.
    ///
    /// THE FILE'S OWN LENGTH IS CHECKED AGAINST WHAT ARRIVED. A woff2 header carries the
    /// total size of the file in bytes 8 to 11, and the two faces here declare 25080 and
    /// 24488, each equal to its length on disk, measured. Comparing them is what separates
    /// "begins like a font" from "is the whole file": a body cut short by a proxy, by a
    /// partial copy into the publish output or by a connection that ended early keeps the
    /// signature intact and loses nothing a signature check can see.
    ///
    /// WHAT THIS STILL CANNOT SEE. A file that is complete and is not a loadable font. The
    /// checks below are a signature and a length, not a parse, and a woff2 can be whole and
    /// still be refused by the browser: it happened here, to a file whose only fault was
    /// that its length was not a multiple of four. That gap is covered in the end to end
    /// suite, which has a real browser to ask.
    /// </summary>
    [Fact]
    public async Task EveryTypeFaceTheStylesheetAsksForIsServedAndIsAFont()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        Assert.Equal("wal", database.EnableWriteAheadLogging());

        using EnvironmentVariableScope environment = new EnvironmentVariableScope();
        ConfigureValidEnvironment(environment, database);

        using IdeaBoardWebFactory factory = new IdeaBoardWebFactory();

        WebApplicationFactoryClientOptions options = new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        };

        using HttpClient client = factory.CreateClient(options);

        string stylesheetPath = "/_content/IdeaBoard.App/css/site.css";

        using HttpResponseMessage stylesheet = await client.GetAsync(stylesheetPath, Cancellation);

        Assert.Equal(HttpStatusCode.OK, stylesheet.StatusCode);

        string css = await stylesheet.Content.ReadAsStringAsync(Cancellation);

        List<string> fontPaths = ResolveFontUrls(css, stylesheetPath);

        foreach (string fontPath in fontPaths)
        {
            using HttpResponseMessage font = await client.GetAsync(fontPath, Cancellation);

            Assert.NotEqual(HttpStatusCode.TemporaryRedirect, font.StatusCode);
            Assert.NotEqual(HttpStatusCode.Found, font.StatusCode);

            Assert.Equal(
                HttpStatusCode.OK,
                font.StatusCode);

            byte[] body = await font.Content.ReadAsByteArrayAsync(Cancellation);

            Assert.True(
                body.Length > 0,
                string.Concat("The type face at ", fontPath, " came back empty."));

            // The signature every woff2 file begins with. A body that is not one means the
            // request was answered by something that is not the file.
            Assert.True(
                body.Length >= 4
                && body[0] == (byte)'w'
                && body[1] == (byte)'O'
                && body[2] == (byte)'F'
                && body[3] == (byte)'2',
                string.Concat(
                    "What came back from ",
                    fontPath,
                    " answered with 200 but does not begin with the woff2 signature, so it "
                    + "is not the type face."));

            Assert.True(
                body.Length >= 12,
                string.Concat(
                    "What came back from ",
                    fontPath,
                    " is too short to hold a woff2 header, so its declared length cannot "
                    + "even be read."));

            uint declaredLength = BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(8, 4));

            Assert.True(
                declaredLength == (uint)body.Length,
                string.Concat(
                    "The type face at ",
                    fontPath,
                    " says in its own header that it is ",
                    declaredLength.ToString(CultureInfo.InvariantCulture),
                    " bytes long and ",
                    body.Length.ToString(CultureInfo.InvariantCulture),
                    " arrived, so what was served is not the whole file."));
        }
    }


    /// <summary>
    /// The address of every type face the stylesheet asks for, each resolved the way a
    /// browser resolves it: relative to the stylesheet it was written in.
    /// </summary>
    private static List<string> ResolveFontUrls(string css, string stylesheetPath)
    {
        MatchCollection matches = Regex.Matches(css, @"src:\s*url\(\s*[""']?([^""')]+)[""']?\s*\)");

        Assert.True(
            matches.Count > 0,
            "The stylesheet declares no type face at all, so either the face was dropped "
            + "or this test is looking for the wrong thing.");

        Uri stylesheetUri = new Uri(new Uri("http://localhost"), stylesheetPath);
        List<string> paths = new List<string>();

        foreach (Match match in matches)
        {
            string declared = match.Groups[1].Value.Trim();
            Uri fontUri = new Uri(stylesheetUri, declared);

            paths.Add(fontUri.AbsolutePath);
        }

        return paths;
    }

    #endregion

    #region Authorisation

    /// <summary>
    /// The two constants that name the Windows scheme carry the same value.
    ///
    /// This exists because the composition root says so in a comment, and a claim a reader
    /// would trust without rechecking belongs in something that runs. If the two ever
    /// diverge, the statement that choosing between them is not a hosting model decision
    /// stops being true, and this is what says so.
    ///
    /// It is also a correction to the record. The comment it backs once carried a second
    /// reason, that the other constant could not be used without an extra package, and that
    /// reason was never measured and was false. A right decision resting on a wrong
    /// measurement looks verified and is not.
    /// </summary>
    [Fact]
    public void BothWindowsSchemeConstantsNameTheSameScheme()
    {
        Assert.Equal(IISServerDefaults.AuthenticationScheme, IISDefaults.AuthenticationScheme);
        Assert.Equal("Windows", IISDefaults.AuthenticationScheme);
    }


    /// <summary>
    /// The published host challenges with the scheme the WEB SERVER provides, and carries
    /// no federated sign-in at all.
    ///
    /// This is asserted against the service graph rather than against a response, for a
    /// reason that is itself the finding. The Windows scheme has no handler inside this
    /// process: the server registers it, and these tests do not run under that server. A
    /// request therefore cannot produce a real challenge here however the host is
    /// configured, so asserting on the shape of one would be asserting on an artefact.
    ///
    /// What CAN be proved here is the configuration itself, and it is the thing that would
    /// regress: if the federated sign-in were put back, this fails.
    /// </summary>
    [Fact]
    public async Task ThePublishedHostChallengesWithTheServerScheme()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        Assert.Equal("wal", database.EnableWriteAheadLogging());

        using EnvironmentVariableScope environment = new EnvironmentVariableScope();
        ConfigureValidEnvironment(environment, database);

        using IdeaBoardWebFactory factory = new IdeaBoardWebFactory();
        using HttpClient client = factory.CreateClient();

        AuthenticationOptions authentication =
            factory.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        Assert.Equal(IISDefaults.AuthenticationScheme, authentication.DefaultScheme);

        IAuthenticationSchemeProvider schemes =
            factory.Services.GetRequiredService<IAuthenticationSchemeProvider>();

        IEnumerable<AuthenticationScheme> registered = await schemes.GetAllSchemesAsync();

        // Nothing is registered in this process, because the scheme belongs to the server.
        // That absence is the assertion: a handler registered here would mean the host had
        // acquired a sign-in mechanism of its own again.
        foreach (AuthenticationScheme scheme in registered)
        {
            Assert.Fail(
                $"The published host registers the authentication scheme '{scheme.Name}'. " +
                "Identity is supplied by the web server and this host registers no handler.");
        }
    }


    /// <summary>
    /// An unauthenticated request for a page is not served with content, and does not fail
    /// for want of a network.
    ///
    /// The second half is what this test is now for. Before the switch, this request
    /// answered 500 because the sign-in library could not fetch metadata from its
    /// authority: a published host pointed at a tenant that did not answer sent nobody to
    /// sign in, it returned a server error with nothing to explain it. There is no longer
    /// anything in this host that can do that, and the assertions below are what would
    /// notice if that came back.
    ///
    /// WHAT THIS DOES NOT ESTABLISH. It does not prove the challenge is well formed,
    /// because no real challenge can happen outside the web server that provides the
    /// scheme. That half stays on the manual verification list, together with the one
    /// setting nothing here can see: anonymous authentication must be off, or the server
    /// lets unidentified requests through and the application cannot tell.
    /// </summary>
    [Fact]
    public async Task APageIsNotServedToAnUnauthenticatedRequest()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        Assert.Equal("wal", database.EnableWriteAheadLogging());

        using EnvironmentVariableScope environment = new EnvironmentVariableScope();
        ConfigureValidEnvironment(environment, database);

        using IdeaBoardWebFactory factory = new IdeaBoardWebFactory();

        // The redirect is not followed, so a redirect shows up as a status code here
        // instead of being chased to whatever sits at the other end.
        WebApplicationFactoryClientOptions options = new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        };

        using HttpClient client = factory.CreateClient(options);

        using HttpResponseMessage response = await client.GetAsync("/", Cancellation);

        string body = await response.Content.ReadAsStringAsync(Cancellation);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);

        AssertCarriesNoPageContent(body);
        AssertDidNotFailForWantOfANetwork(body);

        // A challenge and not the name gate. The two answer with the same status code, and
        // the gate has no business acting on somebody who has not been identified yet.
        // Measured to be load bearing: with the fallback authorisation policy removed, this
        // is the line that goes red, because the request reaches the application and the
        // gate redirects.
        if (response.Headers.Location is not null)
        {
            Assert.NotEqual(
                DisplayNameGateMiddleware.RegistrationPath,
                response.Headers.Location.ToString());
        }
    }


    /// <summary>
    /// Asserts the response does not carry the signature of a failed authority lookup.
    ///
    /// These are the words the previous arrangement failed with, kept verbatim because
    /// recognising the old failure is the whole point. Nothing in this host should be able
    /// to reach the network any more, at startup or on a request.
    /// </summary>
    private static void AssertDidNotFailForWantOfANetwork(string body)
    {
        string[] networkFailureSignatures =
        [
            "IDX20803",
            "IDX20807",
            "well-known/openid-configuration",
            "Unable to obtain configuration",
        ];

        foreach (string signature in networkFailureSignatures)
        {
            Assert.DoesNotContain(signature, body, StringComparison.OrdinalIgnoreCase);
        }
    }


    /// <summary>
    /// Asserts the response carries none of the application's own words, checking the body
    /// both as it arrived and with HTML entities decoded.
    ///
    /// BOTH FORMS, and this is the correction of a dead assertion rather than belt and
    /// braces. Razor encodes every non ASCII character as a numeric entity, so the empty
    /// list message travels as "Ainda n&#xE3;o h&#xE1; ideias", and a literal comparison
    /// against the raw body can never match it. In a NEGATIVE assertion that does not fail,
    /// it passes: the check would have reported success even if the page had been served in
    /// full. Two of the three strings here happen to be pure ASCII and were doing the work
    /// of all three.
    ///
    /// Checking both forms is also stronger than decoding alone, because it assumes nothing
    /// about which encoding the framework applies. A change in either direction still fails
    /// this.
    /// </summary>
    private static void AssertCarriesNoPageContent(string body)
    {
        string decoded = WebUtility.HtmlDecode(body);

        string[] pageContent =
        [
            UiText.SubmitIdeaButton,
            UiText.RankingTab,
            UiText.RecentTab,
            UiText.EmptyList,
            UiText.PublicVoteWarning,
        ];

        foreach (string content in pageContent)
        {
            Assert.DoesNotContain(content, body, StringComparison.Ordinal);
            Assert.DoesNotContain(content, decoded, StringComparison.Ordinal);
        }
    }


    /// <summary>Keeps a failure message readable when the body is a whole error page.</summary>
    private static string Shorten(string body)
    {
        string collapsed = body.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

        if (collapsed.Length <= 600)
        {
            return collapsed;
        }

        return collapsed[..600];
    }

    #endregion
}
