using IdeaBoard.App.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace IdeaBoard.Tests.Unidade;

/// <summary>
/// The startup guard, which is the one piece of this application that only ever runs its
/// interesting path when something is wrong. Everything that happens on a normal day
/// happens with the configuration already correct, so these tests are the only evidence
/// that the guard is not decorative.
/// </summary>
public class StartupConfigurationValidatorTests
{
    #region Helpers

    /// <summary>Folder the application is taken to be published into.</summary>
    private static string BuildContentRoot()
    {
        return Path.Combine(Path.GetTempPath(), "IdeaBoardApplication");
    }


    /// <summary>A database path on a server: absolute, and outside the published folder.</summary>
    private static string BuildPathOutsideContentRoot()
    {
        return Path.Combine(Path.GetTempPath(), "IdeaBoardData", "ideaboard.db");
    }


    /// <summary>A database path that the next publish would delete.</summary>
    private static string BuildPathInsideContentRoot()
    {
        return Path.Combine(BuildContentRoot(), "App_Data", "ideaboard.db");
    }


    /// <summary>
    /// Every value the application refuses to start without, with no client secret, which
    /// is deliberately not one of them.
    /// </summary>
    private static Dictionary<string, string?> BuildCompleteSettings()
    {
        Dictionary<string, string?> settings = new Dictionary<string, string?>(StringComparer.Ordinal);

        settings.Add(ConfigurationKeys.AzureAdInstance, "https://login.microsoftonline.com/");
        settings.Add(ConfigurationKeys.AzureAdTenantId, "11111111-1111-1111-1111-111111111111");
        settings.Add(ConfigurationKeys.AzureAdClientId, "22222222-2222-2222-2222-222222222222");
        settings.Add(ConfigurationKeys.AzureAdCallbackPath, "/signin-oidc");
        settings.Add(ConfigurationKeys.DatabaseConnectionKey, "Data Source=" + BuildPathOutsideContentRoot());
        settings.Add(ConfigurationKeys.DataProtectionKeysPath, Path.Combine(Path.GetTempPath(), "IdeaBoardKeys"));
        settings.Add(ConfigurationKeys.LocalizationTimeZoneId, "Europe/Lisbon");

        return settings;
    }


    /// <summary>Builds the configuration from a plain dictionary, with no files involved.</summary>
    private static IConfiguration BuildConfiguration(Dictionary<string, string?> settings)
    {
        ConfigurationBuilder builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(settings);

        return builder.Build();
    }


    /// <summary>Runs the validator over a set of settings.</summary>
    private static ConfigurationValidationResult Validate(
        Dictionary<string, string?> settings,
        bool isDevelopment)
    {
        return StartupConfigurationValidator.Validate(
            BuildConfiguration(settings),
            isDevelopment,
            BuildContentRoot());
    }


