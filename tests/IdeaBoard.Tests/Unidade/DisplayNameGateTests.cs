using System.Security.Claims;
using IdeaBoard.App.Domain;
using IdeaBoard.App.Pipeline;
using IdeaBoard.App.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace IdeaBoard.Tests.Unidade;

/// <summary>
/// The gate that keeps somebody whose name is not known out of everything except the page
/// that asks for it.
///
/// What it protects is not access, it is what colleagues see. Submitting an idea or casting
/// a vote publishes a name, and nothing in this product edits or removes, so a wrong name
/// published once is published for good.
/// </summary>
public class DisplayNameGateTests
{
    #region Helpers

    private sealed class StubCurrentUser : ICurrentUser
    {
        private readonly User? person;

        public StubCurrentUser(User? person)
        {
            this.person = person;
        }

        public Task<User?> TryGetAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(person);
        }

        public Task<User> GetAsync(CancellationToken cancellationToken)
        {
            if (person is null)
            {
                throw new InvalidOperationException("No row.");
            }

            return Task.FromResult(person);
        }
    }


    private static User SomebodyWithAName()
    {
        return new User
        {
            Id = 1,
            SubjectId = "S-1-5-21-1-2-3-1001",
            DisplayName = "Ana Dias",
            DisplayNameSource = DisplayNameSource.Directory,
            AccountName = "EXEMPLO\\adias",
        };
    }


    private static DefaultHttpContext BuildContext(string path, bool authenticated)
    {
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Path = path;

        if (authenticated)
        {
            ClaimsIdentity identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "EXEMPLO\\adias")],
                "TestAuthentication");

            context.User = new ClaimsPrincipal(identity);
        }
        else
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
        }

        return context;
    }


    private static async Task<DefaultHttpContext> RunAsync(
        string path,
        bool authenticated,
        User? person,
        Action onContinue)
    {
        DefaultHttpContext context = BuildContext(path, authenticated);

        DisplayNameGateMiddleware gate = new DisplayNameGateMiddleware(nextContext =>
        {
            onContinue();
            return Task.CompletedTask;
        });

        await gate.InvokeAsync(context, new StubCurrentUser(person));

        return context;
    }

    #endregion


    #region Letting through

    [Fact]
    public async Task SomebodyWithANameIsLetThrough()
    {
        bool continued = false;

        DefaultHttpContext context = await RunAsync(
            "/nova",
            true,
            SomebodyWithAName(),
            () => { continued = true; });

        Assert.True(continued);
        Assert.NotEqual(StatusCodes.Status302Found, context.Response.StatusCode);
    }


    /// <summary>
    /// An unauthenticated request is not this middleware's business. The authorisation
    /// fallback policy deals with it, and the host that is never published sends people to
    /// its own picker.
    /// </summary>
    [Fact]
    public async Task AnUnauthenticatedRequestIsNotTouched()
    {
        bool continued = false;

        await RunAsync("/nova", false, null, () => { continued = true; });

        Assert.True(continued);
    }


    /// <summary>
    /// The registration page itself is reachable, otherwise the gate would send somebody
    /// round in a circle.
    /// </summary>
    [Theory]
    [InlineData("/nome")]
    [InlineData("/NOME")]
    public async Task TheRegistrationPageIsAlwaysReachable(string path)
    {
        bool continued = false;

        await RunAsync(path, true, null, () => { continued = true; });

        Assert.True(continued);
    }


    /// <summary>
    /// The error and status pages are let through. The status code middleware re-executes
    /// the pipeline against them, and a diagnostic page that redirects instead of saying
    /// what went wrong is worse than none.
    /// </summary>
    [Theory]
    [InlineData("/erro")]
    [InlineData("/estado/404")]
    [InlineData("/Estado/500")]
    public async Task TheDiagnosticPagesAreLetThrough(string path)
    {
        bool continued = false;

        await RunAsync(path, true, null, () => { continued = true; });

        Assert.True(continued);
    }

    #endregion


    #region Stopping

    /// <summary>
    /// The two pages that publish a name to colleagues are exactly the ones that must not
    /// be reachable without one.
    /// </summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/nova")]
    [InlineData("/recentes")]
    [InlineData("/ideias/7")]
    public async Task SomebodyWithoutANameIsSentToRegisterOne(string path)
    {
        bool continued = false;

        DefaultHttpContext context = await RunAsync(
            path,
            true,
            null,
            () => { continued = true; });

        Assert.False(continued, $"'{path}' was reached without a name.");
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal(DisplayNameGateMiddleware.RegistrationPath, context.Response.Headers.Location);
    }


    /// <summary>
    /// A POST is stopped as readily as a GET. A redirect rather than a refusal, because the
    /// person has done nothing wrong and there is something they can do about it.
    /// </summary>
    [Fact]
    public async Task APostIsStoppedToo()
    {
        bool continued = false;

        DefaultHttpContext context = BuildContext("/ideias/7", true);
        context.Request.Method = HttpMethods.Post;

        DisplayNameGateMiddleware gate = new DisplayNameGateMiddleware(nextContext =>
        {
            continued = true;
            return Task.CompletedTask;
        });

        await gate.InvokeAsync(context, new StubCurrentUser(null));

        Assert.False(continued);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
    }

    #endregion
}
