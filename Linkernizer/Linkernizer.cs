using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
  private const string OpeningTagEndExternal = "\" target=\"_blank\" rel=\"noopener\">";
  private const string OpeningTagEndExternalNoReferrer = "\" target=\"_blank\" rel=\"noopener noreferrer\">";
  private const string ClosingTag = "</a>";

  // The number of replacements that fit into the stack-allocated buffer
  // before it has to grow to the heap. Chosen to cover realistic inputs
  // while keeping the buffer small (32 * 8 = 256 bytes) for the stack.
  private const int InitialReplacementCapacity = 32;

  // The shortest possible link is either "ab://c" or "a@b.de".
  private const int MinimumLinkLength = 6;

  private static readonly SearchValues<string> Indicators = SearchValues.Create([SchemeDelimiter, DefaultSubdomain, "@"],
    StringComparison.OrdinalIgnoreCase
  );
  private static readonly SearchValues<char> TrimCharacters = SearchValues.Create('.', ':', '?', '!', ',', ';');
  private static readonly SearchValues<char> ForbiddenCharacters = SearchValues.Create('"', '<', '>');

  // Schemes that could execute scripts when the link is clicked. The span alternate
  // lookup allows checking a candidate's scheme without allocating it as a string.
  private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> DangerousSchemes = FrozenSet.Create(
    StringComparer.OrdinalIgnoreCase, "javascript", "vbscript", "data")
    .GetAlternateLookup<ReadOnlySpan<char>>();

  private static readonly SearchValues<char> AuthorityDelimiters = SearchValues.Create('/', '?', '#');
  private static readonly SearchValues<char> Whitespaces = SearchValues.Create(
    '\u0020', '\u00A0', '\u1680', '\u2000', '\u2001',
    '\u2002', '\u2003', '\u2004', '\u2005', '\u2006',
    '\u2007', '\u2008', '\u2009', '\u200A', '\u202F',
    '\u205F', '\u3000', '\u2028', '\u2029', '\u0009',
    '\u000A', '\u000B', '\u000C', '\u000D', '\u0085'
  );

  private readonly string _defaultScheme;
  private readonly string _internalHost;
  private readonly string _openingTagEndExternal;
  private readonly bool _openExternalLinksInNewTab;

  /// <summary>
  ///   <para>
  ///     Initializes a new instance of the library with the given options.
  ///     Sane default options will be assumed if no options are given.
  ///   </para>
  ///   <para>
  ///     It is recommended to only create one instance and reuse it across the application.
  ///     Instances are immutable after construction and safe for concurrent use from multiple threads.
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
  /// <exception cref="ArgumentException">The given options contain invalid values.</exception>
  public Linkernizer(Action<LinkernizerOptions>? action = null)
  {
    var options = new LinkernizerOptions();
    action?.Invoke(options);

    if (string.IsNullOrWhiteSpace(options.DefaultScheme))
      throw new ArgumentException("DefaultScheme must not be null or empty.", nameof(action));

    if (!options.DefaultScheme.EndsWith(SchemeDelimiter, StringComparison.Ordinal))
      throw new ArgumentException("DefaultScheme must end with \"://\".", nameof(action));

    if (options.InternalHost is null)
      throw new ArgumentException("InternalHost must not be null.", nameof(action));

    if (options.InternalHost.Contains(SchemeDelimiter, StringComparison.Ordinal))
      throw new ArgumentException("InternalHost must not contain a scheme.", nameof(action));

    options.InternalHost = options.InternalHost.TrimEnd('/');
    options.MakeReadOnly();

    _defaultScheme = options.DefaultScheme;
    _internalHost = options.InternalHost;
    _openExternalLinksInNewTab = options.OpenExternalLinksInNewTab;
    _openingTagEndExternal = options.NoReferrerOnExternalLinks
      ? OpeningTagEndExternalNoReferrer
      : OpeningTagEndExternal;
  }

  /// <summary>
  ///   <para>
  ///     Wraps links in the given input with HTML hyperlink markup.
  ///   </para>
  ///   <example>
  ///     Will turn the input <c>www.example.org</c> into
  ///     <c><![CDATA[<a href="https://www.example.org">www.example.org</a>]]></c>.
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

    return ReplaceLinks(input) ?? input;
  }

  /// <summary>
  ///   <para>
  ///     Wraps links in the given input with HTML hyperlink markup.
  ///   </para>
  ///   <example>
  ///     Will turn the input <c>www.example.org</c> into
  ///     <c><![CDATA[<a href="https://www.example.org">www.example.org</a>]]></c>.
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

    return ReplaceLinks(input) ?? input.ToString();
  }

  /// <summary>
  /// Finds all links in the given input and replaces them with HTML hyperlink markup.
  /// </summary>
  /// <param name="input">The complete input.</param>
  /// <returns>
  /// The output containing the markup, or null when the input contains nothing to replace,
  /// so that each caller can return the input unchanged without an unnecessary copy.
  /// </returns>
  private string? ReplaceLinks(ReadOnlySpan<char> input)
  {
    var replacementList = new ReplacementList(stackalloc Replacement[InitialReplacementCapacity]);

    // Without any replacements the list never grew beyond the stack-allocated
    // buffer, so there is no pooled array to return and nothing to dispose.
    if (!GetReplacements(input, ref replacementList))
      return null;

    var output = CreateOutput(input, replacementList.Replacements);
    replacementList.Dispose();

    return output;
  }

  /// <summary>
  /// Constructs the output for the given input with the given replacements done.
  /// The output is built directly from the input span without materializing it as a string.
  /// </summary>
  /// <param name="input">The complete input.</param>
  /// <param name="replacements">The replacements to be made.</param>
  /// <returns>A newly constructed string with relevant replacements done.</returns>
  private string CreateOutput(ReadOnlySpan<char> input, ReadOnlySpan<Replacement> replacements)
  {
    var outputLength = GetOutputLength(input.Length, replacements, _defaultScheme.Length, _openingTagEndExternal.Length);
    var state = new State(input, replacements, _defaultScheme, _openingTagEndExternal);

    return string.Create(outputLength, state, WriteOutput);
  }

  /// <summary>
  /// Computes the total length of the output string based on
  /// the input length and the markup overhead of each replacement.
  /// </summary>
  /// <param name="length">The length of the complete input.</param>
  /// <param name="replacements">The list of replacements to be made.</param>
  /// <param name="defaultSchemeLength">The length of the default scheme.</param>
  /// <param name="externalTagEndLength">The length of the opening tag end used for external links.</param>
  /// <returns>The exact length of the output string.</returns>
  /// <exception cref="UnreachableException">A replacement has an unknown type.</exception>
  private static int GetOutputLength(int length, ReadOnlySpan<Replacement> replacements,
    int defaultSchemeLength, int externalTagEndLength)
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
          => OpeningTagBegin.Length + externalTagEndLength + ClosingTag.Length,

        ReplacementType.ExternalWithoutScheme
          => OpeningTagBegin.Length + externalTagEndLength + ClosingTag.Length + defaultSchemeLength,

        ReplacementType.EmailWithoutScheme
          => OpeningTagBegin.Length + OpeningTagEndInternal.Length + ClosingTag.Length + MailToProtocol.Length,

        _ => throw new UnreachableException($"Unknown replacement type: {replacement.Type}")
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
    var position = 0;
    var inputIndex = 0;

    foreach (var replacement in state.Replacements)
    {
      // Copy the part of the original input that leads up
      // to the beginning of the current replacement.
      if (replacement.Offset > inputIndex)
      {
        var segment = state.Input[inputIndex..replacement.Offset];
        segment.CopyTo(output[position..]);
        position += segment.Length;
        inputIndex = replacement.Offset;
      }

      // Write the new value of the current replacement.
      var inputSlice = state.Input.Slice(replacement.Offset, replacement.Length);
      WriteReplacement(output, ref position, inputSlice, replacement.Type, state.DefaultScheme, state.OpeningTagEndExternal);
      inputIndex += replacement.Length;
    }

    // Check if there is anything remaining.
    if (inputIndex >= state.Input.Length)
      return;

    // Copy the remaining part of the original input.
    var remaining = state.Input[inputIndex..];
    remaining.CopyTo(output[position..]);
  }

  /// <summary>
  /// Finds all parts of the given input that need to be replaced
  /// and adds their location in the input and their type to the given list.
  /// </summary>
  /// <param name="input">The complete input.</param>
  /// <param name="replacements">The list that collects the replacements.</param>
  /// <returns>True if at least one replacement was found and added to the list.</returns>
  private bool GetReplacements(ReadOnlySpan<char> input, ref ReplacementList replacements)
  {
    var hasReplacements = false;

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

      // Ignore excessively long 'words'.
      if (length > ushort.MaxValue)
        continue;

      // Check if the word is actually a link and, if so, which type.
      var candidate = input.Slice(offset, length);
      if (!TryGetReplacementType(candidate, out var type))
        continue;

      replacements.Add(new Replacement(offset, (ushort)length, type));
      hasReplacements = true;
    }

    return hasReplacements;
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

    // Trim alternately until nothing is left to trim, so that the
    // trimming order does not influence the result (as in "(www.example.org.)").
    while (true)
    {
      // Trim some trailing characters (even though they could technically be part of the URL).
      if (length > 0 && TrimCharacters.Contains(input[offset + length - 1]))
      {
        length--;
        continue;
      }

      // Trim parentheses, brackets and quotes in pairs.
      if (length > 1 && AreMatchingPair(input[offset], input[offset + length - 1]))
      {
        offset++;
        length -= 2;
        continue;
      }

      return (offset, length);
    }
  }

  /// <summary>
  /// Determines if the given characters are a matching pair
  /// of parentheses, brackets or quotes.
  /// </summary>
  /// <param name="firstChar">The first character of the candidate.</param>
  /// <param name="lastChar">The last character of the candidate.</param>
  /// <returns>True if the two characters are a matching pair.</returns>
  private static bool AreMatchingPair(char firstChar, char lastChar)
  {
    return firstChar switch
    {
      '(' => lastChar == ')',
      '[' => lastChar == ']',
      '{' => lastChar == '}',
      '<' => lastChar == '>',
      '"' => lastChar == '"',
      '\'' => lastChar == '\'',
      _ => false
    };
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

    // Discard too short candidates that cannot possibly contain a link.
    if (candidate.Length < MinimumLinkLength)
      return false;

    // Discard candidates containing characters that would break out of the generated markup.
    if (candidate.ContainsAny(ForbiddenCharacters))
      return false;

    // We assume a link without a scheme for candidates starting with the common subdomain.
    if (candidate.StartsWith(DefaultSubdomain, StringComparison.OrdinalIgnoreCase))
    {
      type = GetLinkType(candidate, false);
      return true;
    }

    // We assume a fully qualified link with a scheme if the separator is found anywhere
    // (with at least one character before it for the scheme and one after it for the host).
    var delimiterIndex = candidate.IndexOf(SchemeDelimiter);
    if (delimiterIndex >= 1 && delimiterIndex + SchemeDelimiter.Length < candidate.Length)
    {
      // Reject schemes that could execute scripts when the link is clicked,
      // as these would otherwise allow XSS attacks (as in "javascript://...").
      if (DangerousSchemes.Contains(candidate[..delimiterIndex]))
        return false;

      type = GetLinkType(candidate, true);
      return true;
    }

    // We assume an email address if the candidate contains exactly one 'at' character.
    // Not at the beginning, as this would more likely be some sort of handle.
    // Not at the end, as an email address requires a domain after it.
    var firstAtIndex = candidate.IndexOf('@');
    if (firstAtIndex >= 1 && firstAtIndex < candidate.Length - 1 && !candidate[(firstAtIndex + 1)..].Contains('@'))
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
    // Links are internal when they should not open in a new tab,
    // or when their host matches the configured internal host.
    var isInternal = !_openExternalLinksInNewTab || IsInternalHost(link, withScheme);

    return (isInternal, withScheme) switch
    {
      (true, true) => ReplacementType.InternalWithScheme,
      (true, false) => ReplacementType.InternalWithoutScheme,
      (false, true) => ReplacementType.ExternalWithScheme,
      (false, false) => ReplacementType.ExternalWithoutScheme
    };
  }

  /// <summary>
  /// Determines if the host of the given link matches the internal host given in the options.
  /// Always returns false when no internal host is configured, as the distinction between
  /// internal and external links is then meaningless and all links are treated as external.
  /// </summary>
  /// <param name="link">The assumed link with or without scheme.</param>
  /// <param name="withScheme">True if the link was determined to already have the scheme at the beginning.</param>
  /// <returns>True if the host of the link matches the internal host of the options.</returns>
  private bool IsInternalHost(ReadOnlySpan<char> link, bool withScheme)
  {
    if (string.IsNullOrEmpty(_internalHost))
      return false;

    var host = GetHost(link, withScheme);
    return host.Equals(_internalHost, StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>
  /// Returns the host of the given link by parsing the authority component
  /// (as in "user:pass@www.example.org:8080") without allocating a full URI.
  /// Hosts that are IPv6 addresses are returned with their enclosing brackets,
  /// which matches the behavior of <see cref="Uri.Host"/>.
  /// </summary>
  /// <param name="link">The assumed link with or without scheme.</param>
  /// <param name="withScheme">True if the link was determined to already have the scheme at the beginning.</param>
  /// <returns>The host of the given link without the scheme, user information, and port.</returns>
  private static ReadOnlySpan<char> GetHost(ReadOnlySpan<char> link, bool withScheme)
  {
    var linkWithoutScheme = StripScheme(link, withScheme);

    // The authority ends at the first slash, question mark, or hash.
    var authorityEnd = linkWithoutScheme.IndexOfAny(AuthorityDelimiters);
    var authority = authorityEnd >= 0 ? linkWithoutScheme[..authorityEnd] : linkWithoutScheme;

    // Strip the user information before the last 'at' character. Using the last
    // one (like browsers do) prevents spoofing the host with "host@actualhost".
    var userInfoEnd = authority.LastIndexOf('@');
    if (userInfoEnd >= 0)
      authority = authority[(userInfoEnd + 1)..];

    // Hosts that are IPv6 addresses are enclosed in brackets
    // and can contain colons (as in "[2001:db8::1]:8080").
    if (authority is ['[', ..])
    {
      var bracketEnd = authority.IndexOf(']');
      return bracketEnd >= 0 ? authority[..(bracketEnd + 1)] : authority;
    }

    // Otherwise the host ends at the colon that separates the port.
    var portStart = authority.IndexOf(':');
    return portStart >= 0 ? authority[..portStart] : authority;
  }

  /// <summary>
  /// Returns the link without the scheme at the beginning in case there was any.
  /// </summary>
  /// <param name="link">The assumed link with or without scheme.</param>
  /// <param name="withScheme">True if the link was determined to already have the scheme at the beginning.</param>
  /// <returns>The link without the scheme.</returns>
  private static ReadOnlySpan<char> StripScheme(ReadOnlySpan<char> link, bool withScheme)
  {
    // Return the link immediately in case we know that it does not contain a scheme.
    if (!withScheme)
      return link;

    // Otherwise find the end of the scheme and strip it from the link.
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
  /// <param name="openingTagEndExternal">The opening tag end to use for external links.</param>
  /// <exception cref="UnreachableException">The replacement has an unknown type.</exception>
  private static void WriteReplacement(Span<char> output, ref int position, ReadOnlySpan<char> inputSlice,
    ReplacementType type, ReadOnlySpan<char> defaultScheme, ReadOnlySpan<char> openingTagEndExternal)
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
        Write(output, ref position, openingTagEndExternal);
        break;
      case ReplacementType.ExternalWithoutScheme:
        Write(output, ref position, defaultScheme);
        Write(output, ref position, inputSlice);
        Write(output, ref position, openingTagEndExternal);
        break;
      case ReplacementType.EmailWithoutScheme:
        Write(output, ref position, MailToProtocol);
        Write(output, ref position, inputSlice);
        Write(output, ref position, OpeningTagEndInternal);
        break;
      default:
        throw new UnreachableException($"Unknown replacement type: {type}");
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
