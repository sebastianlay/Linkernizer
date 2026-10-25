using System.Buffers;
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

  private static readonly SearchValues<string> Indicators = SearchValues.Create([Scheme.Delimiter, DefaultSubdomain, "@"],
    StringComparison.OrdinalIgnoreCase
  );
  private static readonly SearchValues<char> TrimCharacters = SearchValues.Create('.', ':', '?', '!', ',', ';');
  private static readonly SearchValues<char> ForbiddenCharacters = SearchValues.Create(
    '"', '<', '>',
    '\u0000', '\u0001', '\u0002', '\u0003', '\u0004', '\u0005', '\u0006', '\u0007',
    '\u0008', '\u0009', '\u000A', '\u000B', '\u000C', '\u000D', '\u000E', '\u000F',
    '\u0010', '\u0011', '\u0012', '\u0013', '\u0014', '\u0015', '\u0016', '\u0017',
    '\u0018', '\u0019', '\u001A', '\u001B', '\u001C', '\u001D', '\u001E', '\u001F',
    '\u007F'
  );

  private static readonly SearchValues<char> AuthorityDelimiters = SearchValues.Create('/', '?', '#');
  private static readonly SearchValues<char> Whitespaces = SearchValues.Create(
    '\u0020', '\u00A0', '\u1680', '\u2000', '\u2001', '\u2002', '\u2003', '\u2004',
    '\u2005', '\u2006', '\u2007', '\u2008', '\u2009', '\u200A', '\u202F', '\u205F',
    '\u3000', '\u2028', '\u2029', '\u0009', '\u000A', '\u000B', '\u000C', '\u000D',
    '\u0085'
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
    : this(CreateOptions(action), nameof(action), freeze: true)
  {
  }

  /// <summary>
  /// Initializes a new instance of the library with the given options. This overload
  /// is intended for the dependency injection integration where the options are built
  /// and owned by the options infrastructure and must therefore not be mutated or frozen.
  /// </summary>
  /// <param name="options">The options to use.</param>
  /// <exception cref="ArgumentException">The given options contain invalid values.</exception>
  internal Linkernizer(LinkernizerOptions options)
    : this(options, nameof(options), freeze: false)
  {
  }

  /// <summary>
  /// Validates the given options and initializes the instance from them. The options are
  /// only frozen when this instance owns them (the action overload), as a caller could
  /// otherwise mutate a captured reference and bypass the validation done here.
  /// </summary>
  /// <param name="options">The options to validate and initialize the instance from.</param>
  /// <param name="paramName">The name of the originating parameter for exception messages.</param>
  /// <param name="freeze">True to mark the options as read-only after initialization.</param>
  /// <exception cref="ArgumentException">The given options contain invalid values.</exception>
  private Linkernizer(LinkernizerOptions options, string paramName, bool freeze)
  {
    var error = options.Validate();
    if (error is not null)
      throw new ArgumentException(error, paramName);

    _defaultScheme = options.DefaultScheme;
    _internalHost = options.InternalHost.TrimEnd('/');
    _openExternalLinksInNewTab = options.OpenExternalLinksInNewTab;
    _openingTagEndExternal = options.NoReferrerOnExternalLinks
      ? OpeningTagEndExternalNoReferrer
      : OpeningTagEndExternal;

    if (freeze)
      options.MakeReadOnly();
  }

  /// <summary>
  /// Creates a new options instance and applies the given configuration action to it.
  /// </summary>
  /// <param name="action">The action that configures the options, or null for the defaults.</param>
  /// <returns>The configured options.</returns>
  private static LinkernizerOptions CreateOptions(Action<LinkernizerOptions>? action)
  {
    var options = new LinkernizerOptions();
    action?.Invoke(options);
    return options;
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
    var replacementList = GetReplacements(input, stackalloc Replacement[InitialReplacementCapacity]);

    // Without any replacements the list never grew beyond the stack-allocated
    // buffer, so there is no pooled array to return and nothing to dispose.
    if (replacementList.Replacements.IsEmpty)
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
    var outputLength = GetOutputLength(input.Length, replacements, _defaultScheme, _openingTagEndExternal);
    var state = new State(input, replacements, _defaultScheme, _openingTagEndExternal);

    return string.Create(outputLength, state, WriteOutput);
  }

  /// <summary>
  /// Computes the total length of the output string based on
  /// the input length and the markup overhead of each replacement.
  /// </summary>
  /// <param name="length">The length of the complete input.</param>
  /// <param name="replacements">The list of replacements to be made.</param>
  /// <param name="defaultScheme">The default scheme to prepend for links without a scheme.</param>
  /// <param name="openingTagEndExternal">The opening tag end to use for external links.</param>
  /// <returns>The exact length of the output string.</returns>
  /// <exception cref="OverflowException">The output would be longer than <see cref="int.MaxValue"/> characters.</exception>
  private static int GetOutputLength(int length, ReadOnlySpan<Replacement> replacements,
    string defaultScheme, string openingTagEndExternal)
  {
    foreach (var replacement in replacements)
    {
      var (prefix, tagEnd) = GetMarkup(replacement.Type, defaultScheme, openingTagEndExternal);

      // The additions are checked so that an output which would not fit into a string
      // fails with a meaningful exception here. The given length already contains the
      // matched part of the input once, so only its copy in the href attribute is added.
      checked
      {
        length += OpeningTagBegin.Length + prefix.Length + replacement.Length + tagEnd.Length + ClosingTag.Length;
      }
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
      // Copy the part of the original input that leads up to the beginning of the current replacement.
      Write(output, ref position, state.Input[inputIndex..replacement.Offset]);

      // Write the new value of the current replacement.
      var inputSlice = state.Input.Slice(replacement.Offset, replacement.Length);
      WriteReplacement(output, ref position, inputSlice, replacement.Type, state.DefaultScheme, state.OpeningTagEndExternal);
      inputIndex = replacement.Offset + replacement.Length;
    }

    // Copy the remaining part of the original input.
    Write(output, ref position, state.Input[inputIndex..]);
  }

  /// <summary>
  /// Finds all parts of the given input that need to be replaced
  /// and collects their location in the input and their type in a list.
  /// </summary>
  /// <param name="input">The complete input.</param>
  /// <param name="initialBuffer">The initial (usually stack-allocated) buffer for the list.</param>
  /// <returns>The list of replacements, which has to be disposed by the caller.</returns>
  private ReplacementList GetReplacements(ReadOnlySpan<char> input, Span<Replacement> initialBuffer)
  {
    var replacements = new ReplacementList(initialBuffer);
    var position = 0;
    
    while (true)
    {
      // Jump to the next indicator in the input
      var indicator = input[position..].IndexOfAny(Indicators);
      if (indicator < 0)
        break;

      // Expand selection to last whitespace before and first whitespace after the indicator
      indicator += position;
      var wordStart = position + input[position..indicator].LastIndexOfAny(Whitespaces) + 1;
      var wordLength = input[indicator..].IndexOfAny(Whitespaces);
      var wordEnd = wordLength < 0 ? input.Length : indicator + wordLength;
      var range = wordStart..wordEnd;
      position = wordEnd;
      
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
    }

    return replacements;
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

    // Discard candidates containing characters that would break out of the generated markup,
    // or control characters, as browsers remove leading ones before they parse the URL.
    // These could otherwise be used to smuggle a dangerous scheme past the check below
    // (as in "\u0001javascript://...").
    if (candidate.ContainsAny(ForbiddenCharacters))
      return false;

    // We assume a link without a scheme for candidates starting with the common subdomain.
    if (candidate.StartsWith(DefaultSubdomain, StringComparison.OrdinalIgnoreCase))
    {
      type = GetLinkType(candidate, 0);
      return true;
    }

    // We assume a fully qualified link with a scheme if the separator is found anywhere
    // (with at least one character before it for the scheme and one after it for the host).
    if (Scheme.TryGet(candidate, out var scheme))
    {
      // Reject anything that is not a syntactically valid scheme, as the browser could
      // otherwise end up with a different scheme than the one that was checked here.
      if (!Scheme.IsValid(scheme))
        return false;

      // Reject schemes that could execute scripts when the link is clicked,
      // as these would otherwise allow XSS attacks (as in "javascript://...").
      if (Scheme.IsDangerous(scheme))
        return false;

      // The authority begins directly after the scheme delimiter that was already found.
      type = GetLinkType(candidate, scheme.Length + Scheme.Delimiter.Length);
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
  /// <param name="authorityStart">The index where the authority begins, which is after the scheme delimiter for links with a scheme and zero otherwise.</param>
  /// <returns>The type of the link so that it can be properly replaced later.</returns>
  private ReplacementType GetLinkType(ReadOnlySpan<char> link, int authorityStart)
  {
    // Links are internal when they should not open in a new tab,
    // or when their host matches the configured internal host.
    var isInternal = !_openExternalLinksInNewTab || IsInternalHost(link, authorityStart);
    
    // Only links with a scheme have their authority after the beginning.
    var withScheme = authorityStart > 0;

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
  /// <param name="authorityStart">The index where the authority begins, which is after the scheme delimiter for links with a scheme and zero otherwise.</param>
  /// <returns>True if the host of the link matches the internal host of the options.</returns>
  private bool IsInternalHost(ReadOnlySpan<char> link, int authorityStart)
  {
    if (string.IsNullOrEmpty(_internalHost))
      return false;

    var host = GetHost(link, authorityStart);
    return host.Equals(_internalHost, StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>
  /// Returns the host of the given link by parsing the authority component
  /// (as in "user:pass@www.example.org:8080") without allocating a full URI.
  /// Hosts that are IPv6 addresses are returned with their enclosing brackets,
  /// which matches the behavior of <see cref="Uri.Host"/>.
  /// </summary>
  /// <param name="link">The assumed link with or without scheme.</param>
  /// <param name="authorityStart">The index where the authority begins, which is after the scheme delimiter for links with a scheme and zero otherwise.</param>
  /// <returns>The host of the given link without the scheme, user information, and port.</returns>
  private static ReadOnlySpan<char> GetHost(ReadOnlySpan<char> link, int authorityStart)
  {
    var linkWithoutScheme = link[authorityStart..];

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
  /// Writes the HTML markup for the given replacement into the target span.
  /// </summary>
  /// <param name="output">The target span to write into.</param>
  /// <param name="position">The current write position, advanced by the number of characters written.</param>
  /// <param name="inputSlice">The matched part of the input.</param>
  /// <param name="type">The type of replacement that determines the HTML markup.</param>
  /// <param name="defaultScheme">The default scheme to prepend for links without a scheme.</param>
  /// <param name="openingTagEndExternal">The opening tag end to use for external links.</param>
  private static void WriteReplacement(Span<char> output, ref int position, ReadOnlySpan<char> inputSlice,
    ReplacementType type, string defaultScheme, string openingTagEndExternal)
  {
    var (prefix, tagEnd) = GetMarkup(type, defaultScheme, openingTagEndExternal);

    Write(output, ref position, OpeningTagBegin);
    Write(output, ref position, prefix);
    Write(output, ref position, inputSlice);
    Write(output, ref position, tagEnd);
    Write(output, ref position, inputSlice);
    Write(output, ref position, ClosingTag);
  }

  /// <summary>
  /// Determines the parts of the HTML markup that depend on the type of replacement.
  /// This is used for both computing the output length and writing the output,
  /// so that the two cannot drift apart and corrupt the output.
  /// </summary>
  /// <param name="type">The type of replacement that determines the HTML markup.</param>
  /// <param name="defaultScheme">The default scheme to prepend for links without a scheme.</param>
  /// <param name="openingTagEndExternal">The opening tag end to use for external links.</param>
  /// <returns>The prefix that is written in front of the link in the href attribute, and the end of the opening tag.</returns>
  /// <exception cref="UnreachableException">The replacement has an unknown type.</exception>
  private static (string Prefix, string TagEnd) GetMarkup(ReplacementType type,
    string defaultScheme, string openingTagEndExternal)
  {
    return type switch
    {
      ReplacementType.InternalWithScheme or ReplacementType.EmailWithScheme => (string.Empty, OpeningTagEndInternal),
      ReplacementType.InternalWithoutScheme => (defaultScheme, OpeningTagEndInternal),
      ReplacementType.ExternalWithScheme => (string.Empty, openingTagEndExternal),
      ReplacementType.ExternalWithoutScheme => (defaultScheme, openingTagEndExternal),
      ReplacementType.EmailWithoutScheme => (MailToProtocol, OpeningTagEndInternal),
      _ => throw new UnreachableException($"Unknown replacement type: {type}")
    };
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
