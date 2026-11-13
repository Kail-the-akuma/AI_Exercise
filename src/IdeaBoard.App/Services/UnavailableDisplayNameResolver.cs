namespace IdeaBoard.App.Services;

/// <summary>
/// The resolver that never looks anything up and always answers "no name".
///
/// It is registered whenever the directory is not to be consulted: on a host whose sign-in
/// already carries a name in its claims, on a platform without the directory APIs, and
/// whenever the directory lookup is switched off in configuration.
///
/// That last case is the point of this class rather than a side effect of it. With the
/// directory reachable, the manual path is never taken, so it is never exercised, so the
/// first time it ever runs for real is the day the directory breaks: a path nobody has
/// tried, running for the first time at the exact moment the thing it compensates for has
/// failed. Being able to switch the lookup off turns that path into one that can be
/// exercised deliberately, in whichever environment it matters.
/// </summary>
public class UnavailableDisplayNameResolver : IDisplayNameResolver
{
    #region Resolution

    /// <inheritdoc />
    public Task<DisplayNameResolution> ResolveAsync(string accountIdentifier, CancellationToken cancellationToken)
    {
        return Task.FromResult(DisplayNameResolution.NotResolved());
    }

    #endregion
}
