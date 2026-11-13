using System.Security.Claims;
using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IdeaBoard.App.Services;

/// <summary>
/// Upserts the local user row on sign-in, keyed by the subject identifier.
///
/// The choice of key is the whole point. Anything derived from a person's name changes when
/// they change name, and a key that changes gives the first person to marry a second
/// identity, with their ideas and votes split between the two. The subject identifier does
/// not change, which is the only property being asked of it.
///
/// The key is named for what it is and not for where it comes from. This application has
/// already changed identity provider once, and a column called after the provider of the
/// day guarantees a second rename at the next change.
///
/// One invariant governs this whole class: A USER ROW ALWAYS HAS A NAME. When no name can
/// be obtained, no row is written and the person is sent to register one. The alternative,
/// a row carrying a blank name, would push a null check into every page that renders an
/// author or a voter, permanently, to describe a state that lasts seconds.
/// </summary>
public class UserProvisioningService : IUserProvisioningService
{
    #region Fields

    /// <summary>
    /// Every Windows security identifier starts with this, and no name does.
    ///
    /// ENVIRONMENT DEPENDENCY: this assumes identity comes from Windows. If the identity
    /// provider changes, this is the first line to break, exactly as parsing the identifier
    /// as a GUID broke when it changed the last time.
    ///
    /// The prefix is kept anyway, knowing that. An assertion that depended on nothing would
    /// have to be so weak it caught nothing: "contains no spaces" is durable and lets a
    /// user principal name through. The rule is to assert whatever gives the most detection
    /// and to write down what it rests on, because the fault last time was never that the
    /// check was environmental, it was that nothing beside it said which environment.
    /// </summary>
    private const string SecurityIdentifierPrefix = "S-1-";

    /// <summary>
    /// Claim types the subject identifier may arrive under, most specific first. Several
    /// forms are read rather than one, because inbound claim mapping is a host setting and
    /// this library is not the place to depend on how a given host left it.
    /// </summary>
    private static readonly string[] subjectClaimTypes =
    [
        IdeaBoardClaimTypes.PrimarySid,
        IdeaBoardClaimTypes.ObjectId,
        IdeaBoardClaimTypes.MappedObjectId,
    ];

    /// <summary>
    /// Claim types that carry a name fit to SHOW somebody.
    ///
    /// There is exactly one, and the list is short for a reason worth stating. The long
    /// schemas URI that inbound claim mapping used to rewrite the name into is the very
    /// same URI Windows authentication puts the ACCOUNT name in. Reading it here would
    /// quietly make DOMAIN\someone somebody's display name, published to colleagues as the
    /// author of their ideas, which is the exact failure this whole mechanism exists to
    /// prevent. It is read as the account name instead, which is what it is.
    /// </summary>
    private static readonly string[] displayNameClaimTypes =
    [
        IdeaBoardClaimTypes.DisplayName,
    ];

    private readonly IDbContextFactory<IdeaBoardDbContext> contextFactory;
    private readonly IDisplayNameResolver displayNameResolver;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<UserProvisioningService> logger;

    #endregion


    #region Construction

    /// <summary>
    /// Creates the service over its own context factory rather than the request-scoped
    /// context. Provisioning has to commit on its own: the first thing somebody ever does
    /// may be to vote through a direct link, and then the user row and the vote are written
    /// in the same request. A vote that fails must not undo the provisioning, and the
    /// provisioning must not be left hostage to a business operation that failed, because
    /// the next request would try again and reopen the race this class exists to close.
    /// </summary>
    public UserProvisioningService(
        IDbContextFactory<IdeaBoardDbContext> contextFactory,
        IDisplayNameResolver displayNameResolver,
        TimeProvider timeProvider,
        ILogger<UserProvisioningService> logger)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(displayNameResolver);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        this.contextFactory = contextFactory;
        this.displayNameResolver = displayNameResolver;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    #endregion


    #region Provisioning

