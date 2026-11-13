using System.Security.Claims;
using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IdeaBoard.App.Services;

/// <summary>
/// Upserts the local user row on sign-in, keyed by the Entra object identifier.
///
/// The choice of key is the whole point. An email changes when someone changes name and the
/// object identifier does not, so matching on email would give the first person to change
/// name a second identity, with their ideas and votes split between the two.
/// </summary>
public class UserProvisioningService : IUserProvisioningService
{
    #region Fields

    /// <summary>
    /// Claim types read, canonical form first. Both forms are read rather than one,
    /// because inbound claim mapping is a host setting and this library is not the place
    /// to depend on how a given host left it.
    /// </summary>
    private static readonly string[] objectIdClaimTypes =
    [
        IdeaBoardClaimTypes.ObjectId,
        IdeaBoardClaimTypes.MappedObjectId,
    ];

    private static readonly string[] displayNameClaimTypes =
    [
        IdeaBoardClaimTypes.DisplayName,
        IdeaBoardClaimTypes.MappedDisplayName,
    ];

    /// <summary>
    /// Email claim types, short forms first.
    ///
    /// The short forms are not a nicety here, they are the only fallback that can actually
    /// fire. The published host sets MapInboundClaims to false on purpose, which is exactly
    /// what stops the long forms below from ever arriving, so a list made only of long
    /// fallbacks would be dead rather than merely weak.
    /// </summary>
    private static readonly string[] emailClaimTypes =
    [
        IdeaBoardClaimTypes.Email,
        IdeaBoardClaimTypes.Upn,
        IdeaBoardClaimTypes.EmailAddress,
        IdeaBoardClaimTypes.MappedEmail,
        IdeaBoardClaimTypes.MappedUpn,
    ];

