using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Linkernizer.Internal;

namespace Linkernizer;

/// <summary>
///   <para>
///     This library provides functionality for detecting many different forms of links
///     in text and wrapping them with HTML hyperlink markup for displaying it on a website.
///   </para>
/// </summary>
public class Linkernizer : ILinkernizer
{
  private const string SchemeDelimiter = "://";
  private const string DefaultSubdomain = "www.";
  private const string MailToProtocol = "mailto:";

  private const string OpeningTagBegin = "<a href=\"";
  private const string OpeningTagEndInternal = "\">";
  private const string OpeningTagEndExternal = "\" target=\"_blank\">";
  private const string ClosingTag = "</a>";

  private static readonly SearchValues<string> Indicators = SearchValues.Create([SchemeDelimiter, DefaultSubdomain, "@"],
    StringComparison.OrdinalIgnoreCase
  );
  private static readonly SearchValues<char> TrimCharacters = SearchValues.Create('.', ':', '?', '!', ',', ';');
  private static readonly SearchValues<char> HostDelimiters = SearchValues.Create('/', ':', '?', '#');
  private static readonly SearchValues<char> Whitespaces = SearchValues.Create(
    '\u0020', '\u00A0', '\u1680', '\u2000', '\u2001',
    '\u2002', '\u2003', '\u2004', '\u2005', '\u2006',
    '\u2007', '\u2008', '\u2009', '\u200A', '\u202F',
    '\u205F', '\u3000', '\u2028', '\u2029', '\u0009',
    '\u000A', '\u000B', '\u000C', '\u000D', '\u0085'
  );

  private readonly LinkernizerOptions _options = new();

  /// <summary>
  ///  <para>
  ///     Initializes a new instance of the library with the given options.
  ///     Sane default options will be assumed if no options are given.
  ///   </para>
  ///   <para>
  ///     It is recommended to only create one instance and reuse it across the application.
  ///   </para>
  /// </summary>
  /// <param name="action">
  ///   <example>
  ///     The options can be given as follows:
  ///     <code>
  ///       var linkernizer = new Linkernizer(options => {
  ///         options.DefaultScheme = "https://";
  ///       });
  ///     </code>
  ///   </example>
  /// </param>
  public Linkernizer(Action<LinkernizerOptions>? action = null)
  {
    action?.Invoke(_options);

    if (string.IsNullOrWhiteSpace(_options.DefaultScheme))
      throw new ArgumentException("DefaultScheme must not be null or empty.", nameof(action));

    if (!_options.DefaultScheme.EndsWith(SchemeDelimiter, StringComparison.Ordinal))
      throw new ArgumentException("DefaultScheme must end with \"://\".", nameof(action));

    if (_options.InternalHost.Contains(SchemeDelimiter, StringComparison.Ordinal))
      throw new ArgumentException("InternalHost must not contain a scheme.", nameof(action));

    _options.InternalHost = _options.InternalHost.TrimEnd('/');
  }

  /// <summary>
  ///   <para>
  ///     Wraps links in the given input with HTML hyperlink markup.
  ///   </para>
  ///   <example>
  ///     Will turn the input <c>https://www.example.org</c> into <c><![CDATA[<a href="https://www.example.org">www.example.org</a>]]></c>.
  ///   </example>
  /// </summary>
  /// <param name="input">The input should not already contain HTML.</param>
  /// <returns>The output containing unencoded HTML markup.</returns>
  [return: NotNullIfNotNull(nameof(input))]
  public string? Linkernize(string? input)
  {
    if (input is null)
      return input;

    // Preliminary check if there are possible matches at all.
    if (!input.AsSpan().ContainsAny(Indicators))
      return input;

    return LinkernizeInternal(input);
  }

  /// <summary>
  ///   <para>
  ///     Wraps links in the given input with HTML hyperlink markup.
  ///   </para>
  ///   <example>
  ///     Will turn the input <c>https://www.example.org</c> into <c><![CDATA[<a href="https://www.example.org">www.example.org</a>]]></c>.
  ///   </example>
  /// </summary>
  /// <param name="input">The input should not already contain HTML.</param>
  /// <returns>The output containing unencoded HTML markup.</returns>
  public string Linkernize(ReadOnlySpan<char> input)
  {
    if (input is [])
      return string.Empty;

    // Preliminary check if there are possible matches at all.
    if (!input.ContainsAny(Indicators))
      return input.ToString();

    return LinkernizeInternal(input.ToString());
  }

