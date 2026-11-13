namespace IdeaBoard.App.Configuration;

/// <summary>
/// Configuration keys the application reads. They are named here once so that the startup
/// validator, the wiring and the error messages cannot drift apart.
/// </summary>
public static class ConfigurationKeys
{
    #region Authentication

    /// <summary>Section bound by Microsoft.Identity.Web.</summary>
    public const string AzureAdSection = "AzureAd";

    /// <summary>Authority the sign-in request is sent to.</summary>
    public const string AzureAdInstance = "AzureAd:Instance";

    /// <summary>Single tenant the application accepts identities from.</summary>
    public const string AzureAdTenantId = "AzureAd:TenantId";

    /// <summary>Application registration identifier.</summary>
    public const string AzureAdClientId = "AzureAd:ClientId";

    /// <summary>Path the identity provider returns the token to.</summary>
    public const string AzureAdCallbackPath = "AzureAd:CallbackPath";

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
