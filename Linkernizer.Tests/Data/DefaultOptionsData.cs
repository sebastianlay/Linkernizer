namespace Linkernizer.Tests.Data;

/// <summary>
/// The data that is used to test the library with the default options.
/// </summary>
internal class DefaultOptionsData : TheoryData<string?, string?>
{
  /// <summary>
  /// The constructor will be implicitly called by the test framework.
  /// </summary>
  public DefaultOptionsData()
  {
    Add(null, null);
    Add(string.Empty, string.Empty);
    Add(" ", " ");
  }
}
