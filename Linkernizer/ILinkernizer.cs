using System.Diagnostics.CodeAnalysis;

namespace Linkernizer;

/// <summary>
///   <para>
///     This library provides functionality for detecting many different forms of links
///     in text and wrapping them with HTML hyperlink markup for displaying it on a website.
///   </para>
///   <para>
///     This interface is provided so that the library can be used via dependency injection.
///   </para>
/// </summary>
public interface ILinkernizer
{
  /// <inheritdoc cref="Linkernizer.Linkernize(string?)" />
  [return: NotNullIfNotNull(nameof(input))]
  string? Linkernize(string? input);

  /// <inheritdoc cref="Linkernize(ReadOnlySpan{char})" />
  string Linkernize(ReadOnlySpan<char> input);
}