    private readonly IDbContextFactory<IdeaBoardDbContext> contextFactory;
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
        TimeProvider timeProvider,
        ILogger<UserProvisioningService> logger)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        this.contextFactory = contextFactory;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    #endregion


    #region Provisioning

    /// <inheritdoc />
    public async Task<User> EnsureUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);

        string? entraObjectId = ReadFirstClaim(principal, objectIdClaimTypes);

        // An absent object identifier is an error and never a new person. Treating it as a
        // new person would key every sign-in on the same empty string: everybody would
        // collapse into one row, the "second sign-in does not duplicate" check would still
        // pass, and the whole application would quietly become one shared account.
        if (string.IsNullOrWhiteSpace(entraObjectId))
        {
            logger.LogError(
                "A sign-in arrived with no object identifier claim. Token version: {TokenVersion}. " +
                "Claim types present: {ClaimTypes}.",
                DescribeTokenVersion(principal),
                DescribeClaimTypes(principal));

            throw new InvalidOperationException(
                "The validated token carries no object identifier, so the signed-in person cannot " +
                "be matched to a local user row.");
        }

        // An Entra ID object identifier is a GUID. Anything that is not one is not the
        // object identifier, whichever claim type it arrived under, and the two claims
        // most likely to be picked up by mistake are the ones sitting next to it: the name
        // and the preferred username. Neither parses as a GUID, so both fail loudly here
        // instead of quietly becoming somebody's identity.
        if (!Guid.TryParse(entraObjectId, out _))
        {
            logger.LogError(
                "A sign-in arrived with an object identifier claim that is not a GUID, so it is " +
                "not the object identifier. Token version: {TokenVersion}. Claim types present: " +
                "{ClaimTypes}.",
                DescribeTokenVersion(principal),
                DescribeClaimTypes(principal));

            throw new InvalidOperationException(
                "The object identifier claim does not parse as a GUID, so it is not an Entra ID " +
                "object identifier and cannot be used to match a local user row.");
        }

        string displayName = ReadDisplayName(principal, entraObjectId);
        string? email = ReadEmail(principal);

        return await EnsureUserAsync(
            entraObjectId,
            displayName,
            email,
            DescribeClaimTypes(principal),
            DescribeTokenVersion(principal),
            cancellationToken);
    }


    /// <inheritdoc />
    public async Task<User> EnsureUserAsync(
        string entraObjectId,
        string displayName,
        string? email,
        CancellationToken cancellationToken)
    {
        return await EnsureUserAsync(
            entraObjectId,
            displayName,
            email,
            "not applicable",
            "not applicable",
            cancellationToken);
    }


    /// <summary>
    /// Shared upsert. The claim type description is carried through only so that the log
    /// line written on a first sign-in can record which claim types arrived.
    /// </summary>
    private async Task<User> EnsureUserAsync(
        string entraObjectId,
        string displayName,
        string? email,
        string claimTypeDescription,
        string tokenVersionDescription,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entraObjectId);

        await using IdeaBoardDbContext dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);

        User? existing = await dbContext.Users
            .FirstOrDefaultAsync(user => user.EntraObjectId == entraObjectId, cancellationToken);

        if (existing is null)
        {
            return await CreateAsync(
                dbContext,
                entraObjectId,
                displayName,
                email,
                claimTypeDescription,
                tokenVersionDescription,
                cancellationToken);
        }

        return await RefreshAsync(dbContext, existing, displayName, email, cancellationToken);
    }


    /// <inheritdoc />
    public async Task<User?> FindByEntraObjectIdAsync(string entraObjectId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entraObjectId);

        await using IdeaBoardDbContext dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.EntraObjectId == entraObjectId, cancellationToken);
    }

    #endregion


    #region Persistence

    /// <summary>Inserts a person seen for the first time.</summary>
    private async Task<User> CreateAsync(
        IdeaBoardDbContext dbContext,
        string entraObjectId,
        string displayName,
        string? email,
        string claimTypeDescription,
        string tokenVersionDescription,
        CancellationToken cancellationToken)
    {
        User created = new User
        {
            EntraObjectId = entraObjectId,
            DisplayName = displayName,
            Email = email,
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
            // index on entraObjectId stops the second one, and losing that race is the
            // expected outcome rather than a fault: the row the other request created is
            // exactly the row this one wanted.
            dbContext.Entry(created).State = EntityState.Detached;

            return await ReadAfterLosingRaceAsync(dbContext, entraObjectId, cancellationToken);
        }

        // The claim TYPES are recorded, never their values. A claim type is a schema
        // identifier and carries nothing about the person; the values are their name and
        // their email, which the event log has no business holding. This line is what
        // makes the manual check of which claim form the tenant issues possible without a
        // debugger and without adding a diagnostic page to the published application.
        string emailOutcome = "email claim present";

        if (string.IsNullOrWhiteSpace(email))
        {
            emailOutcome =
                "NO email claim arrived, so the email was stored as null. The usual cause is an " +
                "authority configured for version 1.0 tokens, which carry no preferred_username.";
        }

        logger.LogInformation(
            "Provisioned a local user row for a person signing in for the first time. " +
            "Token version: {TokenVersion}. {EmailOutcome} Claim types present: {ClaimTypes}.",
            tokenVersionDescription,
            emailOutcome,
            claimTypeDescription);

        return created;
    }


    /// <summary>
    /// Updates the stored name and email when the token disagrees with them, so that
    /// someone who changes name does not keep showing up under the old one.
    /// </summary>
    private static async Task<User> RefreshAsync(
        IdeaBoardDbContext dbContext,
        User existing,
        string displayName,
        string? email,
        CancellationToken cancellationToken)
    {
        bool changed = false;

        if (HasChanged(existing.DisplayName, displayName))
        {
            existing.DisplayName = displayName;
            changed = true;
        }

        if (HasChanged(existing.Email, email))
        {
            existing.Email = email;
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
        string entraObjectId,
        CancellationToken cancellationToken)
    {
        User? winner = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.EntraObjectId == entraObjectId, cancellationToken);

        if (winner is null)
        {
            throw new InvalidOperationException(
                "The unique index rejected the insert of a local user row, but no row with that " +
                "object identifier can be read back. The index and the data disagree.");
        }

        return winner;
    }


    /// <summary>
    /// Tells whether an incoming claim value should replace the stored one. A blank
    /// incoming value never overwrites, so a token missing a claim cannot erase a name
    /// that an earlier sign-in had recorded correctly.
    /// </summary>
    private static bool HasChanged(string? stored, string? incoming)
    {
        if (string.IsNullOrWhiteSpace(incoming))
        {
            return false;
        }

        return !string.Equals(stored, incoming, StringComparison.Ordinal);
    }

    #endregion


    #region Claims

    /// <summary>
    /// Reads the name to show. Falls back to the object identifier rather than to an empty
    /// string, because a blank author name would be worse than an ugly one.
    /// </summary>
    private static string ReadDisplayName(ClaimsPrincipal principal, string entraObjectId)
    {
        string? displayName = ReadFirstClaim(principal, displayNameClaimTypes);

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        return entraObjectId;
    }


    /// <summary>
    /// Reads the email from the first claim that carries one. An absent email is stored as
    /// empty rather than refused, because the email is never shown and refusing sign-in
    /// over it would lock someone out of the whole application.
    /// </summary>
    private static string? ReadEmail(ClaimsPrincipal principal)
    {
        return ReadFirstClaim(principal, emailClaimTypes);
    }


    /// <summary>
    /// Describes which token version produced this principal.
    ///
    /// The token declares its own version, so it is read rather than inferred from which
    /// other claims happen to be missing. The issuer is a second, independent indicator,
    /// and only its SHAPE is recorded: the value carries the tenant identifier and has no
    /// business in a log.
    ///
    /// The version is the variable that governs which claim types arrive, and that is the
    /// one thing in this application with no mechanical detection at all. This line is the
    /// whole of it.
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
