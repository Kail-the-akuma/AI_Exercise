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
    /// Without the sign-in configuration the host refuses to start, and says which keys are
    /// missing.
    ///
    /// Everything else is valid here, including a database already in write-ahead logging
    /// mode, so the other guard cannot be what fired. The tracked settings file ships
    /// AzureAd:TenantId and AzureAd:ClientId as empty placeholders, which is the shape a
    /// fresh clone has, and the validator treats empty and absent alike.
    /// </summary>
    [Fact]
    public void StartingWithoutTheSignInConfigurationIsRefusedByName()
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

        Exception failure = Assert.ThrowsAny<Exception>(() =>
        {
            using IdeaBoardWebFactory factory = new IdeaBoardWebFactory();
            factory.CreateClient();
        });

        string message = ExceptionText.Describe(failure);

        Assert.Contains(ConfigurationIncompleteSignature, message, StringComparison.Ordinal);
        Assert.Contains("AzureAd:TenantId", message, StringComparison.Ordinal);
        Assert.Contains("AzureAd:ClientId", message, StringComparison.Ordinal);

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
