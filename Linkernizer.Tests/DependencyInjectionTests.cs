using Linkernizer.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Linkernizer.Tests;

/// <summary>
/// Tests that the service collection extension registers Linkernizer as expected.
/// </summary>
public class DependencyInjectionTests
{
  /// <summary>
  /// Tests that the registered service can be resolved from the container.
  /// </summary>
  [Fact]
  public void AddLinkernizerRegistersResolvableServiceTest()
  {
    // Arrange
    var provider = new ServiceCollection()
      .AddLinkernizer()
      .BuildServiceProvider();

    // Act
    var linkernizer = provider.GetService<ILinkernizer>();

    // Assert
    Assert.NotNull(linkernizer);
  }

  /// <summary>
  /// Tests that the service is registered as a singleton so that a single
  /// immutable instance is shared across the whole application.
  /// </summary>
  [Fact]
  public void AddLinkernizerRegistersSingletonTest()
  {
    // Arrange
    var provider = new ServiceCollection()
      .AddLinkernizer()
      .BuildServiceProvider();

    // Act
    var first = provider.GetRequiredService<ILinkernizer>();
    var second = provider.GetRequiredService<ILinkernizer>();

    // Assert
    Assert.Same(first, second);
  }

  /// <summary>
  /// Tests that the given configuration action is applied to the registered instance.
  /// </summary>
  [Fact]
  public void AddLinkernizerAppliesConfigurationTest()
  {
    // Arrange
    var provider = new ServiceCollection()
      .AddLinkernizer(options => options.DefaultScheme = "http://")
      .BuildServiceProvider();

    // Act
    var linkernizer = provider.GetRequiredService<ILinkernizer>();

    // Assert
    Assert.Equal("""<a href="http://www.example.org">www.example.org</a>""",
      linkernizer.Linkernize("www.example.org"));
  }

  /// <summary>
  /// Tests that the options are bound from a configuration source when one is given.
  /// </summary>
  [Fact]
  public void AddLinkernizerBindsFromConfigurationTest()
  {
    // Arrange
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["Linkernizer:DefaultScheme"] = "http://"
      })
      .Build();

    var provider = new ServiceCollection()
      .AddLinkernizer(configuration.GetSection("Linkernizer"))
      .BuildServiceProvider();

    // Act
    var linkernizer = provider.GetRequiredService<ILinkernizer>();

    // Assert
    Assert.Equal("""<a href="http://www.example.org">www.example.org</a>""",
      linkernizer.Linkernize("www.example.org"));
  }

  /// <summary>
  /// Tests that all four options are bound from the configuration section.
  /// </summary>
  [Fact]
  public void AddLinkernizerBindsAllOptionsFromConfigurationTest()
  {
    // Arrange
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["Linkernizer:DefaultScheme"] = "http://",
        ["Linkernizer:InternalHost"] = "www.example.com",
        ["Linkernizer:OpenExternalLinksInNewTab"] = "true",
        ["Linkernizer:NoReferrerOnExternalLinks"] = "true"
      })
      .Build();

    var provider = new ServiceCollection()
      .AddLinkernizer(configuration.GetSection("Linkernizer"))
      .BuildServiceProvider();

    // Act
    var options = provider.GetRequiredService<IOptions<LinkernizerOptions>>().Value;

    // Assert
    Assert.Equal("http://", options.DefaultScheme);
    Assert.Equal("www.example.com", options.InternalHost);
    Assert.True(options.OpenExternalLinksInNewTab);
    Assert.True(options.NoReferrerOnExternalLinks);
  }

  /// <summary>
  /// Tests that the same service collection is returned so that calls can be chained.
  /// </summary>
  [Fact]
  public void AddLinkernizerReturnsSameServiceCollectionTest()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act
    var result = services.AddLinkernizer();

    // Assert
    Assert.Same(services, result);
  }

  /// <summary>
  /// Tests that an existing registration is not overridden by the extension.
  /// </summary>
  [Fact]
  public void AddLinkernizerDoesNotOverrideExistingRegistrationTest()
  {
    // Arrange
    var custom = new Linkernizer(options => options.DefaultScheme = "ftp://");
    var provider = new ServiceCollection()
      .AddSingleton<ILinkernizer>(custom)
      .AddLinkernizer()
      .BuildServiceProvider();

    // Act
    var resolved = provider.GetRequiredService<ILinkernizer>();

    // Assert
    Assert.Same(custom, resolved);
  }

  /// <summary>
  /// Tests that invalid options fail the options validation when the service is resolved,
  /// as the instance is created lazily by the container.
  /// </summary>
  [Fact]
  public void AddLinkernizerWithInvalidOptionsThrowsOnResolveTest()
  {
    // Arrange
    var provider = new ServiceCollection()
      .AddLinkernizer(options => options.DefaultScheme = "invalid")
      .BuildServiceProvider();

    // Act & Assert
    Assert.Throws<OptionsValidationException>(provider.GetRequiredService<ILinkernizer>);
  }
}
