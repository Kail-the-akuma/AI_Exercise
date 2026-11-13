using System.Net;
using IdeaBoard.App.Pages;
using IdeaBoard.App.Pipeline;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
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
    /// Status codes that count as a CHALLENGE, meaning the application refused to serve a
    /// page and asked for identity instead.
    ///
    /// ENVIRONMENT DEPENDENCY: these are the shapes the sign-in scheme the published host
    /// currently registers produces. When authentication is switched to Windows, the
    /// challenge becomes a negotiate challenge and the set changes. This list is the one
    /// line to change, which is why it is a list and not a condition spread through the
    /// test below.
    /// </summary>
    private static readonly HttpStatusCode[] challengeStatusCodes =
    [
        HttpStatusCode.Found,
        HttpStatusCode.Redirect,
        HttpStatusCode.Unauthorized,
        HttpStatusCode.Forbidden,
    ];


    /// <summary>
    /// An unauthenticated request for a page of the application is not served with content.
    ///
    /// The assertion has three parts, and the third one is what makes the other two worth
    /// anything. Without it, a server error would satisfy the test: a 500 is not a 200 and
    /// carries no page content either, so "was not served" would be true while nothing
    /// about authorisation had been shown. Requiring the response to be a CHALLENGE, rather
    /// than merely not a success, makes an error a failure instead of a pass.
    ///
    /// WHAT THIS DOES NOT ESTABLISH, and the limit is worth stating because the assertion
    /// is weak. It proves the fallback authorisation policy bites: the page is behind
    /// identity and is not served without it. It does NOT prove the challenge goes
    /// anywhere useful, because almost any other fault also produces something that is not
    /// a 200. Whether a person who is challenged actually arrives at a working sign-in, and
    /// comes back, stays on the manual verification list, which is the only place it can
    /// live.
    ///
    /// The fictitious sign-in values in the environment matter here in a way they do not
    /// elsewhere in this file: this is the one request that reaches the authentication
    /// handler rather than stopping at static files.
    /// </summary>
    [Fact]
    public async Task APageIsNotServedToAnUnauthenticatedRequest()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        Assert.Equal("wal", database.EnableWriteAheadLogging());

        using EnvironmentVariableScope environment = new EnvironmentVariableScope();
        ConfigureValidEnvironment(environment, database);

        using IdeaBoardWebFactory factory = new IdeaBoardWebFactory();

        // The discovery document is supplied instead of being fetched, and this was
        // MEASURED to be necessary rather than assumed. Without it the request answers 500:
        // the handler tries to fetch metadata from the fictitious authority these tests
        // configure, the fetch fails, and the challenge never gets built.
        //
        // That is worth stating plainly, because it is also an operational finding. A
        // published host pointed at a tenant that does not answer does not send anybody to
        // sign in, it returns a server error with nothing to explain it.
        //
        // What is given up by supplying it: this test no longer exercises discovery, which
        // was never verifiable here anyway against an authority that does not exist. What
        // is kept is the part that matters, which is whether a page is served without
        // identity.
        using WebApplicationFactory<Program> configured = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<OpenIdConnectOptions>(
                    OpenIdConnectDefaults.AuthenticationScheme,
                    oidcOptions =>
                    {
                        OpenIdConnectConfiguration configuration = new OpenIdConnectConfiguration
                        {
                            AuthorizationEndpoint = "https://authority.invalid/authorize",
                            TokenEndpoint = "https://authority.invalid/token",
                            Issuer = "https://authority.invalid/",
                        };

                        // BOTH are set, and the second one is why the first alone did
                        // nothing. Measured: with only Configuration set, the request
                        // still answered 500, because the sign-in library installs a
                        // configuration manager built from the authority and the handler
                        // prefers it. The static manager is what actually stops the
                        // network call.
                        oidcOptions.Configuration = configuration;
                        oidcOptions.ConfigurationManager =
                            new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                    });
            });
        });

        // The redirect is not followed, so a challenge shows up as a status code here
        // instead of being chased to whatever sits at the other end.
        WebApplicationFactoryClientOptions options = new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        };

        using HttpClient client = configured.CreateClient(options);

        using HttpResponseMessage response = await client.GetAsync("/", Cancellation);

        string body = await response.Content.ReadAsStringAsync(Cancellation);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);

        AssertCarriesNoPageContent(body);

        Assert.True(
            Array.IndexOf(challengeStatusCodes, response.StatusCode) >= 0,
            $"Expected a challenge, got {(int)response.StatusCode}. Body: {Shorten(body)}");

        // A challenge and not the name gate. The gate redirects to its own page with the
        // same status code, and it has no business acting on somebody who has not been
        // identified yet. Without this line, a pipeline composed with the gate in front of
        // authorisation would satisfy every assertion above.
        if (response.Headers.Location is not null)
        {
            Assert.NotEqual(
                DisplayNameGateMiddleware.RegistrationPath,
                response.Headers.Location.ToString());
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
