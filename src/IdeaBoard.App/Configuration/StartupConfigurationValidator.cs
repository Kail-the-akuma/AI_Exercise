using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace IdeaBoard.App.Configuration;

/// <summary>
/// Checks, before the application starts, every configuration value it cannot work
/// without. A missing value stops startup with the key named, instead of surfacing later
/// as a broken request that looks like a product defect.
/// </summary>
public static class StartupConfigurationValidator
{
    #region Entry point

    /// <summary>
    /// Collects every configuration problem that must stop startup. The rules differ
    /// between development and the server on purpose, and the difference is always in the
    /// direction of refusing to start, never of skipping a check that protects a user.
    /// </summary>
    public static ConfigurationValidationResult Validate(
        IConfiguration configuration,
        bool isDevelopment,
        string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        List<string> problems = new List<string>();

        ValidateAzureAd(configuration, problems);
        ValidateDatabase(configuration, isDevelopment, contentRootPath, problems);
        ValidateDataProtection(configuration, isDevelopment, problems);
        ValidateTimeZone(configuration, problems);

        return new ConfigurationValidationResult(problems);
    }

    #endregion


    #region Authentication

    /// <summary>
    /// Requires the four sign-in keys to be present and non-empty, in every environment.
    /// Presence only: no format check, no network call, and no client secret. Whether a
    /// client secret is needed is an open decision, and a validator that demanded one
    /// would be closing that decision silently, through a line of code.
    /// </summary>
    private static void ValidateAzureAd(IConfiguration configuration, List<string> problems)
    {
        RequirePresent(configuration, ConfigurationKeys.AzureAdInstance, problems);
        RequirePresent(configuration, ConfigurationKeys.AzureAdTenantId, problems);
        RequirePresent(configuration, ConfigurationKeys.AzureAdClientId, problems);
        RequirePresent(configuration, ConfigurationKeys.AzureAdCallbackPath, problems);
    }

    #endregion


    #region Storage

    /// <summary>
    /// Requires a connection string, and on a server requires the database file to sit
    /// outside the published folder. A file inside it is deleted by the next publish, so
    /// the rule the operations document only warns about is enforced here instead.
    /// </summary>
    private static void ValidateDatabase(
        IConfiguration configuration,
        bool isDevelopment,
        string contentRootPath,
        List<string> problems)
    {
        string? connectionString = configuration.GetConnectionString(ConfigurationKeys.DatabaseConnectionName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            problems.Add($"'{ConfigurationKeys.DatabaseConnectionKey}' is missing or empty.");
            return;
        }

        if (isDevelopment)
        {
            return;
        }

        string dataSource = ReadDataSource(connectionString);

        if (string.IsNullOrWhiteSpace(dataSource))
        {
            problems.Add($"'{ConfigurationKeys.DatabaseConnectionKey}' carries no Data Source.");
            return;
        }

        if (!Path.IsPathRooted(dataSource))
        {
            problems.Add(
                $"'{ConfigurationKeys.DatabaseConnectionKey}' must use an absolute path outside " +
                $"development, and '{dataSource}' is relative.");
            return;
        }

        if (IsInsideContentRoot(dataSource, contentRootPath))
        {
            problems.Add(
                $"'{ConfigurationKeys.DatabaseConnectionKey}' points inside the application folder " +
                $"('{dataSource}'). A database kept there is deleted by the next publish.");
        }
    }


    /// <summary>Reads the Data Source of a SQLite connection string, or an empty string.</summary>
    private static string ReadDataSource(string connectionString)
    {
        try
        {
            SqliteConnectionStringBuilder connectionBuilder = new SqliteConnectionStringBuilder(connectionString);
            return connectionBuilder.DataSource;
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }


    /// <summary>Tells whether a path resolves to somewhere under the content root.</summary>
    private static bool IsInsideContentRoot(string dataSource, string contentRootPath)
    {
        string fullDataSource = Path.GetFullPath(dataSource);
        string fullContentRoot = Path.GetFullPath(contentRootPath);

        if (!fullContentRoot.EndsWith(Path.DirectorySeparatorChar))
        {
            fullContentRoot += Path.DirectorySeparatorChar;
        }

        return fullDataSource.StartsWith(fullContentRoot, StringComparison.OrdinalIgnoreCase);
    }

    #endregion


    #region Data Protection

    /// <summary>
    /// Requires a key ring folder on a server. In development the platform default is used
    /// and a warning is logged, because the symptom of an unpersisted key ring is people
    /// being thrown out of their session at random, which nobody diagnoses quickly.
    /// </summary>
    private static void ValidateDataProtection(
        IConfiguration configuration,
        bool isDevelopment,
        List<string> problems)
    {
        if (isDevelopment)
        {
            return;
        }

        RequirePresent(configuration, ConfigurationKeys.DataProtectionKeysPath, problems);
    }

    #endregion


    #region Presentation

    /// <summary>
    /// Requires a time zone identifier that this machine can resolve. Stored instants are
    /// UTC and the identifier is fixed in configuration rather than taken from the server
    /// clock, so an unresolvable value has to stop startup and not surface as wrong times.
    /// </summary>
    private static void ValidateTimeZone(IConfiguration configuration, List<string> problems)
    {
        string? timeZoneId = configuration[ConfigurationKeys.LocalizationTimeZoneId];

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            problems.Add($"'{ConfigurationKeys.LocalizationTimeZoneId}' is missing or empty.");
            return;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            problems.Add(
                $"'{ConfigurationKeys.LocalizationTimeZoneId}' is set to '{timeZoneId}', which this " +
                $"machine cannot resolve.");
        }
        catch (InvalidTimeZoneException)
        {
            problems.Add(
                $"'{ConfigurationKeys.LocalizationTimeZoneId}' is set to '{timeZoneId}', which is " +
                $"corrupt on this machine.");
        }
    }

    #endregion


    #region Shared

    /// <summary>Records a problem when a key is absent or holds only whitespace.</summary>
    private static void RequirePresent(IConfiguration configuration, string key, List<string> problems)
    {
        string? value = configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            problems.Add($"'{key}' is missing or empty.");
        }
    }

    #endregion
}