    /// <summary>Tells whether any reported problem quotes a given piece of text.</summary>
    private static bool Mentions(ConfigurationValidationResult result, string expected)
    {
        foreach (string problem in result.Problems)
        {
            if (problem.Contains(expected, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    #endregion


    #region Complete configuration

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ACompleteConfigurationIsAcceptedInEveryEnvironment(bool isDevelopment)
    {
        ConfigurationValidationResult result = Validate(BuildCompleteSettings(), isDevelopment);

        Assert.Empty(result.Problems);
        Assert.True(result.IsValid);
    }

    #endregion


    #region Sign-in keys

    [Theory]
    [InlineData(ConfigurationKeys.AzureAdInstance)]
    [InlineData(ConfigurationKeys.AzureAdTenantId)]
    [InlineData(ConfigurationKeys.AzureAdClientId)]
    [InlineData(ConfigurationKeys.AzureAdCallbackPath)]
    public void AMissingSignInKeyStopsStartupAndIsNamed(string key)
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings.Remove(key);

        ConfigurationValidationResult result = Validate(settings, false);

        Assert.False(result.IsValid);
        Assert.True(Mentions(result, key), $"No problem named '{key}'.");
    }


    [Theory]
    [InlineData(ConfigurationKeys.AzureAdInstance, "")]
    [InlineData(ConfigurationKeys.AzureAdInstance, "   ")]
    [InlineData(ConfigurationKeys.AzureAdTenantId, "")]
    [InlineData(ConfigurationKeys.AzureAdTenantId, "  \t ")]
    [InlineData(ConfigurationKeys.AzureAdClientId, "")]
    [InlineData(ConfigurationKeys.AzureAdClientId, " ")]
    [InlineData(ConfigurationKeys.AzureAdCallbackPath, "")]
    [InlineData(ConfigurationKeys.AzureAdCallbackPath, "    ")]
    public void ASignInKeyHoldingOnlyWhitespaceCountsAsMissing(string key, string value)
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings[key] = value;

        ConfigurationValidationResult result = Validate(settings, false);

        Assert.False(result.IsValid);
        Assert.True(Mentions(result, key), $"No problem named '{key}'.");
    }


    /// <summary>
    /// Whether a client secret is needed is still an open human decision. A validator that
    /// demanded one would close it in silence, so its absence has to leave startup valid.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AClientSecretIsNotRequired(bool isDevelopment)
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();

        ConfigurationValidationResult result = Validate(settings, isDevelopment);

        Assert.True(result.IsValid);
        Assert.False(Mentions(result, "ClientSecret"), "A client secret was demanded.");
    }


    /// <summary>
    /// Presence only, never format. The end to end suite starts the application with made
    /// up sign-in values, and a format check here would stop it from starting at all.
    /// </summary>
    [Theory]
    [InlineData("nao-e-um-guid")]
    [InlineData("xxx")]
    [InlineData("common")]
    [InlineData("0")]
    public void ATenantIdIsNotCheckedForFormat(string tenantId)
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings[ConfigurationKeys.AzureAdTenantId] = tenantId;

        ConfigurationValidationResult result = Validate(settings, false);

        Assert.Empty(result.Problems);
        Assert.True(result.IsValid);
    }


    [Theory]
    [InlineData("nao-e-um-url")]
    [InlineData("signin")]
    public void TheOtherSignInValuesAreNotCheckedForFormatEither(string value)
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings[ConfigurationKeys.AzureAdInstance] = value;
        settings[ConfigurationKeys.AzureAdCallbackPath] = value;

        ConfigurationValidationResult result = Validate(settings, false);

