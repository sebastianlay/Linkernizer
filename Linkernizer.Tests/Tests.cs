using Linkernizer.Tests.Data;
using Xunit;

namespace Linkernizer.Tests;

/// <summary>
/// This test suite is used to verify code changes and as a sort of documentation
/// of the expected behavior as the "right" behavior can be highly subjective.
/// </summary>
public class Tests
{
  /// <summary>
  /// Tests that invalid default scheme values are rejected during construction.
  /// </summary>
  /// <param name="scheme">The invalid scheme value to test.</param>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("https")]
  [InlineData("https:")]
  [InlineData("https:/")]
  public void InvalidDefaultSchemeTest(string? scheme)
  {
    Assert.Throws<ArgumentException>(() => new Linkernizer(options =>
    {
      options.DefaultScheme = scheme!;
    }));
  }

  /// <summary>
  /// Tests that internal host values containing a scheme are rejected during construction.
  /// </summary>
  /// <param name="host">The invalid host value to test.</param>
  [Theory]
  [InlineData("https://www.example.org")]
  [InlineData("http://example.org")]
  public void InvalidInternalHostWithSchemeTest(string host)
  {
    Assert.Throws<ArgumentException>(() => new Linkernizer(options =>
    {
      options.InternalHost = host;
    }));
  }

  /// <summary>
  /// Tests that trailing slashes on the internal host are gracefully trimmed during construction.
  /// </summary>
  /// <param name="host">The host value with trailing slashes to test.</param>
  [Theory]
  [InlineData("www.example.org/")]
  [InlineData("www.example.org///")]
  public void InternalHostTrailingSlashIsTrimmedTest(string host)
  {
    // Arrange
    var linkernizer = new Linkernizer(options =>
    {
      options.InternalHost = host;
      options.OpenExternalLinksInNewTab = true;
    });

    // Act — internal host link should NOT get target="_blank"
    var result = linkernizer.Linkernize("www.example.org");

    // Assert
    Assert.Equal("""<a href="https://www.example.org">www.example.org</a>""", result);
  }

  /// <summary>
  /// Tests the library with the default options.
  /// </summary>
  /// <param name="input">The value that should be supplied to the library.</param>
  /// <param name="expectedOutput">The value the actual output should match.</param>
  [Theory]
  [ClassData(typeof(DefaultOptionsData))]
  public void DefaultOptionsTest(string? input, string? expectedOutput)
  {
    // Arrange
    var linkernizer = new Linkernizer();

    // Act
    var actualOutput = linkernizer.Linkernize(input);

    // Assert
    Assert.Equal(expectedOutput, actualOutput);
  }

  /// <summary>
  /// Tests the library with custom options.
  /// </summary>
  /// <param name="input">The value that should be supplied to the library.</param>
  /// <param name="expectedOutput">The value the actual output should match.</param>
  [Theory]
  [ClassData(typeof(CustomOptionsData))]
  public void CustomOptionsTest(string? input, string? expectedOutput)
  {
    // Arrange
    var linkernizer = new Linkernizer(options =>
    {
      options.OpenExternalLinksInNewTab = true;
      options.InternalHost = "www.example.com";
      options.DefaultScheme = "http://";
    });

    // Act
    var actualOutput = linkernizer.Linkernize(input);

    // Assert
    Assert.Equal(expectedOutput, actualOutput);
  }
}
