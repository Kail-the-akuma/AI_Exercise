namespace IdeaBoard.App.Configuration;

/// <summary>
/// Configuration keys the application reads. They are named here once so that the startup
/// validator, the wiring and the error messages cannot drift apart.
/// </summary>
public static class ConfigurationKeys
{
    #region Authentication

    /// <summary>
    /// Section still bound by the published host's sign-in registration.
    ///
    /// Nothing validates it any more. Under Windows authentication the sign-in settings
    /// live in the site's web.config, out of this process's reach, so a startup check
    /// written against configuration keys would refuse to start the host it is guarding.
    /// </summary>
    public const string AzureAdSection = "AzureAd";

    #endregion


    #region Directory

    /// <summary>
    /// Whether the directory is consulted for somebody's display name at all.
    ///
    /// Switching it off forces the registration path. With the directory answering, that
    /// path is never taken and therefore never exercised, so the first time it would ever
    /// run for real is the day the directory breaks.
    /// </summary>
    public const string DirectoryLookupEnabled = "DirectoryLookup:Enabled";

    /// <summary>How long one directory lookup may take before it is abandoned.</summary>
    public const string DirectoryLookupTimeoutSeconds = "DirectoryLookup:TimeoutSeconds";

    #endregion


    #region Storage

    /// <summary>Name of the connection string entry for the SQLite file.</summary>
    public const string DatabaseConnectionName = "IdeaBoard";

    /// <summary>Full key of the connection string, used in error messages.</summary>
    public const string DatabaseConnectionKey = "ConnectionStrings:IdeaBoard";

    /// <summary>Folder the Data Protection key ring is persisted to.</summary>
    public const string DataProtectionKeysPath = "DataProtection:KeysPath";

    #endregion


    #region Presentation

    /// <summary>Time zone every stored UTC instant is rendered in.</summary>
    public const string LocalizationTimeZoneId = "Localization:TimeZoneId";

    #endregion
}
