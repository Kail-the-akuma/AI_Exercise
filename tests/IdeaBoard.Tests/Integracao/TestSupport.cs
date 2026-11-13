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

    #endregion


    #region Rows

    /// <summary>Builds a person with a fresh object identifier.</summary>
    public static User NewUser(string displayName)
    {
        return NewUser(displayName, Guid.NewGuid().ToString());
    }


    /// <summary>Builds a person with a given object identifier.</summary>
    public static User NewUser(string displayName, string entraObjectId)
    {
        return new User
        {
            EntraObjectId = entraObjectId,
            DisplayName = displayName,
            Email = string.Concat(displayName.Replace(" ", string.Empty, StringComparison.Ordinal), "@example.test"),
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
    /// Builds a principal carrying the claim types the application standardises on. A null
    /// object identifier leaves that claim out altogether, which is the case the
    /// provisioning service has to refuse.
    /// </summary>
    public static ClaimsPrincipal NewPrincipal(string? objectId, string displayName, string email)
    {
        List<Claim> claims = new List<Claim>();

        if (objectId is not null)
        {
            claims.Add(new Claim(IdeaBoardClaimTypes.ObjectId, objectId));
        }

        claims.Add(new Claim(IdeaBoardClaimTypes.DisplayName, displayName));
        claims.Add(new Claim(IdeaBoardClaimTypes.Email, email));

        ClaimsIdentity identity = new ClaimsIdentity(claims, "TestAuthentication");

        return new ClaimsPrincipal(identity);
    }

    #endregion
}
