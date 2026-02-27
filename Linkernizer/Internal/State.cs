namespace Linkernizer.Internal;

/// <summary>
/// Represents the state passed to <see cref="string.Create{TState}"/>
/// for constructing the output string.
/// </summary>
internal readonly struct State(string input, List<Replacement> replacements, string defaultScheme)
{
  /// <summary>
  /// The complete input text.
  /// </summary>
  internal string Input { get; } = input;

  /// <summary>
  /// The list of replacements to be made.
  /// </summary>
  internal List<Replacement> Replacements { get; } = replacements;

  /// <summary>
  /// The default scheme to prepend for links without a scheme.
  /// </summary>
  internal string DefaultScheme { get; } = defaultScheme;
}
