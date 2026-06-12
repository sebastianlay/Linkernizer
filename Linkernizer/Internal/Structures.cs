namespace Linkernizer.Internal;

/// <summary>
/// Represents the state passed to <see cref="string.Create{TState}"/> for constructing the output string.
/// This is a ref struct so that the input can be carried as a span without materializing it as a string.
/// </summary>
/// <param name="input">The complete input text.</param>
/// <param name="replacements">The replacements to be made.</param>
/// <param name="defaultScheme">The default scheme to prepend for links without a scheme.</param>
internal readonly ref struct State(ReadOnlySpan<char> input, ReadOnlySpan<Replacement> replacements, string defaultScheme)
{
  /// <summary>The complete input text.</summary>
  public ReadOnlySpan<char> Input { get; } = input;

  /// <summary>The replacements to be made.</summary>
  public ReadOnlySpan<Replacement> Replacements { get; } = replacements;

  /// <summary>The default scheme to prepend for links without a scheme.</summary>
  public string DefaultScheme { get; } = defaultScheme;
}

/// <summary>
/// Represents a part of the original input that will be replaced.
/// </summary>
/// <param name="Offset">The offset to the start of the original input.</param>
/// <param name="Length">The length of the match in the original input.</param>
/// <param name="Type">The type of the match that determines how the match will be replaced in the output.</param>
internal readonly record struct Replacement(int Offset, ushort Length, ReplacementType Type);

/// <summary>
/// Represents the type of match.
/// </summary>
internal enum ReplacementType : byte
{
  InternalWithScheme,
  InternalWithoutScheme,
  ExternalWithScheme,
  ExternalWithoutScheme,
  EmailWithScheme,
  EmailWithoutScheme
}
