using System.Security.Claims;
using IdeaBoard.App.Data;
using IdeaBoard.App.Domain;
using IdeaBoard.App.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// The upsert that keeps the local user table in step with who signs in.
///
/// The trap this file is written around: a test that only checks "the second sign-in does
/// not create a second row" passes just as happily against a service that keys everybody on
/// the same constant, because then every sign-in collapses into one row. So every check
/// here either distinguishes two different people, or follows one person's identifier
/// across sign-ins. A stable key and a constant key must give different answers.
/// </summary>
public class UserProvisioningTests
{
    #region Helpers

    private static CancellationToken Cancellation
    {
        get
        {
            return TestContext.Current.CancellationToken;
        }
    }


    private static UserProvisioningService CreateService(TemporarySqliteDatabase database)
    {
        return CreateService(database, null);
    }


    private static UserProvisioningService CreateService(
        TemporarySqliteDatabase database,
        RacingInsertInterceptor? racer)
    {
        TemporaryDbContextFactory factory = new TemporaryDbContextFactory(database, racer);

        return new UserProvisioningService(
            factory,
            TimeProvider.System,
            NullLogger<UserProvisioningService>.Instance);
    }


    private static long CountUsers(TemporarySqliteDatabase database)
    {
        return database.ExecuteScalarCount("SELECT COUNT(*) FROM \"User\";");
    }

    #endregion


    #region The upsert

    /// <summary>
    /// The row is created once and refreshed afterwards, even when the name and the email
    /// both change between sign-ins.
    /// </summary>
    [Fact]
    public async Task LaterSignInsRefreshTheRowInsteadOfAddingOne()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        UserProvisioningService service = CreateService(database);

        string objectId = Guid.NewGuid().ToString();

        User first = await service.EnsureUserAsync(
            TestData.NewPrincipal(objectId, "Ana Dias", "ana.dias@example.test"),
            Cancellation);

        User second = await service.EnsureUserAsync(
            TestData.NewPrincipal(objectId, "Ana Dias Correia", "ana.correia@example.test"),
            Cancellation);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Ana Dias Correia", second.DisplayName);
        Assert.Equal("ana.correia@example.test", second.Email);
        Assert.Equal(1, CountUsers(database));

        User? reloaded = await service.FindByEntraObjectIdAsync(objectId, Cancellation);

