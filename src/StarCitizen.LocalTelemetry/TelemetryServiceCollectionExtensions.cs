using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StarCitizen.LocalTelemetry;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers <see cref="StarCitizenTelemetryClient"/> with a dependency-injection container.</summary>
public static class TelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="StarCitizenTelemetryClient"/>, also resolvable as <see cref="ITelemetryClient"/>.
    /// The client logs to <c>ILogger&lt;StarCitizenTelemetryClient&gt;</c> when logging is registered, and the
    /// container disposes it. Existing registrations are kept.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Adjusts the client settings, for example the address.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddStarCitizenTelemetry(
        this IServiceCollection services,
        Action<StarCitizenTelemetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(provider =>
        {
            var options = new StarCitizenTelemetryOptions();
            configure?.Invoke(options);
            return new StarCitizenTelemetryClient(options, provider.GetService<ILogger<StarCitizenTelemetryClient>>());
        });
        services.TryAddSingleton<ITelemetryClient>(provider => provider.GetRequiredService<StarCitizenTelemetryClient>());
        return services;
    }
}