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
/// Two traps this file is written around.
///
/// The first: a test that only checks "the second sign-in does not create a second row"
/// passes just as happily against a service that keys everybody on the same constant,
/// because then every sign-in collapses into one row. So every check here either
/// distinguishes two different people, or follows one person's identifier across sign-ins.
///
/// The second, learned the hard way: every negative test in this file went on passing the
/// day the identity provider changed, because not one of them ever provisioned anybody with
/// a value of the shape production would actually send. The fixtures now use a real
/// security identifier and a real DOMAIN\someone account name, and the tests below assert
/// those shapes, so that replacing them with invented strings that happen to pass would
/// itself fail.
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
        return CreateService(database, new FakeDisplayNameResolver(), null);
    }


    private static UserProvisioningService CreateService(
        TemporarySqliteDatabase database,
        IDisplayNameResolver resolver)
    {
        return CreateService(database, resolver, null);
    }


    private static UserProvisioningService CreateService(
        TemporarySqliteDatabase database,
        IDisplayNameResolver resolver,
        RacingInsertInterceptor? racer)
    {
        TemporaryDbContextFactory factory = new TemporaryDbContextFactory(database, racer);

        return new UserProvisioningService(
            factory,
            resolver,
            TimeProvider.System,
            NullLogger<UserProvisioningService>.Instance);
    }


    private static long CountUsers(TemporarySqliteDatabase database)
    {
        return database.ExecuteScalarCount("SELECT COUNT(*) FROM \"User\";");
    }

    #endregion


    #region The shape of real identity

    /// <summary>
    /// A security identifier of the shape Windows actually issues is accepted and stored
    /// unchanged.
    ///
    /// This is the test whose absence let the application break in silence. The identifier
    /// used to be validated by parsing it as a GUID, which was right while the provider
    /// issued GUIDs, and every negative test around it went on passing afterwards. Only
    /// starting from a value of the real new shape shows that the guard had begun refusing
    /// one hundred per cent of real users.
    /// </summary>
    [Fact]
    public async Task ASecurityIdentifierOfTheRealShapeIsAcceptedAndStored()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        FakeDisplayNameResolver directory = new FakeDisplayNameResolver("Ana Dias");
        UserProvisioningService service = CreateService(database, directory);

        string subjectId = TestData.NewSubjectId();
        string accountName = TestData.NewAccountName("Ana Dias");

        Assert.StartsWith("S-1-", subjectId, StringComparison.Ordinal);
        Assert.False(Guid.TryParse(subjectId, out _), "A real security identifier is not a GUID.");
        Assert.Contains('\\', accountName);

        User? provisioned = await service.TryEnsureUserAsync(
            TestData.NewWindowsPrincipal(subjectId, accountName),
            Cancellation);

        Assert.NotNull(provisioned);
        Assert.Equal(subjectId, provisioned.SubjectId);
        Assert.Equal(accountName, provisioned.AccountName);
        Assert.Equal("Ana Dias", provisioned.DisplayName);
        Assert.Equal(DisplayNameSource.Directory, provisioned.DisplayNameSource);
    }


    /// <summary>
    /// A display name with a backslash in it is refused, because that is an account name
    /// reaching the field colleagues see.
    /// </summary>
    [Fact]
    public async Task ADisplayNameShapedLikeAnAccountIsRefused()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        FakeDisplayNameResolver directory = new FakeDisplayNameResolver("EXEMPLO\\ana");
        UserProvisioningService service = CreateService(database, directory);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service.TryEnsureUserAsync(
                TestData.NewWindowsPrincipal(TestData.NewSubjectId(), "EXEMPLO\\ana"),
                Cancellation);
        });

        Assert.Equal(0, CountUsers(database));
    }


    /// <summary>
    /// The mirror: a value under the Windows account claim that is not of the
    /// DOMAIN\someone shape is refused, because something other than an account name
    /// arrived there.
    /// </summary>
    [Theory]
    [InlineData("Ana Dias")]
    [InlineData("EXEMPLO\\sub\\ana")]
    [InlineData("EXEMPLO\\")]
    [InlineData("\\ana")]
    public async Task AnAccountClaimWithoutTheAccountShapeIsRefused(string accountName)
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        UserProvisioningService service = CreateService(database, new FakeDisplayNameResolver("Ana Dias"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service.TryEnsureUserAsync(
                TestData.NewWindowsPrincipal(TestData.NewSubjectId(), accountName),
                Cancellation);
        });

        Assert.Equal(0, CountUsers(database));
    }


    [Fact]
    public async Task AnAbsentSubjectIdentifierIsRefused()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        UserProvisioningService service = CreateService(database);

        ClaimsPrincipal principal = TestData.NewWindowsPrincipal(null, TestData.NewAccountName("Ana Dias"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service.TryEnsureUserAsync(principal, Cancellation);
        });

        Assert.Equal(0, CountUsers(database));
    }


    [Theory]
    [InlineData("Ana Dias")]
    [InlineData("EXEMPLO\\ana")]
    [InlineData("ana@example.test")]
    [InlineData("0")]
    public async Task ASubjectIdentifierOfNeitherKnownShapeIsRefused(string subjectId)
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        UserProvisioningService service = CreateService(database);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service.TryEnsureUserAsync(
                TestData.NewWindowsPrincipal(subjectId, TestData.NewAccountName("Ana Dias")),
                Cancellation);
        });

        Assert.Equal(0, CountUsers(database));
    }


    /// <summary>
    /// The object identifier form is still accepted, because the hosts that are never
    /// published sign in with it and the end to end suite runs on one of them.
    /// </summary>
    [Fact]
    public async Task AnObjectIdentifierIsStillAcceptedForTheHostsThatUseOne()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        UserProvisioningService service = CreateService(database);

        string objectId = Guid.NewGuid().ToString();

        User? provisioned = await service.TryEnsureUserAsync(
            TestData.NewNamedPrincipal(objectId, "Ana Dias", TestData.NewAccountName("Ana Dias")),
            Cancellation);

        Assert.NotNull(provisioned);
        Assert.Equal(objectId, provisioned.SubjectId);
    }

    #endregion


    #region No name, no row

    /// <summary>
    /// Nothing is written for somebody whose name could not be obtained. The invariant that
    /// every user row has a name is what keeps a null check out of every page that renders
    /// an author or a voter.
    /// </summary>
    [Fact]
    public async Task NoNameMeansNoRowAndNoFailure()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        FakeDisplayNameResolver directory = new FakeDisplayNameResolver();
        UserProvisioningService service = CreateService(database, directory);

        User? provisioned = await service.TryEnsureUserAsync(
            TestData.NewWindowsPrincipal(TestData.NewSubjectId(), TestData.NewAccountName("Ana Dias")),
            Cancellation);

        Assert.Null(provisioned);
        Assert.Equal(0, CountUsers(database));
        Assert.Equal(1, directory.Calls);
    }


    /// <summary>
    /// Registering supplies the name the directory could not, writes the row, trims what
    /// was typed, and records that the person supplied it.
    /// </summary>
    [Fact]
    public async Task RegisteringWritesTheRowAndRecordsWhereTheNameCameFrom()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        UserProvisioningService service = CreateService(database);

        ClaimsPrincipal principal = TestData.NewWindowsPrincipal(
            TestData.NewSubjectId(),
            TestData.NewAccountName("Ana Dias"));

        Assert.Null(await service.TryEnsureUserAsync(principal, Cancellation));

        User registered = await service.RegisterAsync(principal, "  Ana Dias  ", Cancellation);

        Assert.Equal("Ana Dias", registered.DisplayName);
        Assert.Equal(DisplayNameSource.SelfRegistered, registered.DisplayNameSource);
        Assert.Equal(1, CountUsers(database));
    }


    /// <summary>
    /// Registering twice is one person, in the same way voting twice is one vote. Two tabs
    /// and a double submission are the ordinary ways in.
    /// </summary>
    [Fact]
    public async Task RegisteringTwiceGivesOneRow()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        UserProvisioningService service = CreateService(database);

        ClaimsPrincipal principal = TestData.NewWindowsPrincipal(
            TestData.NewSubjectId(),
            TestData.NewAccountName("Ana Dias"));

        User first = await service.RegisterAsync(principal, "Ana Dias", Cancellation);
        User second = await service.RegisterAsync(principal, "Ana Maria Dias", Cancellation);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, CountUsers(database));

        // The second submission did not get to rename anybody either.
        Assert.Equal("Ana Dias", second.DisplayName);
    }

    #endregion


    #region Where a name is allowed to come from

    /// <summary>
    /// A name that came from the directory follows the directory. Somebody who changes
    /// their family name stops appearing under the old one, which matters in a product
    /// whose whole subject is attributing ideas to named people.
    /// </summary>
    [Fact]
    public async Task ADirectoryNameFollowsALaterDirectoryChange()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        UserProvisioningService service = CreateService(database);

        string subjectId = TestData.NewSubjectId();
        string accountName = TestData.NewAccountName("Ana Dias");

        await service.TryEnsureUserAsync(
            TestData.NewNamedPrincipal(subjectId, "Ana Dias", accountName),
            Cancellation);

        User? renamed = await service.TryEnsureUserAsync(
            TestData.NewNamedPrincipal(subjectId, "Ana Dias Correia", accountName),
            Cancellation);

        Assert.NotNull(renamed);
        Assert.Equal("Ana Dias Correia", renamed.DisplayName);
        Assert.Equal(DisplayNameSource.Directory, renamed.DisplayNameSource);
        Assert.Equal(1, CountUsers(database));
    }


    /// <summary>
    /// A name the PERSON typed is never replaced, however loudly the directory later
    /// disagrees. Overwriting it is silent data loss: they would find out by seeing a name
    /// they did not choose on ideas that cannot be edited or removed.
    /// </summary>
    [Fact]
    public async Task ASelfRegisteredNameIsNeverOverwrittenByTheDirectory()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        UserProvisioningService service = CreateService(database);

        string subjectId = TestData.NewSubjectId();
        string accountName = TestData.NewAccountName("Ana Dias");

        await service.RegisterAsync(
            TestData.NewWindowsPrincipal(subjectId, accountName),
            "Ana Dias",
            Cancellation);

        User? afterDirectoryReturns = await service.TryEnsureUserAsync(
            TestData.NewNamedPrincipal(subjectId, "ANA DIAS CORREIA", accountName),
            Cancellation);

        Assert.NotNull(afterDirectoryReturns);
        Assert.Equal("Ana Dias", afterDirectoryReturns.DisplayName);
        Assert.Equal(DisplayNameSource.SelfRegistered, afterDirectoryReturns.DisplayNameSource);
    }


    /// <summary>
    /// The pair that proves the rule is about ORIGIN and not about freezing. A person who
    /// registered their own name, and whose ACCOUNT is then renamed in the directory, keeps
    /// their name and gets the new account name.
    ///
    /// This is the one that a single condition wrapping both assignments gets wrong, in
    /// either direction: one guard covering both columns either freezes the account name,
    /// which sends somebody reading the table by hand to the wrong row during a manual
    /// correction, or unfreezes the display name, which is the silent data loss above.
    /// </summary>
    [Fact]
    public async Task AnAccountRenameIsFollowedEvenWhenTheNameIsSelfRegistered()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        UserProvisioningService service = CreateService(database);

        string subjectId = TestData.NewSubjectId();
        string oldAccountName = "EXEMPLO\\adias";
        string newAccountName = "EXEMPLO\\acorreia";

        await service.RegisterAsync(
            TestData.NewWindowsPrincipal(subjectId, oldAccountName),
            "Ana Dias",
            Cancellation);

        User? afterRename = await service.TryEnsureUserAsync(
            TestData.NewWindowsPrincipal(subjectId, newAccountName),
            Cancellation);

        Assert.NotNull(afterRename);
        Assert.Equal(newAccountName, afterRename.AccountName);
        Assert.Equal("Ana Dias", afterRename.DisplayName);
        Assert.Equal(DisplayNameSource.SelfRegistered, afterRename.DisplayNameSource);
    }


    /// <summary>
    /// The directory is paid for once. Somebody who already has a row never triggers a
    /// lookup again, which is invisible in the result and only observable by counting.
    /// </summary>
    [Fact]
    public async Task TheDirectoryIsNotConsultedForSomebodyWhoAlreadyHasARow()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        FakeDisplayNameResolver directory = new FakeDisplayNameResolver("Ana Dias");
        UserProvisioningService service = CreateService(database, directory);

        ClaimsPrincipal principal = TestData.NewWindowsPrincipal(
            TestData.NewSubjectId(),
            TestData.NewAccountName("Ana Dias"));

        await service.TryEnsureUserAsync(principal, Cancellation);
        await service.TryEnsureUserAsync(principal, Cancellation);
        await service.TryEnsureUserAsync(principal, Cancellation);

        Assert.Equal(1, directory.Calls);
    }


    /// <summary>
    /// The directory is looked up by the security identifier, which arrives in the request
    /// and survives a rename, and not by the account name, which does not.
    /// </summary>
    [Fact]
    public async Task TheDirectoryIsLookedUpByTheSecurityIdentifier()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        FakeDisplayNameResolver directory = new FakeDisplayNameResolver("Ana Dias");
        UserProvisioningService service = CreateService(database, directory);

        string subjectId = TestData.NewSubjectId();

        await service.TryEnsureUserAsync(
            TestData.NewWindowsPrincipal(subjectId, TestData.NewAccountName("Ana Dias")),
            Cancellation);

        Assert.Equal(subjectId, directory.LastIdentifier);
    }

    #endregion


    #region Telling people apart

    /// <summary>
    /// Two different people stay two people, and one person keeps their row across
    /// sign-ins. A service keying everybody on a constant passes "no duplicates" and fails
    /// here.
    /// </summary>
    [Fact]
    public async Task DifferentPeopleGetDifferentRowsAndOnePersonKeepsTheirs()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        UserProvisioningService service = CreateService(database);

        string anaId = TestData.NewSubjectId();
        string brunoId = TestData.NewSubjectId();
        string carlaId = TestData.NewSubjectId();

        User? ana = await service.TryEnsureUserAsync(
            TestData.NewNamedPrincipal(anaId, "Ana Dias", "EXEMPLO\\adias"),
            Cancellation);
        User? bruno = await service.TryEnsureUserAsync(
            TestData.NewNamedPrincipal(brunoId, "Bruno Lima", "EXEMPLO\\blima"),
            Cancellation);
        User? carla = await service.TryEnsureUserAsync(
            TestData.NewNamedPrincipal(carlaId, "Carla Nunes", "EXEMPLO\\cnunes"),
            Cancellation);

        await service.TryEnsureUserAsync(
            TestData.NewNamedPrincipal(anaId, "Ana Dias", "EXEMPLO\\adias"),
            Cancellation);
        await service.TryEnsureUserAsync(
            TestData.NewNamedPrincipal(brunoId, "Bruno Lima", "EXEMPLO\\blima"),
            Cancellation);

        Assert.NotNull(ana);
        Assert.NotNull(bruno);
        Assert.NotNull(carla);

        Assert.Equal(3, CountUsers(database));
        Assert.Equal(3, new HashSet<int> { ana.Id, bruno.Id, carla.Id }.Count);

        User? anaAgain = await service.FindBySubjectIdAsync(anaId, Cancellation);
        User? carlaAgain = await service.FindBySubjectIdAsync(carlaId, Cancellation);

        Assert.NotNull(anaAgain);
        Assert.NotNull(carlaAgain);
        Assert.Equal(ana.Id, anaAgain.Id);
        Assert.Equal(carla.Id, carlaAgain.Id);
    }


    /// <summary>
    /// Two people with the SAME display name stay two people. This is the case the account
    /// name column exists for.
    /// </summary>
    [Fact]
    public async Task TwoPeopleWithTheSameDisplayNameStayTwoPeople()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        UserProvisioningService service = CreateService(database);

        User? first = await service.TryEnsureUserAsync(
            TestData.NewNamedPrincipal(TestData.NewSubjectId(), "Ana Dias", "EXEMPLO\\adias"),
            Cancellation);
        User? second = await service.TryEnsureUserAsync(
            TestData.NewNamedPrincipal(TestData.NewSubjectId(), "Ana Dias", "EXEMPLO\\adias2"),
            Cancellation);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.AccountName, second.AccountName);
        Assert.Equal(2, CountUsers(database));
    }

    #endregion


    #region Races and write failures

    /// <summary>
    /// Losing the race to create the same person is success, gives one row, and answers the
    /// row the winner created.
    /// </summary>
    [Fact]
    public async Task LosingTheRaceOnTheUserIndexIsTreatedAsSuccess()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        string subjectId = TestData.NewSubjectId();
        string accountName = TestData.NewAccountName("Ana Dias");

        Dictionary<string, object> competingValues = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            { "$subjectId", subjectId },
            { "$displayName", "Ana Dias" },
            { "$displayNameSource", DisplayNameSource.Directory.ToString() },
            { "$accountName", accountName },
            { "$firstSeenAt", "2026-01-01 09:00:00" },
        };

        RacingInsertInterceptor racer = new RacingInsertInterceptor(
            database.ConnectionString,
            "User",
            "INSERT INTO \"User\" (\"subjectId\", \"displayName\", \"displayNameSource\", " +
            "\"accountName\", \"firstSeenAt\") VALUES ($subjectId, $displayName, " +
            "$displayNameSource, $accountName, $firstSeenAt);",
            competingValues);

        UserProvisioningService service = CreateService(
            database,
            new FakeDisplayNameResolver("Ana Dias"),
            racer);

        User? provisioned = await service.TryEnsureUserAsync(
            TestData.NewWindowsPrincipal(subjectId, accountName),
            Cancellation);

        Assert.True(racer.Raced, "The competing user row must actually have been written.");
        Assert.NotNull(provisioned);
        Assert.Equal(1, CountUsers(database));

        long winnerId = database.ExecuteScalarCount("SELECT MIN(\"id\") FROM \"User\";");

        Assert.Equal(winnerId, provisioned.Id);
        Assert.Equal(subjectId, provisioned.SubjectId);
    }


    /// <summary>
    /// A write that fails for a reason other than the subject identifier index rises as an
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

        UserProvisioningService service = CreateService(
            database,
            new FakeDisplayNameResolver("Ana Dias"));

        DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await service.TryEnsureUserAsync(
                TestData.NewWindowsPrincipal(TestData.NewSubjectId(), TestData.NewAccountName("Ana Dias")),
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
