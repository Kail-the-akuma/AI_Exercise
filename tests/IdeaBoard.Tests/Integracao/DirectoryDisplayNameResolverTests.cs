using System.Diagnostics;
using System.Runtime.Versioning;
using IdeaBoard.App.Configuration;
using IdeaBoard.App.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// The real directory resolver, run against the real machine.
///
/// BE CLEAR ABOUT WHAT THIS DOES AND DOES NOT ESTABLISH. The machine these tests run on
/// belongs to no domain, so the only outcome reachable here is failure. That makes the
/// successful branch unverifiable: the filter, the timeout against a live controller, the
/// latency of a lookup that finds something, and what comes back when an account exists
/// with no display name are all untested by anything, anywhere, until the first day in a
/// domain.
///
/// The failure path, on the other hand, is the one that matters most and the one that is
/// fully exercised here. It is what every developer machine takes, every time, and it is
/// what a server takes on the day the directory is unreachable. These tests prove it stays
/// silent, bounded and honest.
/// </summary>
[SupportedOSPlatform("windows")]
public class DirectoryDisplayNameResolverTests
{
    #region Helpers

    private static CancellationToken Cancellation
    {
        get
        {
            return TestContext.Current.CancellationToken;
        }
    }


    private static DirectoryDisplayNameResolver CreateResolver(int timeoutSeconds)
    {
        DirectoryLookupOptions options = new DirectoryLookupOptions
        {
            Enabled = true,
            TimeoutSeconds = timeoutSeconds,
        };

        return new DirectoryDisplayNameResolver(
            options,
            NullLogger<DirectoryDisplayNameResolver>.Instance);
    }

    #endregion


    #region The failure path, exercised for real

    /// <summary>
    /// With no domain to contact, the lookup reports no name and raises nothing at all.
    ///
    /// This is the contract the whole arrangement rests on. It runs on somebody's very
    /// first request, before they have seen a single page, so anything escaping here is an
    /// error page as a person's first impression of the application.
    /// </summary>
    [Fact]
    public async Task AnUnreachableDirectoryReportsNoNameAndRaisesNothing()
    {
        DirectoryDisplayNameResolver resolver = CreateResolver(3);

        DisplayNameResolution resolution = await resolver.ResolveAsync(
            "S-1-5-21-2901470336-2226362600-3229698106-1001",
            Cancellation);

        Assert.False(resolution.IsResolved);
        Assert.Equal(string.Empty, resolution.DisplayName);
    }


    /// <summary>
    /// The failure is bounded. Measured here it is fast, because there is no domain to
    /// resolve and nothing to wait for, so this is a LOWER BOUND and says nothing about a
    /// slow controller. What it does establish is that the outer ceiling is respected and
    /// that nothing waits indefinitely.
    /// </summary>
    [Fact]
    public async Task TheFailureStaysWithinTheCeiling()
    {
        DirectoryDisplayNameResolver resolver = CreateResolver(3);

        Stopwatch clock = Stopwatch.StartNew();

        await resolver.ResolveAsync("S-1-5-21-1-2-3-1001", Cancellation);

        clock.Stop();

        // The ceiling is the configured timeout plus two seconds. A generous margin is
        // allowed on top, because this asserts that there is a bound and not what the bound
        // costs: a test that measured the duration closely would fail on a loaded machine
        // and teach nobody anything.
        Assert.True(
            clock.Elapsed < TimeSpan.FromSeconds(20),
            $"The lookup took {clock.Elapsed}, which suggests nothing bounded it.");
    }

    #endregion


    #region Refusals that cost nothing

    /// <summary>
    /// An account name is refused outright rather than looked up, and without touching the
    /// network.
    ///
    /// ACCOUNT NAMES ARE REUSED. Somebody leaves, somebody else is given the same account
    /// name, and a lookup by account name then returns the new holder's display name and
    /// writes it onto the previous holder's row. Nothing fails. A correct row acquires the
    /// wrong name, and it happens in the refresh that follows a person for months rather
    /// than at provisioning, so the difference only ever appears with time.
    /// </summary>
    [Theory]
    [InlineData("EXEMPLO\\ana")]
    [InlineData("ana@exemplo.test")]
    [InlineData("Ana Dias")]
    [InlineData("11111111-1111-1111-1111-111111111111")]
    public async Task AnythingThatIsNotASecurityIdentifierIsRefusedImmediately(string identifier)
    {
        DirectoryDisplayNameResolver resolver = CreateResolver(30);

        Stopwatch clock = Stopwatch.StartNew();

        DisplayNameResolution resolution = await resolver.ResolveAsync(identifier, Cancellation);

        clock.Stop();

        Assert.False(resolution.IsResolved);

        // Refused before any directory call, which is the point: it must not be looked up
        // even when it would work.
        Assert.True(
            clock.Elapsed < TimeSpan.FromSeconds(2),
            $"A refusal took {clock.Elapsed}, so something was contacted.");
    }


    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyIdentifierReportsNoName(string identifier)
    {
        DirectoryDisplayNameResolver resolver = CreateResolver(3);

        DisplayNameResolution resolution = await resolver.ResolveAsync(identifier, Cancellation);

        Assert.False(resolution.IsResolved);
    }

    #endregion


    #region The resolver that never looks anything up

    /// <summary>
    /// The stand-in used when the directory is switched off answers the same way, which is
    /// what makes switching it off a way to exercise the registration path rather than a
    /// different code path with different behaviour.
    /// </summary>
    [Fact]
    public async Task TheUnavailableResolverAnswersTheSameWay()
    {
        UnavailableDisplayNameResolver resolver = new UnavailableDisplayNameResolver();

        DisplayNameResolution resolution = await resolver.ResolveAsync(
            "S-1-5-21-1-2-3-1001",
            Cancellation);

        Assert.False(resolution.IsResolved);
        Assert.Equal(string.Empty, resolution.DisplayName);
    }

    #endregion
}
