using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace IdeaBoard.LocalIdentity;

/// <summary>
/// Registers the local authentication scheme. Only hosts that are never published call
/// this: the local host used to see the application, and the end to end suite.
/// </summary>
public static class LocalAuthenticationExtensions
{
    #region Registration

    /// <summary>
    /// Adds the local scheme as the default one. The caller supplies the identity source,
    /// which is the only part that differs between the hosts that use this.
    /// </summary>
    public static IServiceCollection AddLocalIdentity<TSource>(this IServiceCollection services)
        where TSource : class, ILocalIdentitySource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ILocalIdentitySource, TSource>();

        services
            .AddAuthentication(LocalAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, LocalAuthenticationHandler>(
                LocalAuthenticationHandler.SchemeName,
                null);

        return services;
    }

    #endregion
}
