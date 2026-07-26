using System.Collections.Frozen;

namespace Linkernizer.Internal;

/// <summary>
/// Provides the rules for URI schemes that are shared between the parsing of the input
/// and the validation of the options. This is the single source of truth for both, so
/// that a scheme which would be rejected in the input can also not be configured as
/// the default scheme that is prepended to links without one.
/// </summary>
internal static class Scheme
{
  /// <summary>The delimiter that separates the scheme from the rest of the link.</summary>
  internal const string Delimiter = "://";

  // Schemes that could execute scripts when the link is clicked. The span alternate
  // lookup allows checking a candidate's scheme without allocating it as a string.
  private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> DangerousSchemes = FrozenSet.Create(
    StringComparer.OrdinalIgnoreCase, "javascript", "vbscript", "data")
    .GetAlternateLookup<ReadOnlySpan<char>>();

  /// <summary>
  /// Determines if the given scheme only consists of the characters that are allowed
  /// in a scheme according to RFC 3986. Everything else is rejected because browsers
  /// remove or decode some characters (such as control characters or HTML entities)
  /// before they parse the URL, which would otherwise allow sneaking a dangerous
  /// scheme past the check for them (as in "&amp;#106;avascript://...").
  /// </summary>
  /// <param name="scheme">The part of the link before the scheme delimiter.</param>
  /// <returns>True if the given scheme is syntactically valid.</returns>
  internal static bool IsValid(ReadOnlySpan<char> scheme)
  {
    // A scheme always has to begin with a letter.
    if (scheme is [] || !char.IsAsciiLetter(scheme[0]))
      return false;

    // All following characters may also be digits or one of a few special characters.
    foreach (var character in scheme[1..])
    {
      if (!char.IsAsciiLetterOrDigit(character) && character is not ('+' or '-' or '.'))
        return false;
    }

    return true;
  }

  /// <summary>
  /// Determines if the given scheme could execute scripts when a link with it is clicked.
  /// </summary>
  /// <param name="scheme">The part of the link before the scheme delimiter.</param>
  /// <returns>True if the given scheme could execute scripts.</returns>
  internal static bool IsDangerous(ReadOnlySpan<char> scheme) => DangerousSchemes.Contains(scheme);

  /// <summary>
  /// Returns the link without the scheme at the beginning in case there was any.
  /// </summary>
  /// <param name="link">The assumed link with or without scheme.</param>
  /// <param name="withScheme">True if the link was determined to already have the scheme at the beginning.</param>
  /// <returns>The link without the scheme.</returns>
  internal static ReadOnlySpan<char> Strip(ReadOnlySpan<char> link, bool withScheme)
  {
    // Return the link immediately in case we know that it does not contain a scheme.
    if (!withScheme)
      return link;

    // Otherwise find the end of the scheme and strip it from the link.
    var schemeEnd = link.IndexOf(Delimiter);
    var hostStart = schemeEnd >= 0 ? schemeEnd + Delimiter.Length : 0;

    return link[hostStart..];
  }

  /// <summary>
  /// Tries to find the scheme at the beginning of the given candidate. A scheme is only
  /// assumed when the delimiter is found with at least one character before it for the
  /// scheme and at least one character after it for the host. The scheme is merely located
  /// and not validated here, so callers have to pass it to <see cref="IsValid"/> and
  /// <see cref="IsDangerous"/> before they turn the candidate into a link.
  /// </summary>
  /// <param name="candidate">The part of the input that potentially needs to be replaced.</param>
  /// <param name="scheme">The part of the candidate before the delimiter, or an empty span when there is none.</param>
  /// <returns>True if the given candidate begins with a scheme.</returns>
  internal static bool TryGet(ReadOnlySpan<char> candidate, out ReadOnlySpan<char> scheme)
  {
    scheme = default;

    var delimiterIndex = candidate.IndexOf(Delimiter);
    if (delimiterIndex < 1 || delimiterIndex + Delimiter.Length >= candidate.Length)
      return false;

    scheme = candidate[..delimiterIndex];
    return true;
  }
}
