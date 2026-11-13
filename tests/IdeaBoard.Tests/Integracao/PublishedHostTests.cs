using System.Net;
using Microsoft.AspNetCore.Authentication;
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
        environment.Set("AzureAd__Instance", "https://login.microsoftonline.com/");
        environment.Set("AzureAd__TenantId", "11111111-1111-1111-1111-111111111111");
        environment.Set("AzureAd__ClientId", "22222222-2222-2222-2222-222222222222");
        environment.Set("AzureAd__CallbackPath", "/signin-oidc");
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

    #endregion

    #region Authorisation

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
