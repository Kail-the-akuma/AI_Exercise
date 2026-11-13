using System.Net;
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
}