        Assert.NotNull(reloaded);
        Assert.Equal(first.Id, reloaded.Id);
        Assert.Equal("Ana Dias Correia", reloaded.DisplayName);
    }


    /// <summary>
    /// Two people who happen to share an email are two people, and one person whose email
    /// changed is still one person. This is what says the key is the object identifier.
    /// </summary>
    [Fact]
    public async Task TheKeyIsTheObjectIdentifierAndNeverTheEmail()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        UserProvisioningService service = CreateService(database);

        string sharedEmail = "contacto@example.test";
        string firstObjectId = Guid.NewGuid().ToString();
        string secondObjectId = Guid.NewGuid().ToString();

        User first = await service.EnsureUserAsync(
            TestData.NewPrincipal(firstObjectId, "Ana Dias", sharedEmail),
            Cancellation);

        User second = await service.EnsureUserAsync(
            TestData.NewPrincipal(secondObjectId, "Bruno Lima", sharedEmail),
            Cancellation);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, CountUsers(database));

        User again = await service.EnsureUserAsync(
            TestData.NewPrincipal(firstObjectId, "Ana Dias", "outro.endereco@example.test"),
            Cancellation);

        Assert.Equal(first.Id, again.Id);
        Assert.Equal(2, CountUsers(database));
    }


    /// <summary>
    /// Several people signing in repeatedly give one row each, and each identifier keeps
    /// leading back to its own row.
    ///
    /// A service keying everybody on a constant would answer one row here and fail, which
    /// is the whole reason this test signs in more than one person.
    /// </summary>
    [Fact]
    public async Task EveryPersonKeepsTheirOwnRowAcrossRepeatedSignIns()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        UserProvisioningService service = CreateService(database);

        string anaObjectId = Guid.NewGuid().ToString();
        string brunoObjectId = Guid.NewGuid().ToString();
        string carlaObjectId = Guid.NewGuid().ToString();

        User ana = await service.EnsureUserAsync(
            TestData.NewPrincipal(anaObjectId, "Ana Dias", "ana@example.test"),
            Cancellation);

        User bruno = await service.EnsureUserAsync(
            TestData.NewPrincipal(brunoObjectId, "Bruno Lima", "bruno@example.test"),
            Cancellation);

        User carla = await service.EnsureUserAsync(
            TestData.NewPrincipal(carlaObjectId, "Carla Nunes", "carla@example.test"),
            Cancellation);

        await service.EnsureUserAsync(
            TestData.NewPrincipal(anaObjectId, "Ana Dias", "ana@example.test"),
            Cancellation);

        await service.EnsureUserAsync(
            TestData.NewPrincipal(brunoObjectId, "Bruno Lima", "bruno@example.test"),
            Cancellation);

        Assert.Equal(3, CountUsers(database));

        HashSet<int> distinctIds = new HashSet<int>
        {
            ana.Id,
            bruno.Id,
            carla.Id,
        };

        Assert.Equal(3, distinctIds.Count);

        User? anaAgain = await service.FindByEntraObjectIdAsync(anaObjectId, Cancellation);
        User? brunoAgain = await service.FindByEntraObjectIdAsync(brunoObjectId, Cancellation);
        User? carlaAgain = await service.FindByEntraObjectIdAsync(carlaObjectId, Cancellation);

        Assert.NotNull(anaAgain);
        Assert.NotNull(brunoAgain);
        Assert.NotNull(carlaAgain);
        Assert.Equal("Ana Dias", anaAgain.DisplayName);
        Assert.Equal("Bruno Lima", brunoAgain.DisplayName);
        Assert.Equal("Carla Nunes", carlaAgain.DisplayName);
    }

    #endregion


    #region Refusals

    /// <summary>A token with no object identifier is refused and leaves the table untouched.</summary>
    [Fact]
    public async Task ASignInWithoutAnObjectIdentifierIsRefused()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        UserProvisioningService service = CreateService(database);

        ClaimsPrincipal principal = TestData.NewPrincipal(null, "Ana Dias", "ana@example.test");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service.EnsureUserAsync(principal, Cancellation);
        });

        Assert.Equal(0, CountUsers(database));
    }


    /// <summary>
    /// An object identifier claim that is not a GUID is not an object identifier, whatever
    /// claim type carried it, and is refused rather than becoming somebody's identity.
    /// </summary>
    [Fact]
    public async Task ASignInWithAnObjectIdentifierThatIsNotAGuidIsRefused()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();
        UserProvisioningService service = CreateService(database);

        ClaimsPrincipal principal = TestData.NewPrincipal(
            "ana.dias@example.test",
            "Ana Dias",
            "ana@example.test");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service.EnsureUserAsync(principal, Cancellation);
        });

        Assert.Equal(0, CountUsers(database));
    }

    #endregion


    #region The unique index on the object identifier

    /// <summary>
    /// Losing the race to create the same person is success, gives one row, and answers the
    /// row the winner created, with the winner's identifier.
    /// </summary>
    [Fact]
    public async Task LosingTheRaceOnTheUserIndexIsTreatedAsSuccess()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        string objectId = Guid.NewGuid().ToString();

        Dictionary<string, object> competingValues = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            { "$entraObjectId", objectId },
            { "$displayName", "Ana Dias" },
            { "$email", "ana@example.test" },
            { "$firstSeenAt", "2026-01-01 09:00:00" },
        };

        RacingInsertInterceptor racer = new RacingInsertInterceptor(
            database.ConnectionString,
            "User",
            "INSERT INTO \"User\" (\"entraObjectId\", \"displayName\", \"email\", \"firstSeenAt\") " +
            "VALUES ($entraObjectId, $displayName, $email, $firstSeenAt);",
            competingValues);

        UserProvisioningService service = CreateService(database, racer);

        User provisioned = await service.EnsureUserAsync(
            TestData.NewPrincipal(objectId, "Ana Dias", "ana@example.test"),
            Cancellation);

        Assert.True(racer.Raced, "The competing user row must actually have been written.");
        Assert.Equal(1, CountUsers(database));

        long winnerId = database.ExecuteScalarCount("SELECT MIN(\"id\") FROM \"User\";");

        Assert.Equal(winnerId, provisioned.Id);
        Assert.Equal(objectId, provisioned.EntraObjectId);
    }


    /// <summary>
    /// A write that fails for a reason other than the object identifier index rises as an
    /// exception, and the person is never handed a row that was not written.
    ///
    /// The failure is forced with a trigger that aborts the insert, because the User table
    /// has no foreign key of its own to violate. SQLite reports it as a constraint failure
    /// with a different extended code, which is exactly the case a detector that only
    /// looked at the exception type would get wrong.
    /// </summary>
    [Fact]
    public async Task AWriteFailureThatIsNotTheUserIndexRises()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        database.ExecuteNonQuery(
            "CREATE TRIGGER refuse_user_insert BEFORE INSERT ON \"User\" " +
            "BEGIN SELECT RAISE(ABORT, 'the disk said no'); END;");

        UserProvisioningService service = CreateService(database);

        DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await service.EnsureUserAsync(
                TestData.NewPrincipal(Guid.NewGuid().ToString(), "Ana Dias", "ana@example.test"),
                Cancellation);
        });

        SqliteException inner = Assert.IsType<SqliteException>(failure.InnerException);

        // SQLITE_CONSTRAINT_TRIGGER, which shares its primary code with the unique index
        // violation and differs only in the extended one.
        Assert.Equal(19, inner.SqliteErrorCode);
        Assert.Equal(1811, inner.SqliteExtendedErrorCode);
        Assert.False(SqliteUniqueConstraint.IsViolation(failure));
        Assert.Equal(0, CountUsers(database));
    }

    #endregion
}