  /// <summary>
  /// Finds the parts that need to be replaced in the given input
  /// and constructs the output with the replaced values.
  /// </summary>
  /// <param name="input">The complete input.</param>
  /// <returns>A newly constructed string with relevant replacements done.</returns>
  private string LinkernizeInternal(string input)
  {
    var replacements = GetReplacements(input);
    if (replacements.Count == 0)
      return input;

    var defaultScheme = _options.DefaultScheme;
    var replacementsSpan = CollectionsMarshal.AsSpan(replacements);
    var outputLength = GetOutputLength(input.Length, replacementsSpan, defaultScheme.Length);
    var state = new State(input, replacements, defaultScheme);

    return string.Create(outputLength, state, WriteOutput);
  }

  /// <summary>
  /// Computes the total length of the output string based on
  /// the input length and the markup overhead of each replacement.
  /// </summary>
  /// <param name="length">The length of the complete input.</param>
  /// <param name="replacements">The list of replacements to be made.</param>
  /// <param name="defaultSchemeLength">The length of the default scheme.</param>
  /// <returns>The exact length of the output string.</returns>
  private static int GetOutputLength(int length, ReadOnlySpan<Replacement> replacements, int defaultSchemeLength)
  {
    foreach (var replacement in replacements)
    {
      length += replacement.Length + replacement.Type switch
      {
        ReplacementType.InternalWithScheme or ReplacementType.EmailWithScheme
          => OpeningTagBegin.Length + OpeningTagEndInternal.Length + ClosingTag.Length,

        ReplacementType.InternalWithoutScheme
          => OpeningTagBegin.Length + OpeningTagEndInternal.Length + ClosingTag.Length + defaultSchemeLength,

        ReplacementType.ExternalWithScheme
          => OpeningTagBegin.Length + OpeningTagEndExternal.Length + ClosingTag.Length,

        ReplacementType.ExternalWithoutScheme
          => OpeningTagBegin.Length + OpeningTagEndExternal.Length + ClosingTag.Length + defaultSchemeLength,

        ReplacementType.EmailWithoutScheme
          => OpeningTagBegin.Length + OpeningTagEndInternal.Length + ClosingTag.Length + MailToProtocol.Length,

        _ => 0
      };
    }

    return length;
  }

  /// <summary>
  /// Writes the complete output into the given span by copying
  /// non-replaced segments and writing HTML markup for each replacement.
  /// </summary>
  /// <param name="output">The target span to write into.</param>
  /// <param name="state">The input, replacements, and default scheme.</param>
  private static void WriteOutput(Span<char> output, State state)
  {
    var input = state.Input.AsSpan();
    var replacements = CollectionsMarshal.AsSpan(state.Replacements);

    var position = 0;
    var inputIndex = 0;

    foreach (var replacement in replacements)
    {
      // Copy the part of the original input that leads up
      // to the beginning of the current replacement.
      if (replacement.Offset > inputIndex)
      {
        var segment = input[inputIndex..replacement.Offset];
        segment.CopyTo(output[position..]);
        position += segment.Length;
        inputIndex = replacement.Offset;
      }

      // Write the new value of the current replacement.
      var inputSlice = input.Slice(replacement.Offset, replacement.Length);
      WriteReplacement(output, ref position, inputSlice, replacement.Type, state.DefaultScheme);
      inputIndex += replacement.Length;
    }

    // Check if there is anything remaining.
    if (inputIndex >= input.Length)
      return;

    // Copy the remaining part of the original input.
    var remaining = input[inputIndex..];
    remaining.CopyTo(output[position..]);
  }

  /// <summary>
  /// Finds all parts of the given input that need to be replaced
  /// and returns their location in the input and their type.
  /// </summary>
  /// <param name="input">The complete input.</param>
  /// <returns>A list of parts that need to be replaced.</returns>
  private List<Replacement> GetReplacements(ReadOnlySpan<char> input)
  {
    var result = new List<Replacement>();

    // Split the input on whitespaces as they are usually
    // the boundary of a part that should be linked.
    // We assume spaces in URLs to be URL-encoded.
    foreach (var range in input.SplitAny(Whitespaces))
    {
      // Ignore all 'words' that cannot contain a link.
      if (!input[range].ContainsAny(Indicators))
        continue;

      // Trim extra characters at the beginning and end
      // of the word. This is not strictly standards-compliant,
      // but often the desired behavior in the *real* world.
      var (offset, length) = TrimExtraCharacters(input, range);

      // Check if the word is actually a link and, if so, which type.
      if (TryGetReplacementType(input.Slice(offset, length), out var type))
        result.Add(new Replacement(offset, length, type));
    }

    return result;
  }

