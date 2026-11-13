using Xunit;

namespace IdeaBoard.Tests.Integracao;

/// <summary>
/// The two guards that refuse to start the published host.
///
/// Every other test in the solution runs with the guards satisfied, which means none of
/// them would notice if a guard were deleted. These two are the only evidence that the
/// guards are load bearing rather than decorative, so each one leaves everything else
/// valid and breaks exactly one thing: anything less and a failure for an unrelated reason
/// would satisfy the assertion just as well.
///
/// The two messages are checked against each other on purpose. If they were alike, each
/// test would pass on the other test's failure and both would be worthless.
/// </summary>
[Collection(HostTestCollection.Name)]
public class StartupGuardTests
{
    #region Constants

    private const string ConfigurationIncompleteSignature = "configuration is incomplete";
    private const string JournalModeSignature = "journal mode";

    #endregion


    #region Sign-in configuration

    /// <summary>
    /// The published host STARTS with the configuration production will actually have,
    /// which carries no sign-in keys at all.
    ///
    /// This replaces a test that asserted the opposite, and the replacement is the whole
    /// lesson. The old test proved the guard refused what was wrong, and it kept passing on
    /// the day the identity provider changed, while the guard it was protecting had quietly
    /// begun refusing every correct configuration there is. No negative test can notice
    /// that. Only one that starts from what production really holds can.
    ///
    /// Under Windows authentication the sign-in settings live in the site's web.config, in
    /// the IIS configuration, where this process cannot read them. The capability that was
    /// lost with the old guard is named in the validator and moved to the manual checks:
    /// the host can now start with Windows authentication switched off and anonymous
    /// authentication on, and nothing here will notice.
    ///
    /// Everything else is valid, including a database already in write-ahead logging mode,
    /// so a failure here could not be the other guard.
    /// </summary>
    [Fact]
    public void StartingWithoutAnySignInConfigurationIsAccepted()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        Assert.Equal("wal", database.EnableWriteAheadLogging());

        using EnvironmentVariableScope environment = new EnvironmentVariableScope();
        environment.Set("ASPNETCORE_ENVIRONMENT", "Development");
        environment.Set("ConnectionStrings__IdeaBoard", database.ConnectionString);
        environment.Set("Localization__TimeZoneId", "Europe/Lisbon");
        environment.Set("AzureAd__Instance", null);
        environment.Set("AzureAd__TenantId", null);
        environment.Set("AzureAd__ClientId", null);
        environment.Set("AzureAd__CallbackPath", null);

        using IdeaBoardWebFactory factory = new IdeaBoardWebFactory();

        // Creating the client is what builds and starts the host. Reaching the next line
        // without an exception is the assertion.
        using HttpClient client = factory.CreateClient();

        Assert.NotNull(client);
    }


    /// <summary>
    /// A directory timeout that cannot be read stops startup and is named, because somebody
    /// meant to change the behaviour and did not.
    /// </summary>
    [Fact]
    public void AnUnreadableDirectoryTimeoutIsRefusedByName()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        Assert.Equal("wal", database.EnableWriteAheadLogging());

        using EnvironmentVariableScope environment = new EnvironmentVariableScope();
        environment.Set("ASPNETCORE_ENVIRONMENT", "Development");
        environment.Set("ConnectionStrings__IdeaBoard", database.ConnectionString);
        environment.Set("Localization__TimeZoneId", "Europe/Lisbon");
        environment.Set("DirectoryLookup__TimeoutSeconds", "depressa");

        Exception failure = Assert.ThrowsAny<Exception>(() =>
        {
            using IdeaBoardWebFactory factory = new IdeaBoardWebFactory();
            factory.CreateClient();
        });

        string message = ExceptionText.Describe(failure);

        Assert.Contains(ConfigurationIncompleteSignature, message, StringComparison.Ordinal);
        Assert.Contains("DirectoryLookup:TimeoutSeconds", message, StringComparison.Ordinal);

        // Distinguishable from the other guard, otherwise this test would pass on its failure.
        Assert.DoesNotContain(JournalModeSignature, message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion


    #region Write-ahead logging

    /// <summary>
    /// Against a database that was never put into write-ahead logging mode the host refuses
    /// to start, and names the mode it found.
    ///
    /// The sign-in configuration is complete here, so the other guard cannot be what fired.
    /// The mode asserted is the one read back from the file rather than a mode this test
    /// assumes, so the check is that the message reports what is actually there.
    ///
    /// The database is taken out of write-ahead logging on purpose. Since Entity Framework
    /// Core 7 a database that Entity Framework creates is put into that mode as it is
    /// created, so a database built by the migrations already satisfies the guard and
    /// cannot be used to test it. What this reproduces is a database that reached the
    /// server by some other route than the documented provisioning.
    /// </summary>
    [Fact]
    public void StartingAgainstADatabaseThatIsNotInWriteAheadLoggingIsRefusedByMode()
    {
        using TemporarySqliteDatabase database = new TemporarySqliteDatabase();

        database.SetJournalMode("DELETE");

        string foundMode = database.ReadJournalMode();

        Assert.NotEqual("wal", foundMode, StringComparer.OrdinalIgnoreCase);

        using EnvironmentVariableScope environment = new EnvironmentVariableScope();
        environment.Set("ASPNETCORE_ENVIRONMENT", "Development");
        environment.Set("ConnectionStrings__IdeaBoard", database.ConnectionString);
        environment.Set("Localization__TimeZoneId", "Europe/Lisbon");
        environment.Set("AzureAd__Instance", "https://login.microsoftonline.com/");
        environment.Set("AzureAd__TenantId", "11111111-1111-1111-1111-111111111111");
        environment.Set("AzureAd__ClientId", "22222222-2222-2222-2222-222222222222");
        environment.Set("AzureAd__CallbackPath", "/signin-oidc");

        Exception failure = Assert.ThrowsAny<Exception>(() =>
        {
            using IdeaBoardWebFactory factory = new IdeaBoardWebFactory();
            factory.CreateClient();
        });

        string message = ExceptionText.Describe(failure);

        Assert.Contains(JournalModeSignature, message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(foundMode, message, StringComparison.OrdinalIgnoreCase);

        // Distinguishable from the other guard, otherwise this test would pass on its failure.
        Assert.DoesNotContain(ConfigurationIncompleteSignature, message, StringComparison.Ordinal);
    }

    #endregion
}
