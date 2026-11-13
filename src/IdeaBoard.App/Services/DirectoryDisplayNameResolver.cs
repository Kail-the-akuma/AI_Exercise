using System.DirectoryServices;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using IdeaBoard.App.Configuration;
using Microsoft.Extensions.Logging;

namespace IdeaBoard.App.Services;

/// <summary>
/// Looks a person's name up in the Active Directory the machine belongs to.
///
/// Windows only, declared rather than assumed. The attribute is what lets this class live
/// in a library targeting plain net10.0 alongside everything else: the platform analyser
/// accepts the Windows only calls inside an annotated type, and the composition root does
/// the run time check before ever constructing one. Without the attribute the same code
/// fails the build, because warnings are errors across this solution.
///
/// It searches rather than binding straight to the account, and that is a decision with one
/// reason: a search carries a client timeout and a direct bind carries none. Without a
/// timeout, giving up on the wait does NOT cancel the blocking call, it only releases
/// whoever was waiting, and the thread stays held by a slow controller. On the first
/// request of every new person that consumes pool threads that do not come back, which is
/// an availability problem and not a matter of comfort.
/// </summary>
[SupportedOSPlatform("windows")]
public class DirectoryDisplayNameResolver : IDisplayNameResolver
{
    #region Constants

    /// <summary>The one attribute asked for, and the only one.</summary>
    private const string DisplayNameAttribute = "displayName";

    /// <summary>Where the search starts, resolved by the machine's own domain.</summary>
    private const string RootPath = "LDAP://RootDSE";

    #endregion


    #region Fields

    private readonly DirectoryLookupOptions options;
    private readonly ILogger<DirectoryDisplayNameResolver> logger;

    #endregion


    #region Construction

    /// <summary>Creates the resolver over its timeout and a log to report failures to.</summary>
    public DirectoryDisplayNameResolver(
        DirectoryLookupOptions options,
        ILogger<DirectoryDisplayNameResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.options = options;
        this.logger = logger;
    }

    #endregion


    #region Resolution