  /// <summary>
  /// Finds characters that should be trimmed at the start and end
  /// of the given range in the given input and returns the updated range.
  /// </summary>
  /// <param name="input">The complete input.</param>
  /// <param name="range">The range of a possible replacement in the input.</param>
  /// <returns>The trimmed range of a possible replacement in the input.</returns>
  private static (int Offset, int Length) TrimExtraCharacters(ReadOnlySpan<char> input, Range range)
  {
    var (offset, length) = range.GetOffsetAndLength(input.Length);

    // Trim some trailing characters (even though they could technically be part of the URL).
    while (length > 0 && TrimCharacters.Contains(input[offset + length - 1]))
      length--;

    // Trim parentheses and brackets in pairs.
    while (length > 1 && AreParenthesesOrBrackets(input[offset], input[offset + length - 1]))
    {
      offset++;
      length -= 2;
    }

    return (offset, length);
  }

  /// <summary>
  /// Determines if the given characters are a matching pair
  /// of parentheses or brackets.
  /// </summary>
  /// <param name="firstChar">The first character of the candidate.</param>
  /// <param name="lastChar">The last character of the candidate.</param>
  /// <returns>True if the two characters are a matching pair.</returns>
  private static bool AreParenthesesOrBrackets(char firstChar, char lastChar)
  {
    return (firstChar == '(' && lastChar == ')')
        || (firstChar == '[' && lastChar == ']')
        || (firstChar == '{' && lastChar == '}')
        || (firstChar == '<' && lastChar == '>');
  }

  /// <summary>
  /// Checks the given candidate based on some heuristics
  /// and tries to determine if it needs to be replaced or not.
  /// </summary>
  /// <param name="candidate">The part of the input that potentially needs to be replaced.</param>
  /// <param name="type">The type of replacement that needs to be done.</param>
  /// <returns>True if the given candidate needs to be replaced.</returns>
  private bool TryGetReplacementType(ReadOnlySpan<char> candidate, out ReplacementType type)
  {
    type = default;

    // Discard too short candidates as the shortest possible link is either "ab://c" or "a@b.de".
    if (candidate.Length < 6)
      return false;

    // We assume a link without a scheme for candidates starting with the common subdomain.
    if (candidate.StartsWith(DefaultSubdomain, StringComparison.OrdinalIgnoreCase))
    {
      type = GetLinkType(candidate, false);
      return true;
    }

    // We assume a fully qualified link with a scheme if the separator is found anywhere.
    if (candidate.IndexOf(SchemeDelimiter) >= 0)
    {
      type = GetLinkType(candidate, true);
      return true;
    }

    // We assume an email address if the candidate contains exactly one 'at' character
    // (that is not at the beginning, as this would more likely be some sort of handle).
    var firstAtIndex = candidate.IndexOf('@');
    if (firstAtIndex >= 1 && firstAtIndex == candidate.LastIndexOf('@'))
    {
      type = candidate.StartsWith(MailToProtocol, StringComparison.OrdinalIgnoreCase)
        ? ReplacementType.EmailWithScheme
        : ReplacementType.EmailWithoutScheme;
      return true;
    }

    return false;
  }

  /// <summary>
  /// Determines the type of the link.
  /// </summary>
  /// <param name="link">The candidate that was already determined to be a link of some sort.</param>
  /// <param name="withScheme">True if the link was determined to already have the scheme at the beginning.</param>
  /// <returns>The type of the link so that it can be properly replaced later.</returns>
  private ReplacementType GetLinkType(ReadOnlySpan<char> link, bool withScheme)
  {
    // Treat all links as internal links as there is no difference between
    // internal and external links when they should all open in the same tab.
    if (!_options.OpenExternalLinksInNewTab)
    {
      return withScheme
        ? ReplacementType.InternalWithScheme
        : ReplacementType.InternalWithoutScheme;
    }

    // Treat all links as external links when the internal host is not set
    // as the distinction between internal and external is done based on the host.
    if (string.IsNullOrEmpty(_options.InternalHost))
    {
      return withScheme
        ? ReplacementType.ExternalWithScheme
        : ReplacementType.ExternalWithoutScheme;
    }

    // Try to get the host from the link and compare it to the given internal host.
    if (IsInternalHost(link, withScheme))
    {
      return withScheme
        ? ReplacementType.InternalWithScheme
        : ReplacementType.InternalWithoutScheme;
    }

    return withScheme
      ? ReplacementType.ExternalWithScheme
      : ReplacementType.ExternalWithoutScheme;
  }

