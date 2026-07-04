using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Linkernizer.DependencyInjection;

/// <summary>
/// Extension methods for registering Linkernizer with an <see cref="IServiceCollection"/>.
/// </summary>
public static class LinkernizerServiceCollectionExtensions
{
  /// <param name="services">The service collection to add the registration to.</param>
  extension(IServiceCollection services)
  {
    /// <summary>
    ///   <para>
    ///     Registers <see cref="ILinkernizer"/> as a singleton so that a single immutable,
    ///     thread-safe instance is shared across the application. The registration is only
    ///     added when no <see cref="ILinkernizer"/> has been registered yet, so an existing
    ///     registration is left untouched.
    ///   </para>
    ///   <example>
    ///     The options can be configured just like on the constructor:
    ///     <code>
    ///       services.AddLinkernizer(options => {
    ///         options.OpenExternalLinksInNewTab = true;
    ///       });
    ///     </code>
    ///   </example>
    /// </summary>
    /// <param name="configure">An optional action to configure the options.</param>
    /// <returns>The same service collection so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public IServiceCollection AddLinkernizer(Action<LinkernizerOptions>? configure = null)
    {
      ArgumentNullException.ThrowIfNull(services);

      var builder = services.AddOptions<LinkernizerOptions>();
      if (configure is not null)
        builder.Configure(configure);

      return services.AddLinkernizerCore(builder);
    }

    /// <summary>
    ///   <para>
    ///     Registers <see cref="ILinkernizer"/> as a singleton and binds its options from the
    ///     given configuration, so that the settings can be provided through any configuration
    ///     source (such as appsettings.json or environment variables).
    ///   </para>
    ///   <example>
    ///     The consumer passes the section that the options are bound from:
    ///     <code>
    ///       services.AddLinkernizer(builder.Configuration.GetSection("Linkernizer"));
    ///     </code>
    ///   </example>
    /// </summary>
    /// <param name="configuration">The configuration section to bind the options from.</param>
    /// <returns>The same service collection so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configuration"/> is null.</exception>
    public IServiceCollection AddLinkernizer(IConfiguration configuration)
    {
      ArgumentNullException.ThrowIfNull(services);
      ArgumentNullException.ThrowIfNull(configuration);

      var builder = services.AddOptions<LinkernizerOptions>().Bind(configuration);

      return services.AddLinkernizerCore(builder);
    }

    /// <summary>
    /// Registers the shared validation and the singleton for the given options builder.
    /// The validation delegates to the core library so that both agree on which options
    /// are valid, and it runs eagerly on application start as well as on first resolve.
    /// </summary>
    /// <param name="builder">The options builder that the configuration has been applied to.</param>
    /// <returns>The same service collection so that calls can be chained.</returns>
    private IServiceCollection AddLinkernizerCore(OptionsBuilder<LinkernizerOptions> builder)
    {
      services.TryAddEnumerable(
        ServiceDescriptor.Singleton<IValidateOptions<LinkernizerOptions>, LinkernizerOptionsValidator>());

      builder.ValidateOnStart();

      services.TryAddSingleton<ILinkernizer>(provider =>
        new Linkernizer(provider.GetRequiredService<IOptions<LinkernizerOptions>>().Value));

      return services;
    }
  }
}
