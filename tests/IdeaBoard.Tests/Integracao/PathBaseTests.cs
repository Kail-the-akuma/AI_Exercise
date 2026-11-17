using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// The application served from somewhere other than the root of its server.
///
/// WHY THIS FILE EXISTS. Every other test and every scenario serves the application at the
/// root, where the path base is empty. On the server it is being deployed to, it lives at
/// /IdeaBoard inside a site that already serves another application to real people, and a
/// whole class of defect is invisible until something is served under a prefix: anything
/// that writes a path literally instead of building it looks correct at the root and points
/// outside the application everywhere else.
///
/// Pointing outside the application is not a broken link here. It is a request arriving at
/// the neighbour.
///
/// WHAT THIS STILL CANNOT SEE. This runs over the test server, not over IIS. It proves what
/// the application puts in the header. Whether the IIS module rewrites a Location on its way
/// out is a question about IIS, and it is not answered here or anywhere in this solution.
/// </summary>
public class PathBaseTests
{
    #region Constants

    private const string PathBase = "/IdeaBoard";

    private static readonly CancellationToken Cancellation = TestContext.Current.CancellationToken;

    #endregion


    #region Tests

    /// <summary>
    /// Somebody with no name recorded is sent to the registration page INSIDE the
    /// application.
    ///
    /// The status code is right either way, so it is the header that is asserted. A bare
    /// "/nome" sends the person to the root of the server, which is the neighbour, and it
    /// would happen to everybody on their first visit and to nobody else.
    /// </summary>
    [Fact]
    public async Task SomebodyWithoutANameIsSentToRegistrationInsideTheApplication()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        Assert.Equal("wal", database.EnableWriteAheadLogging());

        using EnvironmentVariableScope environment = new EnvironmentVariableScope();

        environment.Set("ASPNETCORE_ENVIRONMENT", "Development");
        environment.Set("ConnectionStrings__IdeaBoard", database.ConnectionString);
        environment.Set("Localization__TimeZoneId", "Europe/Lisbon");

        using PathBasedWebFactory factory = new PathBasedWebFactory();

        WebApplicationFactoryClientOptions options = new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        };

        using HttpClient client = factory.CreateClient(options);

        using HttpResponseMessage response = await client.GetAsync(PathBase + "/nova", Cancellation);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        string? location = null;

        if (response.Headers.Location is not null)
        {
            location = response.Headers.Location.ToString();
        }

        Assert.Equal(PathBase + "/nome", location);
    }

    #endregion


    #region Hosting

    /// <summary>
    /// The host, served under a path base and with a signed in person who has no row yet.
    ///
    /// The test identity lives here and not in IdeaBoard.Web, which is the invariant that
    /// keeps authentication that was written for a test out of what gets published.
    /// </summary>
    private sealed class PathBasedWebFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.UseEnvironment("Development");

            builder.ConfigureTestServices(services =>
            {
                // A startup filter rather than a change to the application, because the path
                // base is something the server puts there and not something the application
                // declares. This puts it there the same way.
                services.AddSingleton<IStartupFilter>(new PathBaseStartupFilter(PathBase));

                services.AddAuthentication(SignedInScheme.Name)
                    .AddScheme<AuthenticationSchemeOptions, SignedInScheme>(SignedInScheme.Name, null);
            });
        }
    }


    private sealed class PathBaseStartupFilter : IStartupFilter
    {
        private readonly string pathBase;

        public PathBaseStartupFilter(string pathBase)
        {
            this.pathBase = pathBase;
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            ArgumentNullException.ThrowIfNull(next);

            return builder =>
            {
                builder.UsePathBase(pathBase);

                next(builder);
            };
        }
    }


    /// <summary>
    /// Signs every request in as somebody the database has never seen, which is the exact
    /// state the gate exists for.
    /// </summary>
    private sealed class SignedInScheme : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string Name = "TestWindows";

        public SignedInScheme(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            ClaimsIdentity identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, "EXEMPLO\\adias"),
                    new Claim(ClaimTypes.PrimarySid, "S-1-5-21-1-2-3-9999"),
                ],
                Name);

            ClaimsPrincipal principal = new ClaimsPrincipal(identity);

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Name)));
        }
    }

    #endregion
}