    /// <inheritdoc />
    public async Task<User?> TryEnsureUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);

        string subjectId = ReadSubjectId(principal);
        string accountName = ReadAccountName(principal);

        await using IdeaBoardDbContext dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);

        // An existing row is answered without ever touching the directory. The lookup is a
        // cost paid once, on a person's very first request, and a check made here rather
        // than inside the resolver is what keeps it from being paid on every request by
        // everybody.
        User? existing = await dbContext.Users
            .FirstOrDefaultAsync(user => user.SubjectId == subjectId, cancellationToken);

        if (existing is not null)
        {
            string? claimedName = ReadFirstClaim(principal, displayNameClaimTypes);

            return await RefreshAsync(
                dbContext,
                existing,
                claimedName,
                DisplayNameSource.Directory,
                accountName,
                cancellationToken);
        }

        string displayName = await ReadDisplayNameAsync(principal, cancellationToken);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            logger.LogInformation(
                "No name could be obtained for a person signing in for the first time, neither " +
                "from a claim nor from the directory, so no user row was created. They will be " +
                "asked to register a name. Token version: {TokenVersion}. Claim types present: " +
                "{ClaimTypes}.",
                DescribeTokenVersion(principal),
                DescribeClaimTypes(principal));

            return null;
        }

        return await CreateAsync(
            dbContext,
            subjectId,
            displayName,
            DisplayNameSource.Directory,
            accountName,
            DescribeClaimTypes(principal),
            DescribeTokenVersion(principal),
            cancellationToken);
    }


    /// <inheritdoc />
    public async Task<User> RegisterAsync(
        ClaimsPrincipal principal,
        string displayName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        string subjectId = ReadSubjectId(principal);
        string accountName = ReadAccountName(principal);

        await using IdeaBoardDbContext dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Somebody who already has a row is not registering, whatever the form said. Two
        // tabs, a reloaded page, or a directory that started answering between the form
        // being shown and being submitted, all land here, and all of them mean the same
        // thing: the row that exists is the answer.
        User? existing = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.SubjectId == subjectId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        return await CreateAsync(
            dbContext,
            subjectId,
            displayName.Trim(),
            DisplayNameSource.SelfRegistered,
            accountName,
            DescribeClaimTypes(principal),
            DescribeTokenVersion(principal),
            cancellationToken);
    }


    /// <inheritdoc />
    public async Task<User> EnsureUserAsync(
        string subjectId,
        string displayName,
        string accountName,
        DisplayNameSource displayNameSource,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        await using IdeaBoardDbContext dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);

        User? existing = await dbContext.Users
            .FirstOrDefaultAsync(user => user.SubjectId == subjectId, cancellationToken);

        if (existing is not null)
        {
            return await RefreshAsync(
                dbContext,
                existing,
                displayName,
                displayNameSource,
                accountName,
                cancellationToken);
        }

        return await CreateAsync(
            dbContext,
            subjectId,
            displayName,
            displayNameSource,
            accountName,
            "not applicable",
            "not applicable",
            cancellationToken);
    }


    /// <inheritdoc />
    public async Task<User?> FindBySubjectIdAsync(string subjectId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        await using IdeaBoardDbContext dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.SubjectId == subjectId, cancellationToken);
    }


    /// <inheritdoc />
    public string ReadSubjectId(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        string? subjectId = ReadFirstClaim(principal, subjectClaimTypes);

        // An absent subject identifier is an error and never a new person. Treating it as
        // a new person would key every sign-in on the same empty string: everybody would
        // collapse into one row, the "second sign-in does not duplicate" check would still
        // pass, and the whole application would quietly become one shared account.
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            logger.LogError(
                "A sign-in arrived with no subject identifier claim. Token version: " +
                "{TokenVersion}. Claim types present: {ClaimTypes}.",
                DescribeTokenVersion(principal),
                DescribeClaimTypes(principal));

            throw new InvalidOperationException(
                "The validated identity carries no subject identifier, so the signed-in person " +
                "cannot be matched to a local user row.");
        }

        // A subject identifier has one of exactly two shapes: a Windows security
        // identifier, or a GUID.
        //
        // The GUID branch is here for the DEVELOPMENT HOST, and saying so is the point of
        // this paragraph. It is tempting to attribute it to the sign-in library the
        // published host still carries, and that attribution would be wrong in a way that
        // costs something: when authentication is switched over and that library goes, a
        // reader would see a justification naming a provider that no longer exists,
        // conclude the branch is dead, and remove it. Removing it breaks IdeaBoard.Local,
        // whose configured people carry GUID object identifiers, and with it the end to end
        // suite built on that host.
        //
        // ENVIRONMENT DEPENDENCY: the GUID branch rests on the development host issuing
        // GUID identifiers. If that host ever changes identity source, this branch stops
        // being needed, and that is the only thing that makes it unnecessary.
        //
        // The check is widened rather than dropped, because of what it protects against.
        //
        // The claims most likely to be picked up by mistake are the ones sitting next to
        // this one: the display name, and the account name in its DOMAIN\someone form.
        // Neither of those is a security identifier and neither parses as a GUID, so both
        // still fail loudly here instead of quietly becoming somebody's identity.
        if (!IsPlausibleSubjectId(subjectId))
        {
            logger.LogError(
                "A sign-in arrived with a subject identifier that is neither a security " +
                "identifier nor a GUID, so it is not an identifier at all. Token version: " +
                "{TokenVersion}. Claim types present: {ClaimTypes}.",
                DescribeTokenVersion(principal),
                DescribeClaimTypes(principal));

            throw new InvalidOperationException(
                "The subject identifier claim is neither a security identifier nor a GUID, so it " +
                "cannot be used to match a local user row.");
        }

        return subjectId;
    }

    #endregion


    #region Persistence

    /// <summary>Inserts a person seen for the first time, with a name already in hand.</summary>
    private async Task<User> CreateAsync(
        IdeaBoardDbContext dbContext,
        string subjectId,
        string displayName,
        DisplayNameSource displayNameSource,
        string accountName,
        string claimTypeDescription,
        string tokenVersionDescription,
        CancellationToken cancellationToken)
    {
        AssertIsNotAnAccountName(displayName);

        User created = new User
        {
            SubjectId = subjectId,
            DisplayName = displayName,
            DisplayNameSource = displayNameSource,
            AccountName = accountName,
            FirstSeenAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        dbContext.Users.Add(created);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (SqliteUniqueConstraint.IsViolation(exception))
        {
            // Two concurrent requests from somebody who has never signed in, two tabs or a
            // page plus a fetch, both read "no such person" and both insert. The unique
            // index on subjectId stops the second one, and losing that race is the expected
            // outcome rather than a fault: the row the other request created is exactly the
            // row this one wanted.
            //
            // This covers the registration form as much as the automatic path. Somebody who
            // submits the form twice, or in two tabs, is two concurrent inserts of the same
            // person, and the second one is a success that already happened.
            dbContext.Entry(created).State = EntityState.Detached;

            return await ReadAfterLosingRaceAsync(dbContext, subjectId, cancellationToken);
        }

        // The claim TYPES are recorded, never their values. A claim type is a schema
        // identifier and carries nothing about the person; the values are their name and
        // their account name, which the event log has no business holding. This line is
        // what makes the manual check of which claim form arrives possible without a
        // debugger and without adding a diagnostic page to the published application.
        logger.LogInformation(
            "Provisioned a local user row for a person signing in for the first time, with a " +
            "name from {DisplayNameSource}. Token version: {TokenVersion}. Claim types present: " +
            "{ClaimTypes}.",
            displayNameSource,
            tokenVersionDescription,
            claimTypeDescription);

        return created;
    }


    /// <summary>
    /// Updates what a fresh sign-in is allowed to update.
    ///
    /// THE TWO COLUMNS FOLLOW DIFFERENT RULES AND ONE CONDITION MUST NOT COVER BOTH.
    /// Writing this quickly, with a single guard wrapping both assignments, is the obvious
    /// mistake, and its symptom is names people typed quietly disappearing.
    ///
    /// The account name always follows the directory. A rename there changes it, the
    /// subject identifier does not change, and nothing else in the row records it. Its
    /// declared purpose is telling two people with the same display name apart while
    /// somebody reads the database by hand, which happens during a manual correction, the
    /// most delicate operation this product has. A stale account name sends that person to
    /// the wrong row. Keeping it current is also free: the value arrives in every request.
    ///
    /// The display name follows the directory only when the stored one came from there.
    /// That keeps legitimate renames showing, which matters in a product built on naming
    /// people, while refusing to overwrite a name the PERSON typed because nothing could be
    /// looked up when they arrived. Overwriting that is silent data loss: no error, no
    /// trace, and they find out by seeing a name they did not choose on their own ideas.
    /// </summary>
    private static async Task<User> RefreshAsync(
        IdeaBoardDbContext dbContext,
        User existing,
        string? displayName,
        DisplayNameSource displayNameSource,
        string accountName,
        CancellationToken cancellationToken)
    {
        bool changed = false;

        if (MayReplaceName(existing, displayName))
        {
            AssertIsNotAnAccountName(displayName!);

            existing.DisplayName = displayName!;
            existing.DisplayNameSource = displayNameSource;
            changed = true;
        }

        if (HasChanged(existing.AccountName, accountName))
        {
            existing.AccountName = accountName;
            changed = true;
        }

        if (!changed)
        {
            return existing;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return existing;
    }


    /// <summary>
    /// Reads back the row the other request committed. It has to be there: the only way to
    /// reach here is for the unique index to have rejected this insert, which means
    /// somebody else inserted it first.
    /// </summary>
    private static async Task<User> ReadAfterLosingRaceAsync(
        IdeaBoardDbContext dbContext,
        string subjectId,
        CancellationToken cancellationToken)
    {
        User? winner = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.SubjectId == subjectId, cancellationToken);

        if (winner is null)
        {
            throw new InvalidOperationException(
                "The unique index rejected the insert of a local user row, but no row with that " +
                "subject identifier can be read back. The index and the data disagree.");
        }

        return winner;
    }


    /// <summary>
    /// Tells whether an incoming value should replace the stored one. A blank incoming
    /// value never overwrites, so a sign-in missing a claim cannot erase something an
    /// earlier one had recorded correctly.
    /// </summary>
    private static bool HasChanged(string? stored, string? incoming)
    {
        if (string.IsNullOrWhiteSpace(incoming))
        {
            return false;
        }

        return !string.Equals(stored, incoming, StringComparison.Ordinal);
    }


    /// <summary>
    /// Tells whether an incoming organisation-supplied name may replace the stored one. A
    /// name the person registered themselves is never replaced.
    /// </summary>
    private static bool MayReplaceName(User existing, string? incoming)
    {
        if (existing.DisplayNameSource == DisplayNameSource.SelfRegistered)
        {
            return false;
        }

        return HasChanged(existing.DisplayName, incoming);
    }

    #endregion


    #region Claims

    /// <summary>
    /// Works out the name to show for somebody who has no row yet.
    ///
    /// The claims come first. When the sign-in already carries a name, which is the case
    /// against Entra ID, nothing else is consulted and no directory is touched. The
    /// directory is asked only when no name claim arrived, which is what happens under
    /// Windows authentication, where the identity is an account name and not a name.
    ///
    /// There is deliberately no fall back to the identifier. An identifier shown as the
    /// author of an idea is just plausible enough to survive review and reach colleagues.
    /// An empty answer is the honest one, and the one the caller knows how to act on, by
    /// writing no row and sending the person to register a name.
    /// </summary>
    private async Task<string> ReadDisplayNameAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        string? claimedName = ReadFirstClaim(principal, displayNameClaimTypes);

        if (!string.IsNullOrWhiteSpace(claimedName))
        {
            return claimedName;
        }

        string accountIdentifier = ReadAccountIdentifier(principal);

        // The resolver never throws, by contract, so there is no try block here. One would
        // only suggest to a later reader that this call can fail in a way worth handling,
        // and invite somebody to weaken the contract on the other side.
        DisplayNameResolution resolution = await displayNameResolver.ResolveAsync(
            accountIdentifier,
            cancellationToken);

        if (resolution.IsResolved)
        {
            return resolution.DisplayName;
        }

        return string.Empty;
    }


    /// <summary>
    /// The value the directory is looked up by.
    ///
    /// The security identifier is preferred: it arrives in the request itself at no cost,
    /// it survives a rename, and it is unambiguous. The account name is the fall back for a
    /// principal that carries no such claim.
    /// </summary>
    private static string ReadAccountIdentifier(ClaimsPrincipal principal)
    {
        string? primarySid = principal.FindFirstValue(IdeaBoardClaimTypes.PrimarySid);

        if (!string.IsNullOrWhiteSpace(primarySid))
        {
            return primarySid;
        }

        return ReadAccountName(principal);
    }


    /// <summary>
    /// Reads the account name, which is what tells two people with the same display name
    /// apart. It is stored and never shown.
    ///
    /// It comes from the identity itself rather than from a directory, which is the whole
    /// reason it replaced the email address here. An email could only ever have been
    /// obtained from the same directory lookup that supplies the name, so on the path where
    /// that lookup fails there would be no email at all, which is precisely the path where
    /// telling two identical names apart matters most.
    /// </summary>
    private static string ReadAccountName(ClaimsPrincipal principal)
    {
        string? claimed = principal.FindFirstValue(IdeaBoardClaimTypes.WindowsAccountName);

        if (!string.IsNullOrWhiteSpace(claimed))
        {
            // The mirror of the display name rule, asked only of the claim that is defined
            // to carry this shape. A value from here without exactly one separator means
            // something other than an account name arrived under the account claim.
            if (!DisplayNameRules.HasAccountNameShape(claimed))
            {
                throw new InvalidOperationException(
                    "The Windows account claim carries a value that is not of the form " +
                    "DOMAIN\\someone, so it is not an account name. Storing it would put the " +
                    "wrong value in the column that exists to tell two people with the same " +
                    "display name apart.");
            }

            return claimed;
        }

        if (principal.Identity is not null && !string.IsNullOrWhiteSpace(principal.Identity.Name))
        {
            return principal.Identity.Name;
        }

        // Reached only by a host that authenticated somebody without naming the account.
        // Both real hosts always supply one, so this is a composition mistake rather than
        // a state a person can get into, and it fails here rather than becoming an empty
        // string in a column whose entire purpose is telling two people apart.
        throw new InvalidOperationException(
            "The authenticated identity carries no account name, so there is nothing to store " +
            "for telling two people with the same display name apart. The host authenticated " +
            "this request without naming the account.");
    }


    /// <summary>
    /// Refuses a display name that has the shape of an account.
    ///
    /// This is the invariant of the column and not a rule about where the name came from,
    /// so it is asserted on every write and in one function. It is the only check that
    /// catches the single failure in this change producing no error at all: the Windows
    /// name claim carries DOMAIN\\someone, lives at the very same claim type a display name
    /// used to arrive under, and reading it would compile, run, raise nothing, and publish
    /// an account identifier to colleagues as the author of their ideas.
    ///
    /// Here it throws, because reaching it means the application fed itself the wrong
    /// value. On the registration page the same rule is a validation message instead,
    /// because there the value came from a person and a person can be told.
    /// </summary>
    private static void AssertIsNotAnAccountName(string displayName)
    {
        if (!DisplayNameRules.LooksLikeAccountName(displayName))
        {
            return;
        }

        throw new InvalidOperationException(
            "A display name containing a backslash was about to be stored, which means an " +
            "account name reached the field shown to colleagues as the author of an idea. The " +
            "likely cause is reading the Windows name claim, which carries DOMAIN\\someone, as " +
            "though it carried a name fit to show.");
    }


    /// <summary>Tells whether a value has the shape of an identifier rather than of a name.</summary>
    private static bool IsPlausibleSubjectId(string value)
    {
        if (value.StartsWith(SecurityIdentifierPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Guid.TryParse(value, out _);
    }


    /// <summary>
    /// Describes which token version produced this principal.
    ///
    /// The token declares its own version, so it is read rather than inferred from which
    /// other claims happen to be missing. The issuer is a second, independent indicator,
    /// and only its SHAPE is recorded: the value carries the tenant identifier and has no
    /// business in a log.
    /// </summary>
    private static string DescribeTokenVersion(ClaimsPrincipal principal)
    {
        string version = "absent";
        string? declaredVersion = principal.FindFirstValue(IdeaBoardClaimTypes.TokenVersion);

        if (!string.IsNullOrWhiteSpace(declaredVersion))
        {
            version = declaredVersion;
        }

        return $"{version}, {DescribeIssuerShape(principal)}";
    }


    /// <summary>Says whether the issuer looks like a version 2.0 one, without quoting it.</summary>
    private static string DescribeIssuerShape(ClaimsPrincipal principal)
    {
        string? issuer = principal.FindFirstValue(IdeaBoardClaimTypes.Issuer);

        if (string.IsNullOrWhiteSpace(issuer))
        {
            return "no issuer claim";
        }

        if (issuer.TrimEnd('/').EndsWith("/v2.0", StringComparison.OrdinalIgnoreCase))
        {
            return "issuer ends in /v2.0";
        }

        return "issuer does not end in /v2.0";
    }


    /// <summary>
    /// Lists the claim types on the principal, sorted and deduplicated. Types only: the
    /// values are personal data and never reach the log.
    /// </summary>
    private static string DescribeClaimTypes(ClaimsPrincipal principal)
    {
        SortedSet<string> claimTypes = new SortedSet<string>(StringComparer.Ordinal);

        foreach (Claim claim in principal.Claims)
        {
            claimTypes.Add(claim.Type);
        }

        return string.Join(", ", claimTypes);
    }


    /// <summary>Returns the value of the first claim type present, or null.</summary>
    private static string? ReadFirstClaim(ClaimsPrincipal principal, string[] claimTypes)
    {
        foreach (string claimType in claimTypes)
        {
            string? value = principal.FindFirstValue(claimType);

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    #endregion
}
