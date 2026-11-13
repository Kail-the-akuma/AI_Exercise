using System.Globalization;
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

        ValidateDatabase(configuration, isDevelopment, contentRootPath, problems);
        ValidateDataProtection(configuration, isDevelopment, contentRootPath, problems);
        ValidateTimeZone(configuration, problems);
        ValidateDirectoryLookup(configuration, problems);

        return new ConfigurationValidationResult(problems);
    }

    #endregion


    #region Authentication

    // There is deliberately no authentication validation here any more, and a capability
    // was LOST when it went, which is why this is written down rather than quietly deleted.
    //
    // It used to require four sign-in keys to be present, which amounted to saying that
    // without authentication configuration the application does nothing useful. That was
    // checkable because the configuration was within the application's reach.
    //
    // Under Windows authentication it is not. The settings live in the site's web.config,
    // in the IIS configuration, outside anything this process can read at startup. The host
    // can now start perfectly with Windows authentication switched off and anonymous
    // authentication switched on, and nothing in here will notice. The symptom is a 401 on
    // the first request, visible but unexplained.
    //
    // Detecting that moved to the manual verification list, which is the honest place for
    // it. Do not put a check back here: with the four keys gone from configuration, any
    // check written against them would refuse to start the very host it is meant to guard.

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
        string contentRootPath,
        List<string> problems)
    {
        if (isDevelopment)
        {
            return;
        }

        string? keysPath = configuration[ConfigurationKeys.DataProtectionKeysPath];

        if (string.IsNullOrWhiteSpace(keysPath))
        {
            problems.Add($"'{ConfigurationKeys.DataProtectionKeysPath}' is missing or empty.");
            return;
        }

        if (!Path.IsPathRooted(keysPath))
        {
            problems.Add(
                $"'{ConfigurationKeys.DataProtectionKeysPath}' must use an absolute path outside " +
                $"development, and '{keysPath}' is relative.");
            return;
        }

        if (IsInsideContentRoot(keysPath, contentRootPath))
        {
            problems.Add(
                $"'{ConfigurationKeys.DataProtectionKeysPath}' points inside the application folder " +
                $"('{keysPath}'). A key ring kept there is deleted by the next publish, and the " +
                $"symptom is people being thrown out of their session and submissions failing with " +
                $"antiforgery errors, intermittently, with nothing in the code to explain it.");
        }
    }

    #endregion


    #region Directory

    /// <summary>
    /// Checks the directory settings when they are present, and accepts their absence.
    ///
    /// Absence is valid because both have documented defaults, and requiring them would
    /// break every existing configuration file to say nothing new. A value that is present
    /// and unreadable is a different matter: it means somebody meant to change the
    /// behaviour and did not, which is worth stopping for rather than silently ignoring.
    /// </summary>
    private static void ValidateDirectoryLookup(IConfiguration configuration, List<string> problems)
    {
        string? enabled = configuration[ConfigurationKeys.DirectoryLookupEnabled];

        if (!string.IsNullOrWhiteSpace(enabled) && !bool.TryParse(enabled, out _))
        {
            problems.Add(
                $"'{ConfigurationKeys.DirectoryLookupEnabled}' is set to '{enabled}', which is " +
                $"not true or false.");
        }

        string? timeoutSeconds = configuration[ConfigurationKeys.DirectoryLookupTimeoutSeconds];

        if (string.IsNullOrWhiteSpace(timeoutSeconds))
        {
            return;
        }

        if (!int.TryParse(timeoutSeconds, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds))
        {
            problems.Add(
                $"'{ConfigurationKeys.DirectoryLookupTimeoutSeconds}' is set to '{timeoutSeconds}', " +
                $"which is not a whole number.");
            return;
        }

        if (seconds < 1 || seconds > DirectoryLookupOptions.MaximumTimeoutSeconds)
        {
            problems.Add(
                $"'{ConfigurationKeys.DirectoryLookupTimeoutSeconds}' is set to {seconds}, and has " +
                $"to be between 1 and {DirectoryLookupOptions.MaximumTimeoutSeconds}. A timeout of " +
                $"zero would make every lookup fail, and one longer than that is a person waiting " +
                $"in front of a blank page.");
        }
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