        Assert.True(result.IsValid);
    }

    #endregion


    #region Database

    [Fact]
    public void AMissingConnectionStringStopsStartupAndIsNamed()
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings.Remove(ConfigurationKeys.DatabaseConnectionKey);

        ConfigurationValidationResult result = Validate(settings, false);

        Assert.False(result.IsValid);
        Assert.True(
            Mentions(result, ConfigurationKeys.DatabaseConnectionKey),
            "No problem named the connection string.");
    }


    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyConnectionStringCountsAsMissing(string connectionString)
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings[ConfigurationKeys.DatabaseConnectionKey] = connectionString;

        ConfigurationValidationResult result = Validate(settings, true);

        Assert.False(result.IsValid);
        Assert.True(
            Mentions(result, ConfigurationKeys.DatabaseConnectionKey),
            "No problem named the connection string.");
    }


    [Fact]
    public void ARelativeDatabasePathIsRejectedOutsideDevelopment()
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings[ConfigurationKeys.DatabaseConnectionKey] = "Data Source=ideaboard.db";

        ConfigurationValidationResult result = Validate(settings, false);

        Assert.False(result.IsValid);
        Assert.True(
            Mentions(result, ConfigurationKeys.DatabaseConnectionKey),
            "No problem named the connection string.");
    }


    [Fact]
    public void ARelativeDatabasePathIsAcceptedInDevelopment()
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings[ConfigurationKeys.DatabaseConnectionKey] = "Data Source=ideaboard.db";
        settings.Remove(ConfigurationKeys.DataProtectionKeysPath);

        ConfigurationValidationResult result = Validate(settings, true);

        Assert.True(result.IsValid);
    }


    /// <summary>
    /// A database kept inside the published folder is deleted by the next publish, which is
    /// a data loss that looks like nothing at all until the day it happens.
    /// </summary>
    [Fact]
    public void ADatabaseInsideTheApplicationFolderIsRejectedOutsideDevelopment()
    {
        string dataSource = BuildPathInsideContentRoot();

        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings[ConfigurationKeys.DatabaseConnectionKey] = "Data Source=" + dataSource;

        ConfigurationValidationResult result = Validate(settings, false);

        Assert.False(result.IsValid);
        Assert.True(
            Mentions(result, ConfigurationKeys.DatabaseConnectionKey),
            "No problem named the connection string.");
        Assert.True(Mentions(result, dataSource), "The offending path was not quoted.");
    }


    [Fact]
    public void ADatabaseAtTheRootOfTheApplicationFolderIsRejectedOutsideDevelopment()
    {
        string dataSource = Path.Combine(BuildContentRoot(), "ideaboard.db");

        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings[ConfigurationKeys.DatabaseConnectionKey] = "Data Source=" + dataSource;

        ConfigurationValidationResult result = Validate(settings, false);

        Assert.False(result.IsValid);
    }


    /// <summary>
    /// The folder the application runs from is reported with a trailing separator, so the
    /// check has to hold with one as well as without.
    /// </summary>
    [Fact]
    public void ADatabaseInsideTheApplicationFolderIsRejectedWithATrailingSeparatorToo()
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings[ConfigurationKeys.DatabaseConnectionKey] = "Data Source=" + BuildPathInsideContentRoot();

        ConfigurationValidationResult result = StartupConfigurationValidator.Validate(
            BuildConfiguration(settings),
            false,
            BuildContentRoot() + Path.DirectorySeparatorChar);

        Assert.False(result.IsValid);
    }


    /// <summary>
    /// A sibling folder whose name merely starts with the application folder's name is not
    /// inside it, and rejecting it would refuse a perfectly good server layout.
    /// </summary>
    [Fact]
    public void ADatabaseInASiblingFolderWithASimilarNameIsAccepted()
    {
        string dataSource = Path.Combine(BuildContentRoot() + "Data", "ideaboard.db");

        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings[ConfigurationKeys.DatabaseConnectionKey] = "Data Source=" + dataSource;

        ConfigurationValidationResult result = Validate(settings, false);

        Assert.Empty(result.Problems);
        Assert.True(result.IsValid);
    }


    [Theory]
    [InlineData("Cache=Shared")]
    [InlineData("isto nao e uma cadeia de ligacao")]
    public void AConnectionStringWithoutAUsableDataSourceIsRejectedOutsideDevelopment(string connectionString)
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings[ConfigurationKeys.DatabaseConnectionKey] = connectionString;

        ConfigurationValidationResult result = Validate(settings, false);

        Assert.False(result.IsValid);
        Assert.True(
            Mentions(result, ConfigurationKeys.DatabaseConnectionKey),
            "No problem named the connection string.");
    }


    [Fact]
    public void AnAbsoluteDatabasePathOutsideTheApplicationFolderIsAccepted()
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings[ConfigurationKeys.DatabaseConnectionKey] = "Data Source=" + BuildPathOutsideContentRoot();

        ConfigurationValidationResult result = Validate(settings, false);

        Assert.Empty(result.Problems);
        Assert.True(result.IsValid);
    }

    #endregion


    #region Data Protection

    /// <summary>
    /// In development the platform default key ring is used, so the folder is not demanded.
    /// </summary>
    [Fact]
    public void TheKeyRingFolderIsNotRequiredInDevelopment()
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings.Remove(ConfigurationKeys.DataProtectionKeysPath);

        ConfigurationValidationResult result = Validate(settings, true);

        Assert.Empty(result.Problems);
        Assert.True(result.IsValid);
    }


    [Fact]
    public void TheKeyRingFolderIsRequiredOutsideDevelopment()
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings.Remove(ConfigurationKeys.DataProtectionKeysPath);

        ConfigurationValidationResult result = Validate(settings, false);

        Assert.False(result.IsValid);
        Assert.True(
            Mentions(result, ConfigurationKeys.DataProtectionKeysPath),
            "No problem named the key ring folder.");
    }


    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyKeyRingFolderCountsAsMissingOutsideDevelopment(string keysPath)
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings[ConfigurationKeys.DataProtectionKeysPath] = keysPath;

        ConfigurationValidationResult result = Validate(settings, false);

        Assert.False(result.IsValid);
        Assert.True(
            Mentions(result, ConfigurationKeys.DataProtectionKeysPath),
            "No problem named the key ring folder.");
    }

    #endregion


    #region Time zone

    [Fact]
    public void AMissingTimeZoneStopsStartupAndIsNamed()
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings.Remove(ConfigurationKeys.LocalizationTimeZoneId);

        ConfigurationValidationResult result = Validate(settings, true);

        Assert.False(result.IsValid);
        Assert.True(
            Mentions(result, ConfigurationKeys.LocalizationTimeZoneId),
            "No problem named the time zone key.");
    }


    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void AnEmptyTimeZoneCountsAsMissing(string timeZoneId)
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings[ConfigurationKeys.LocalizationTimeZoneId] = timeZoneId;

        ConfigurationValidationResult result = Validate(settings, true);

        Assert.False(result.IsValid);
        Assert.True(
            Mentions(result, ConfigurationKeys.LocalizationTimeZoneId),
            "No problem named the time zone key.");
    }


    [Theory]
    [InlineData("Marte/Olimpo")]
    [InlineData("Europe/Lisboa")]
    [InlineData("nao-e-um-fuso")]
    public void ATimeZoneThisMachineCannotResolveStopsStartupWithTheValueQuoted(string timeZoneId)
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings[ConfigurationKeys.LocalizationTimeZoneId] = timeZoneId;

        ConfigurationValidationResult result = Validate(settings, true);

        Assert.False(result.IsValid);
        Assert.True(Mentions(result, timeZoneId), $"The value '{timeZoneId}' was not quoted.");
    }


    /// <summary>
    /// The identifier the application is configured with has to resolve on the machine the
    /// suite runs on, Windows included, where it is translated from its IANA name.
    /// </summary>
    [Fact]
    public void TheConfiguredTimeZoneResolves()
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings[ConfigurationKeys.LocalizationTimeZoneId] = "Europe/Lisbon";

        ConfigurationValidationResult result = Validate(settings, true);

        Assert.Empty(result.Problems);
        Assert.True(result.IsValid);
    }

    #endregion


    #region Reporting

    /// <summary>
    /// Someone who has just cloned the repository has to be able to fix everything in one
    /// pass, instead of discovering one missing key per restart.
    /// </summary>
    [Fact]
    public void EveryProblemIsNamedInTheFailureMessage()
    {
        Dictionary<string, string?> settings = new Dictionary<string, string?>(StringComparer.Ordinal);

        ConfigurationValidationResult result = Validate(settings, false);

        Assert.False(result.IsValid);

        string message = result.BuildFailureMessage();

        Assert.Contains(ConfigurationKeys.AzureAdInstance, message, StringComparison.Ordinal);
        Assert.Contains(ConfigurationKeys.AzureAdTenantId, message, StringComparison.Ordinal);
        Assert.Contains(ConfigurationKeys.AzureAdClientId, message, StringComparison.Ordinal);
        Assert.Contains(ConfigurationKeys.AzureAdCallbackPath, message, StringComparison.Ordinal);
        Assert.Contains(ConfigurationKeys.DatabaseConnectionKey, message, StringComparison.Ordinal);
        Assert.Contains(ConfigurationKeys.DataProtectionKeysPath, message, StringComparison.Ordinal);
        Assert.Contains(ConfigurationKeys.LocalizationTimeZoneId, message, StringComparison.Ordinal);

        foreach (string problem in result.Problems)
        {
            Assert.Contains(problem, message, StringComparison.Ordinal);
        }
    }


    [Fact]
    public void SeveralProblemsAreAllCollectedInsteadOfOnlyTheFirst()
    {
        Dictionary<string, string?> settings = BuildCompleteSettings();
        settings.Remove(ConfigurationKeys.AzureAdInstance);
        settings.Remove(ConfigurationKeys.DataProtectionKeysPath);
        settings[ConfigurationKeys.LocalizationTimeZoneId] = "Marte/Olimpo";

        ConfigurationValidationResult result = Validate(settings, false);

        Assert.Equal(3, result.Problems.Count);
    }

    #endregion


    #region Arguments

    [Fact]
    public void AnAbsentConfigurationIsRefused()
    {
        Assert.Throws<ArgumentNullException>(
            () => StartupConfigurationValidator.Validate(null!, false, BuildContentRoot()));
    }


    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyContentRootIsRefused(string contentRootPath)
    {
        IConfiguration configuration = BuildConfiguration(BuildCompleteSettings());

        Assert.Throws<ArgumentException>(
            () => StartupConfigurationValidator.Validate(configuration, false, contentRootPath));
    }

    #endregion
}
