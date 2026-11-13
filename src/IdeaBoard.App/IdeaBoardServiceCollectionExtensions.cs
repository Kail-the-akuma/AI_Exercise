using System.Globalization;
using IdeaBoard.App.Configuration;
using IdeaBoard.App.Data;
using IdeaBoard.App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdeaBoard.App;

/// <summary>
/// Everything the application needs in the service container, registered once and shared
/// by every host.
///
/// No authentication scheme is registered here, by design. Each host composes its own, and
/// the published host is the only one that knows how to talk to Entra ID.
/// </summary>
public static class IdeaBoardServiceCollectionExtensions
{
    #region Registration

    /// <summary>
    /// Validates the configuration, then registers the data access, the domain services
    /// and the authorisation policy.
    /// </summary>
    public static IServiceCollection AddIdeaBoard(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        bool isDevelopment = environment.IsDevelopment();

        RefuseToStartOnBadConfiguration(configuration, isDevelopment, environment.ContentRootPath);

        AddPresentation(services, configuration);
        AddDirectoryLookup(services, configuration);
        AddStorage(services, configuration);
        AddDataProtection(services, configuration, isDevelopment);
        AddDomainServices(services);
        AddAuthorization(services);

        return services;
    }

    #endregion


    #region Configuration

    /// <summary>
    /// Stops startup when a value the application cannot work without is missing, naming
    /// the key. Failing here beats starting and failing on the first request, which turns
    /// a setup mistake into something that looks like a product defect.
    /// </summary>
    private static void RefuseToStartOnBadConfiguration(
        IConfiguration configuration,
        bool isDevelopment,
        string contentRootPath)
    {
        ConfigurationValidationResult validation = StartupConfigurationValidator.Validate(
            configuration,
            isDevelopment,
            contentRootPath);

        if (validation.IsValid)
        {
            return;
        }

        throw new InvalidOperationException(validation.BuildFailureMessage());
    }

    #endregion


    #region Presentation

    /// <summary>
    /// Registers the time zone stored instants are rendered in.
    ///
    /// It comes from configuration rather than from the server clock on purpose. Both agree
    /// on a Portuguese machine, which is exactly why relying on the clock would never be
    /// noticed as wrong until the application moved.
    /// </summary>
    private static void AddPresentation(IServiceCollection services, IConfiguration configuration)
    {
        string timeZoneId = configuration[ConfigurationKeys.LocalizationTimeZoneId]!;
        TimeZoneInfo displayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        services.AddSingleton(displayTimeZone);
    }

    #endregion


    #region Directory

    /// <summary>
    /// Registers how somebody's display name gets resolved.
    ///
    /// The consumer knows nothing about any of this: it asks for a name and gets one or
    /// does not. Which implementation answers is decided here, once, from configuration and
    /// from the platform, which is the same shape the local identity arrangement uses.
    /// </summary>
    private static void AddDirectoryLookup(IServiceCollection services, IConfiguration configuration)
    {
        DirectoryLookupOptions options = ReadDirectoryLookupOptions(configuration);

        services.AddSingleton(options);

        if (!options.Enabled)
        {
            // Switched off on purpose. This is what makes the registration path reachable
            // in an environment where the directory would otherwise always answer, so that
            // the first time it runs for real is not the day the directory breaks.
            services.AddSingleton<IDisplayNameResolver, UnavailableDisplayNameResolver>();
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            // The platform check is here rather than inside the resolver, which is what
            // lets the Windows only type exist in a library that targets plain net10.0.
            services.AddSingleton<IDisplayNameResolver, UnavailableDisplayNameResolver>();
            return;
        }

        services.AddSingleton<IDisplayNameResolver, DirectoryDisplayNameResolver>();
    }


    /// <summary>
    /// Reads the directory settings, falling back to the documented defaults. The values
    /// were already checked at startup, so anything present here parses.
    /// </summary>
    private static DirectoryLookupOptions ReadDirectoryLookupOptions(IConfiguration configuration)
    {
        DirectoryLookupOptions options = new DirectoryLookupOptions();

        string? enabled = configuration[ConfigurationKeys.DirectoryLookupEnabled];

        if (!string.IsNullOrWhiteSpace(enabled))
        {
            options.Enabled = bool.Parse(enabled);
        }

        string? timeoutSeconds = configuration[ConfigurationKeys.DirectoryLookupTimeoutSeconds];

        if (!string.IsNullOrWhiteSpace(timeoutSeconds))
        {
            options.TimeoutSeconds = int.Parse(timeoutSeconds, CultureInfo.InvariantCulture);
        }

        return options;
    }

    #endregion


    #region Storage

    /// <summary>Registers the database context against the configured SQLite file.</summary>
    private static void AddStorage(IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(ConfigurationKeys.DatabaseConnectionName)!;

        // A factory as well as the scoped context. Provisioning the local user row has to
        // commit independently of whatever business write shares its request, so it opens
        // its own short lived context instead of the shared one.
        services.AddDbContextFactory<IdeaBoardDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        services.AddScoped<IdeaBoardDbContext>(serviceProvider =>
        {
            IDbContextFactory<IdeaBoardDbContext> factory =
                serviceProvider.GetRequiredService<IDbContextFactory<IdeaBoardDbContext>>();

            return factory.CreateDbContext();
        });
    }


    /// <summary>
    /// Persists the key ring outside development. Without this, every application pool
    /// recycle invalidates the cookies and antiforgery tokens issued before it, and people
    /// are thrown out of their session at what look like random times.
    /// </summary>
    private static void AddDataProtection(
        IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        if (isDevelopment)
        {
            return;
        }

        string keysPath = configuration[ConfigurationKeys.DataProtectionKeysPath]!;

        services
            .AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetApplicationName("IdeaBoard");
    }

    #endregion


    #region Services

    /// <summary>Registers the domain services, all request-scoped.</summary>
    private static void AddDomainServices(IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();

        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IIdeaService, IdeaService>();
        services.AddScoped<IVoteService, VoteService>();
    }


    /// <summary>
    /// Requires an authenticated person everywhere, as a fallback policy rather than as an
    /// attribute per page. With a fallback, a new page is born protected by default. With
    /// attributes, a new page is born open and only becomes protected if somebody
    /// remembers.
    /// </summary>
    private static void AddAuthorization(IServiceCollection services)
    {
        AuthorizationPolicy fallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(fallbackPolicy);
    }

    #endregion
}
