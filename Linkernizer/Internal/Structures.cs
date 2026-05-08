namespace Linkernizer.Internal;

/// <summary>
/// Represents the state passed to <see cref="string.Create{TState}"/> for constructing the output string.
/// </summary>
/// <param name="Input">The complete input text.</param>
/// <param name="Replacements">The list of replacements to be made.</param>
/// <param name="DefaultScheme">The default scheme to prepend for links without a scheme.</param>
internal readonly record struct State(string Input, List<Replacement> Replacements, string DefaultScheme);

/// <summary>
/// Represents a part of the original input that will be replaced.
/// </summary>
/// <param name="Offset">The offset to the start of the original input.</param>
/// <param name="Length">The length of the match in the original input.</param>
/// <param name="Type">The type of the match that determines how the match will be replaced in the output.</param>
internal readonly record struct Replacement(int Offset, int Length, ReplacementType Type);

/// <summary>
/// Represents the type of match.
/// </summary>
internal enum ReplacementType
{
  InternalWithScheme,
  InternalWithoutScheme,
  ExternalWithScheme,
  ExternalWithoutScheme,
  EmailWithScheme,
  EmailWithoutScheme
}
