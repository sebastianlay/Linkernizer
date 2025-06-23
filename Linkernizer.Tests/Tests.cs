using Linkernizer.Tests.Data;
using Xunit;

namespace Linkernizer.Tests;

/// <summary>
/// This test suite is used to verify code changes and as a sort of documentation
/// of the expected behaviour as the "right" behaviour can be highly subjective.
/// </summary>
public class Tests
{
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
