using System.Globalization;
using System.Security.Claims;
using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using IdeaBoard.App.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// Hands out contexts over one temporary database, for the services that open a context of
/// their own instead of sharing the one the request uses.
/// </summary>
public sealed class TemporaryDbContextFactory : IDbContextFactory<IdeaBoardDbContext>
{
    #region Fields

    private readonly TemporarySqliteDatabase database;
    private readonly IInterceptor? interceptor;

    #endregion


    #region Construction

    /// <summary>Creates the factory over a database, with no interceptor.</summary>
    public TemporaryDbContextFactory(TemporarySqliteDatabase database)
        : this(database, null)
    {
    }


    /// <summary>Creates the factory over a database, with every context intercepted.</summary>
    public TemporaryDbContextFactory(TemporarySqliteDatabase database, IInterceptor? interceptor)
    {
        ArgumentNullException.ThrowIfNull(database);

        this.database = database;
        this.interceptor = interceptor;
    }

    #endregion


    #region Creation

    /// <inheritdoc />
    public IdeaBoardDbContext CreateDbContext()
    {
        return database.CreateContext(interceptor);
    }

    #endregion
}


/// <summary>
/// Rows and principals the tests start from, built in one place so that a test reads as
/// what it checks rather than as how its fixtures are assembled.
/// </summary>
public static class TestData
{
    #region Constants

    /// <summary>A fixed instant every fixture is anchored to.</summary>
    public static readonly DateTime Anchor = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// The domain part of every account name a fixture builds.
    ///
    /// Test data here has the SHAPE PRODUCTION WILL HAVE, and that is not decoration. The
    /// identifier used to be a GUID because the identity provider issued GUIDs, and every
    /// negative test went on passing the day it became a security identifier, because no
    /// test ever provisioned anybody with a value of the real new shape. An invented string
    /// that happens to satisfy a check verifies the check and nothing else.
    /// </summary>
    public const string AccountDomain = "EXEMPLO";

    /// <summary>The fixed part of a security identifier, as a real domain issues it.</summary>
    private const string SecurityIdentifierPrefix = "S-1-5-21-2901470336-2226362600-3229698106-";

    private static int nextRelativeIdentifier = 1000;

    #endregion


    #region Identity values

    /// <summary>Builds a security identifier of the shape Windows actually issues.</summary>
    public static string NewSubjectId()
    {
        int relativeIdentifier = Interlocked.Increment(ref nextRelativeIdentifier);

        return string.Concat(
            SecurityIdentifierPrefix,
            relativeIdentifier.ToString(CultureInfo.InvariantCulture));
    }


    /// <summary>Builds an account name of the DOMAIN\someone shape.</summary>
    public static string NewAccountName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        string account = displayName
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return string.Concat(AccountDomain, "\\", account);
    }

    #endregion


    #region Rows

    /// <summary>Builds a person with a fresh subject identifier.</summary>
    public static User NewUser(string displayName)
    {
        return NewUser(displayName, NewSubjectId());
    }


    /// <summary>Builds a person with a given subject identifier.</summary>
    public static User NewUser(string displayName, string subjectId)
    {
        return new User
        {
            SubjectId = subjectId,
            DisplayName = displayName,
            DisplayNameSource = DisplayNameSource.Directory,
            AccountName = NewAccountName(displayName),
            FirstSeenAt = Anchor,
        };
    }


    /// <summary>Builds an idea by a given author, submitted at a given instant.</summary>
    public static Idea NewIdea(User author, string title, DateTime createdAt)
    {
        ArgumentNullException.ThrowIfNull(author);

        return new Idea
        {
            Title = title,
            Description = string.Concat("Description of ", title),
            AuthorUserId = author.Id,
            CreatedAt = createdAt,
        };
    }


    /// <summary>Builds a vote cast at a given instant.</summary>
    public static Vote NewVote(Idea idea, User voter, DateTime createdAt)
    {
        ArgumentNullException.ThrowIfNull(idea);
        ArgumentNullException.ThrowIfNull(voter);

        return new Vote
        {
            IdeaId = idea.Id,
            UserId = voter.Id,
            CreatedAt = createdAt,
        };
    }

    #endregion


    #region Principals

    /// <summary>
    /// Builds a principal of the shape Windows authentication produces: a security
    /// identifier and an account name, and NO name fit to show. That absence is the whole
    /// point, because it is what sends the application to the directory.
    ///
    /// A null subject identifier leaves that claim out altogether, which is the case the
    /// provisioning service has to refuse.
    /// </summary>
    public static ClaimsPrincipal NewWindowsPrincipal(string? subjectId, string accountName)
    {
        List<Claim> claims = new List<Claim>();

        if (subjectId is not null)
        {
            claims.Add(new Claim(IdeaBoardClaimTypes.PrimarySid, subjectId));
        }

        claims.Add(new Claim(IdeaBoardClaimTypes.WindowsAccountName, accountName));

        ClaimsIdentity identity = new ClaimsIdentity(
            claims,
            "TestAuthentication",
            IdeaBoardClaimTypes.WindowsAccountName,
            ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }


    /// <summary>
    /// Builds a principal of the shape the hosts that are never published produce: an
    /// object identifier and a name already fit to show, plus an account name.
    /// </summary>
    public static ClaimsPrincipal NewNamedPrincipal(string subjectId, string displayName, string accountName)
    {
        List<Claim> claims =
        [
            new Claim(IdeaBoardClaimTypes.ObjectId, subjectId),
            new Claim(IdeaBoardClaimTypes.DisplayName, displayName),
            new Claim(IdeaBoardClaimTypes.WindowsAccountName, accountName),
        ];

        ClaimsIdentity identity = new ClaimsIdentity(
            claims,
            "TestAuthentication",
            IdeaBoardClaimTypes.DisplayName,
            ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }

    #endregion
}
