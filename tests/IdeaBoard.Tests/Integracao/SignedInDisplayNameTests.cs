using System.Security.Claims;
using IdeaBoard.App.Domain;
using IdeaBoard.App.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// The name the layout shows, and the three states it has to survive.
///
/// This exists because the first version of it did not survive them. The layout is drawn on
/// every page, and two of those pages are reachable before anybody has a user row: the page
/// that asks somebody to register a name, and the error page. The error page is where a
/// refused request lands, so asking an unauthenticated request who it is turned a refusal
/// that was working correctly into a 500, and an end to end scenario caught it.
/// </summary>
public class SignedInDisplayNameTests
{
    #region Doubles

    /// <summary>A current user that answers with a fixed row, or with nothing.</summary>
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


    /// <summary>A current user that fails if it is ever asked, which is the point.</summary>
    private sealed class ForbiddenCurrentUser : ICurrentUser
    {
        public Task<User?> TryGetAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "The current user was asked about an unauthenticated request.");
        }

        public Task<User> GetAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "The current user was asked about an unauthenticated request.");
        }
    }


    private static IHttpContextAccessor AccessorFor(bool authenticated)
    {
        DefaultHttpContext httpContext = new DefaultHttpContext();

        if (authenticated)
        {
            ClaimsIdentity identity = new ClaimsIdentity("TestAuthentication");
            httpContext.User = new ClaimsPrincipal(identity);
        }

        return new HttpContextAccessor { HttpContext = httpContext };
    }


    private static CancellationToken Cancellation
    {
        get
        {
            return TestContext.Current.CancellationToken;
        }
    }

    #endregion


    #region States

    /// <summary>
    /// An unauthenticated request is answered with nothing, and the current user is never
    /// consulted at all.
    ///
    /// The double throws if it is asked, so this fails rather than passing by accident if
    /// the guard is removed and the underlying call happens to succeed one day.
    /// </summary>
    [Fact]
    public async Task AnUnauthenticatedRequestIsAnsweredWithNothingAndAsksNobody()
    {
        SignedInDisplayName accessor = new SignedInDisplayName(
            new ForbiddenCurrentUser(),
            AccessorFor(false));

        Assert.Null(await accessor.TryGetAsync(Cancellation));
    }


    /// <summary>
    /// Somebody authenticated who has no row yet is answered with nothing. That is the
    /// person on their way to register a name, and the layout is drawn for them too.
    /// </summary>
    [Fact]
    public async Task SomebodyWithoutARowYetIsAnsweredWithNothing()
    {
        SignedInDisplayName accessor = new SignedInDisplayName(
            new StubCurrentUser(null),
            AccessorFor(true));

        Assert.Null(await accessor.TryGetAsync(Cancellation));
    }


    /// <summary>The ordinary case: the name, and nothing else about the person.</summary>
    [Fact]
    public async Task SomebodyWithARowIsAnsweredWithTheirName()
    {
        User person = TestData.NewUser("João Gonçalves");

        SignedInDisplayName accessor = new SignedInDisplayName(
            new StubCurrentUser(person),
            AccessorFor(true));

        Assert.Equal("João Gonçalves", await accessor.TryGetAsync(Cancellation));
    }

    #endregion
}