    /// <inheritdoc />
    public async Task<DisplayNameResolution> ResolveAsync(
        string accountIdentifier,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accountIdentifier))
        {
            return DisplayNameResolution.NotResolved();
        }

        // Only a security identifier is accepted, and an account name is refused outright
        // rather than used as a convenient second best.
        //
        // ACCOUNT NAMES ARE RECYCLED. Somebody leaves, somebody else joins and is given the
        // same account name. A search by account name then returns the NEW holder's name
        // and writes it onto the OLD holder's row. Nothing fails and nothing is logged: a
        // correct row acquires the wrong name.
        //
        // And it bites where it is hardest to see. Not at first provisioning, where both
        // keys are fresh and either would do, but in the refresh that follows a person for
        // months afterwards. The difference only ever appears with time.
        SecurityIdentifier? securityIdentifier = TryParseSecurityIdentifier(accountIdentifier);

        if (securityIdentifier is null)
        {
            logger.LogWarning(
                "A directory lookup was asked for with something that is not a security " +
                "identifier, so it was refused. Searching by account name is not an acceptable " +
                "substitute: account names are reused, and a reused one silently attaches the " +
                "new holder's name to the previous holder's row.");

            return DisplayNameResolution.NotResolved();
        }

        try
        {
            // The search carries its own client timeout, which is the mechanism that
            // actually bounds the directory call. This outer ceiling is a backstop for
            // everything the client timeout does not cover, such as resolving and binding
            // to the root before any search is sent, and it is deliberately looser so that
            // the inner, cancelling timeout is the one that normally wins.
            Task<DisplayNameResolution> lookup = Task.Run(
                () =>
                {
                    return LookUp(securityIdentifier);
                },
                CancellationToken.None);

            return await lookup.WaitAsync(options.OuterCeiling, cancellationToken);
        }
        catch (TimeoutException)
        {
            logger.LogWarning(
                "The directory did not answer within {CeilingSeconds} seconds, so no name was " +
                "resolved and the person will be asked for one.",
                options.OuterCeiling.TotalSeconds);

            return DisplayNameResolution.NotResolved();
        }
        catch (OperationCanceledException)
        {
            // The request went away. Reporting "no name" rather than letting the
            // cancellation travel keeps the promise this interface makes, and costs
            // nothing: there is no longer anybody waiting for the answer.
            return DisplayNameResolution.NotResolved();
        }
        catch (Exception exception)
        {
            LogFailure(exception);

            return DisplayNameResolution.NotResolved();
        }
    }


    /// <summary>
    /// The synchronous search, run away from the request thread.
    ///
    /// It catches BY BREADTH and never by a specific exception type, which is not laziness.
    /// The two ways of reaching a directory fail with completely different types: binding
    /// through the account management API raises PrincipalServerDownException, and searching
    /// raises a COMException from the interop layer. Code written to catch the type of one
    /// mechanism lets the other through, and what comes through here is somebody's first
    /// request turning into an error page.
    ///
    /// It also returns rather than throwing, so the task never ends up faulted. A faulted
    /// task nobody awaits, which is exactly what the outer timeout leaves behind, raises an
    /// unobserved exception later, at a moment unrelated to the request that caused it.
    /// </summary>
    private DisplayNameResolution LookUp(SecurityIdentifier securityIdentifier)
    {
        try
        {
            using DirectoryEntry root = new DirectoryEntry(RootPath);
            using DirectorySearcher searcher = new DirectorySearcher(root);

            searcher.Filter = BuildSidFilter(securityIdentifier);

            // Exactly one attribute is requested, and the list is cleared first so that no
            // default set can creep in. This is the difference between a design guarantee
            // and a measurement that goes stale with the next version of the library: the
            // account management API exposes dozens of properties, including telephone
            // numbers, employee identifiers, last logon and password policy, and whether
            // those cross the network is not something the caller controls. Here it is.
            searcher.PropertiesToLoad.Clear();
            searcher.PropertiesToLoad.Add(DisplayNameAttribute);

            searcher.SizeLimit = 1;
            searcher.ClientTimeout = options.Timeout;
            searcher.ServerTimeLimit = options.Timeout;

            SearchResult? found = searcher.FindOne();

            if (found is null)
            {
                logger.LogWarning(
                    "The directory was reached but holds no account with the signed-in security " +
                    "identifier. The person will be asked for a name.");

                return DisplayNameResolution.NotResolved();
            }

            return ReadName(found);
        }
        catch (Exception exception)
        {
            LogFailure(exception);

            return DisplayNameResolution.NotResolved();
        }
    }


    /// <summary>
    /// Reads the single attribute that was asked for. An account that carries no display
    /// name is a real state in directories filled in by hand, and it counts as no name
    /// rather than as a blank one.
    /// </summary>
    private DisplayNameResolution ReadName(SearchResult found)
    {
        if (!found.Properties.Contains(DisplayNameAttribute))
        {
            logger.LogWarning(
                "The directory holds the account but it carries no display name. The person will " +
                "be asked for a name.");

            return DisplayNameResolution.NotResolved();
        }

        ResultPropertyValueCollection values = found.Properties[DisplayNameAttribute];

        if (values.Count == 0)
        {
            return DisplayNameResolution.NotResolved();
        }

        string? displayName = Convert.ToString(values[0], CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return DisplayNameResolution.NotResolved();
        }

        return DisplayNameResolution.Resolved(displayName);
    }

    /// <summary>
    /// Parses a security identifier, answering null instead of throwing. There is no
    /// TryParse on the type, and this class is not allowed to let anything escape.
    /// </summary>
    private static SecurityIdentifier? TryParseSecurityIdentifier(string value)
    {
        try
        {
            return new SecurityIdentifier(value);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    #endregion


    #region Filter

    /// <summary>
    /// Builds the search filter for a security identifier, in the escaped binary form.
    ///
    /// The textual form, objectSid=S-1-5-21-and-so-on, is a convenience some directories
    /// accept. The binary form is what the attribute actually holds and what the filter
    /// syntax is defined over, so it needs no convenience to be true. Choosing it also
    /// disposes of the question of what a value taken from a claim could do inside a
    /// filter: everything emitted here is a backslash and two hexadecimal digits, produced
    /// from bytes, so there is no text from the request in the filter at all.
    /// </summary>
    public static string BuildSidFilter(SecurityIdentifier securityIdentifier)
    {
        ArgumentNullException.ThrowIfNull(securityIdentifier);

        byte[] binaryForm = new byte[securityIdentifier.BinaryLength];
        securityIdentifier.GetBinaryForm(binaryForm, 0);

        StringBuilder filter = new StringBuilder("(objectSid=");

        foreach (byte value in binaryForm)
        {
            filter.Append('\\');
            filter.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        filter.Append(')');

        return filter.ToString();
    }

    #endregion


    #region Reporting

    /// <summary>
    /// Records why a lookup failed.
    ///
    /// The exception TYPE is the useful part and it is named first, because the states that
    /// matter read differently: a directory that cannot be contacted at all, and a
    /// directory that answered and refused. Neither the account nor the resolved name is
    /// written, for the same reason the provisioning log records claim types and not claim
    /// values.
    /// </summary>
    private void LogFailure(Exception exception)
    {
        logger.LogWarning(
            exception,
            "The directory lookup for the signed-in person failed with {ExceptionType}, so no name " +
            "was resolved and the person will be asked for one. The application continues to work " +
            "without the directory: it is consulted for the display name only, never for identity.",
            exception.GetType().FullName);
    }

    #endregion
}
