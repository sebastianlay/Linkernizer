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
  /// Tests that a null internal host is rejected during construction.
  /// </summary>
  [Fact]
  public void InvalidInternalHostNullTest()
  {
    Assert.Throws<ArgumentException>(() => new Linkernizer(options =>
    {
      options.InternalHost = null!;
    }));
  }

  /// <summary>
  /// Tests that the options can no longer be changed after construction,
  /// as this would bypass the validation done during construction.
  /// </summary>
  [Fact]
  public void OptionsCannotBeChangedAfterConstructionTest()
  {
    // Arrange
    LinkernizerOptions? captured = null;
    _ = new Linkernizer(options => captured = options);

    // Assert
    Assert.NotNull(captured);
    Assert.Throws<InvalidOperationException>(() => { captured.DefaultScheme = "ftp://"; });
    Assert.Throws<InvalidOperationException>(() => { captured.InternalHost = "www.example.org"; });
    Assert.Throws<InvalidOperationException>(() => { captured.OpenExternalLinksInNewTab = true; });
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
  /// Tests that hosts that are IPv6 addresses are correctly compared with the internal host.
  /// </summary>
  [Fact]
  public void InternalHostWithIPv6AddressTest()
  {
    // Arrange
    var linkernizer = new Linkernizer(options =>
    {
      options.InternalHost = "[2001:db8::1]";
      options.OpenExternalLinksInNewTab = true;
    });

    // Act — the bracketed host should match the internal host despite the port
    var result = linkernizer.Linkernize("https://[2001:db8::1]:8080/example");

    // Assert
    Assert.Equal("""<a href="https://[2001:db8::1]:8080/example">https://[2001:db8::1]:8080/example</a>""", result);
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

  /// <summary>
  /// Tests the span overload of the library with the default options.
  /// </summary>
  /// <param name="input">The value that should be supplied to the library.</param>
  /// <param name="expectedOutput">The value the actual output should match.</param>
  [Theory]
  [ClassData(typeof(DefaultOptionsData))]
  public void DefaultOptionsSpanTest(string? input, string? expectedOutput)
  {
    // Arrange
    var linkernizer = new Linkernizer();

    // Act
    var actualOutput = linkernizer.Linkernize(input.AsSpan());

    // Assert — a null input is an empty span and therefore yields an empty string
    Assert.Equal(expectedOutput ?? string.Empty, actualOutput);
  }

  /// <summary>
  /// Tests the span overload of the library with custom options.
  /// </summary>
  /// <param name="input">The value that should be supplied to the library.</param>
  /// <param name="expectedOutput">The value the actual output should match.</param>
  [Theory]
  [ClassData(typeof(CustomOptionsData))]
  public void CustomOptionsSpanTest(string? input, string? expectedOutput)
  {
    // Arrange
    var linkernizer = new Linkernizer(options =>
    {
      options.OpenExternalLinksInNewTab = true;
      options.InternalHost = "www.example.com";
      options.DefaultScheme = "http://";
    });

    // Act
    var actualOutput = linkernizer.Linkernize(input.AsSpan());

    // Assert — a null input is an empty span and therefore yields an empty string
    Assert.Equal(expectedOutput ?? string.Empty, actualOutput);
  }
}
