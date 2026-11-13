using System.Globalization;
using IdeaBoard.App.Configuration;
using IdeaBoard.App.Data;
using IdeaBoard.App.Pipeline;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IdeaBoard.App;

/// <summary>
/// The request pipeline, composed once and shared by every host.
///
/// Each host still calls AddRazorPages and MapRazorPages for itself, because a Razor class
/// library does not bring those with it.
/// </summary>
public static class IdeaBoardApplicationBuilderExtensions
{
    #region Pipeline

    /// <summary>
    /// Verifies the database is provisioned, then composes the pipeline in the order the
    /// application needs it.
    /// </summary>
    public static WebApplication UseIdeaBoard(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        bool isDevelopment = app.Environment.IsDevelopment();

        VerifyDatabaseIsProvisioned(app);
        WarnAboutDevelopmentKeyRing(app, isDevelopment);

        if (!isDevelopment)
        {
            app.UseExceptionHandler("/Erro");
        }

        // There is deliberately no UseHsts and no UseHttpsRedirection. HTTPS and the
        // redirection of HTTP belong to the IIS site, and HSTS stays off until the
        // self-issued certificate is replaced, because together they make the site
        // unreachable with no way for anyone to proceed past the warning.

        app.UseStatusCodePagesWithReExecute("/Estado/{0}");

        app.UseStaticFiles();
        app.UseRouting();

        app.UseRequestLocalization(BuildLocalizationOptions());

        app.UseAuthentication();
        app.UseAuthorization();

        // After authorisation, because it has an opinion only about people who are signed
        // in, and before the endpoints, because its whole job is to stop a request reaching
        // one. A person whose name is not known yet can reach the page that asks for it and
        // nothing else.
        app.UseMiddleware<DisplayNameGateMiddleware>();

        return app;
    }

    #endregion


    #region Startup checks

    /// <summary>
    /// Refuses to start when the database is not in write-ahead logging mode. The check
    /// verifies and never corrects, because a database that reached this point without the
    /// provisioning step is a database whose other provisioning steps are also in doubt.
    /// </summary>
    private static void VerifyDatabaseIsProvisioned(WebApplication app)
    {
        string connectionString =
            app.Configuration.GetConnectionString(ConfigurationKeys.DatabaseConnectionName)!;

        SqliteJournalModeVerifier.EnsureWriteAheadLogging(connectionString);
    }


    /// <summary>Says out loud that sessions do not survive a restart in development.</summary>
    private static void WarnAboutDevelopmentKeyRing(WebApplication app, bool isDevelopment)
    {
        if (!isDevelopment)
        {
            return;
        }

        app.Logger.LogWarning(
            "Data Protection keys are not persisted in development. Sessions end when the process " +
            "restarts, which is expected here and must never be the case on a server.");
    }

    #endregion


    #region Localization

    /// <summary>
    /// Fixes the culture to European Portuguese rather than negotiating it from the
    /// browser, so that dates and numbers read the same for everyone looking at the same
    /// page.
    /// </summary>
    private static RequestLocalizationOptions BuildLocalizationOptions()
    {
        CultureInfo portugueseCulture = new CultureInfo("pt-PT");
        List<CultureInfo> supportedCultures = [portugueseCulture];

        RequestLocalizationOptions localizationOptions = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(portugueseCulture),
            SupportedCultures = supportedCultures,
            SupportedUICultures = supportedCultures,
        };

        localizationOptions.RequestCultureProviders.Clear();

        return localizationOptions;
    }

    #endregion
}