  /// <summary>
  /// Determines if the host of the given link matches the internal host given in the options.
  /// </summary>
  /// <param name="link">The assumed link with or without scheme.</param>
  /// <param name="withScheme">True if the link was determined to already have the scheme at the beginning.</param>
  /// <returns>True if the host of the link matches the internal host of the options.</returns>
  private bool IsInternalHost(ReadOnlySpan<char> link, bool withScheme)
  {
    var host = GetHost(link, withScheme);

    return host.Equals(_options.InternalHost, StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>
  /// Returns the host of the given link.
  /// </summary>
  /// <param name="link">The assumed link with or without scheme.</param>
  /// <param name="withScheme">True if the link was determined to already have the scheme at the beginning.</param>
  /// <returns>The host of the given link without the scheme.</returns>
  private static ReadOnlySpan<char> GetHost(ReadOnlySpan<char> link, bool withScheme)
  {
    var linkWithoutScheme = StripScheme(link, withScheme);
    var hostEnd = linkWithoutScheme.IndexOfAny(HostDelimiters);

    return hostEnd >= 0 ? linkWithoutScheme[..hostEnd] : linkWithoutScheme;
  }

  /// <summary>
  /// Returns the link without the scheme at the beginning in case there was any.
  /// </summary>
  /// <param name="link">The assumed link with or without scheme.</param>
  /// <param name="withScheme">True if the link was determined to already have the scheme at the beginning.</param>
  /// <returns>The link without the scheme.</returns>
  private static ReadOnlySpan<char> StripScheme(ReadOnlySpan<char> link, bool withScheme)
  {
    // return the link immediately in case we know that it does not contain a scheme
    if (!withScheme)
      return link;

    // otherwise find the end of the scheme and strip it from the link
    var schemeEnd = link.IndexOf(SchemeDelimiter);
    var hostStart = schemeEnd >= 0 ? schemeEnd + SchemeDelimiter.Length : 0;

    return link[hostStart..];
  }

  /// <summary>
  /// Writes the HTML markup for the given replacement into the target span.
  /// </summary>
  /// <param name="output">The target span to write into.</param>
  /// <param name="position">The current write position, advanced by the number of characters written.</param>
  /// <param name="inputSlice">The matched part of the input.</param>
  /// <param name="type">The type of replacement that determines the HTML markup.</param>
  /// <param name="defaultScheme">The default scheme to prepend for links without a scheme.</param>
  private static void WriteReplacement(Span<char> output, ref int position, ReadOnlySpan<char> inputSlice,
    ReplacementType type, ReadOnlySpan<char> defaultScheme)
  {
    Write(output, ref position, OpeningTagBegin);

    switch (type)
    {
      case ReplacementType.InternalWithScheme:
      case ReplacementType.EmailWithScheme:
        Write(output, ref position, inputSlice);
        Write(output, ref position, OpeningTagEndInternal);
        break;
      case ReplacementType.InternalWithoutScheme:
        Write(output, ref position, defaultScheme);
        Write(output, ref position, inputSlice);
        Write(output, ref position, OpeningTagEndInternal);
        break;
      case ReplacementType.ExternalWithScheme:
        Write(output, ref position, inputSlice);
        Write(output, ref position, OpeningTagEndExternal);
        break;
      case ReplacementType.ExternalWithoutScheme:
        Write(output, ref position, defaultScheme);
        Write(output, ref position, inputSlice);
        Write(output, ref position, OpeningTagEndExternal);
        break;
      case ReplacementType.EmailWithoutScheme:
        Write(output, ref position, MailToProtocol);
        Write(output, ref position, inputSlice);
        Write(output, ref position, OpeningTagEndInternal);
        break;
      default:
        throw new ArgumentOutOfRangeException(nameof(type), type, null);
    }

    Write(output, ref position, inputSlice);
    Write(output, ref position, ClosingTag);
  }

  /// <summary>
  /// Copies the given value into the target span at the current
  /// position and advances the position by the number of characters written.
  /// </summary>
  /// <param name="targetSpan">The target span to write into.</param>
  /// <param name="position">The current write position, advanced by the length of the value.</param>
  /// <param name="value">The characters to write.</param>
  private static void Write(Span<char> targetSpan, ref int position, ReadOnlySpan<char> value)
  {
    value.CopyTo(targetSpan[position..]);
    position += value.Length;
  }
}
