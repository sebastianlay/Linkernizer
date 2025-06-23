using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Linkernizer.Internal;
using Microsoft.Extensions.ObjectPool;

namespace Linkernizer;

/// <summary>
///   <para>
///     This library provides functionality for detecting many different forms of links
///     in text and wrapping them with HTML hyperlink markup for displaying it on a website.
///   </para>
/// </summary>
public class Linkernizer : ILinkernizer
{
  private readonly SearchValues<string> _indicators = SearchValues.Create(["www.", "://", "@"], StringComparison.OrdinalIgnoreCase);
  private readonly SearchValues<char> _trimCharacters = SearchValues.Create(['.', ':', '?', '!', ',', ';']);
  private readonly SearchValues<char> _whitespaces = SearchValues.Create([
    '\u0020', '\u00A0', '\u1680', '\u2000', '\u2001',
    '\u2002', '\u2003', '\u2004', '\u2005', '\u2006',
    '\u2007', '\u2008', '\u2009', '\u200A', '\u202F',
    '\u205F', '\u3000', '\u2028', '\u2029', '\u0009',
    '\u000A', '\u000B', '\u000C', '\u000D', '\u0085'
  ]);

  private readonly LinkernizerOptions _options = new();
  private readonly DefaultObjectPoolProvider _objectPoolProvider = new();
  private readonly ObjectPool<StringBuilder> _stringBuilderPool;

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

    _stringBuilderPool = _objectPoolProvider.CreateStringBuilderPool();
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

    var inputAsSpan = input.AsSpan();

    // Preliminary check if there are possible matches at all.
    if (!inputAsSpan.ContainsAny(_indicators))
      return input;

    return LinkernizeInternal(inputAsSpan);
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
      return input.ToString();

    // Preliminary check if there are possible matches at all.
    if (!input.ContainsAny(_indicators))
      return input.ToString();

    return LinkernizeInternal(input);
  }

  /// <summary>
  /// Finds the parts that need to be replaced in the given input
  /// and constructs the output with the replaced values.
  /// </summary>
  /// <param name="input">The complete input.</param>
  /// <returns>A newly constructed string with relevant replacements done.</returns>
  private string LinkernizeInternal(ReadOnlySpan<char> input)
  {
    var replacements = GetReplacements(input);
    if (replacements.Count == 0)
      return input.ToString();

    var index = 0;
    var stringBuilder = _stringBuilderPool.Get();

    foreach (var replacement in replacements)
    {
      // Append the part of the original input that leads up
      // to the beginning of the current replacement.
      if (replacement.Offset > index)
      {
        stringBuilder.Append(input[index..replacement.Offset]);
        index = replacement.Offset;
      }

      // Append the new value of the current replacement.
      AppendReplacedValue(input, replacement, stringBuilder);
      index += replacement.Length;
    }

    // Append the remaining part of the original input.
    if (index < input.Length)
      stringBuilder.Append(input[index..input.Length]);

    var result = stringBuilder.ToString();
    _stringBuilderPool.Return(stringBuilder);

    return result;
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
    foreach (var range in input.SplitAny(_whitespaces))
    {
      // Ignore all 'words' that cannot contain a link.
      if (!input[range].ContainsAny(_indicators))
        continue;

      // Trim extra characters at the beginning and end
      // of the word. This is not strictly standards-compliant,
      // but often the desired behaviour in the *real* world.
      var trimmedRange = TrimExtraCharacters(input, range);
      (var offset, var length) = trimmedRange.GetOffsetAndLength(input.Length);

      // Check if the word is actually a link and, if so, which type.
      if (TryGetReplacementType(input[trimmedRange], out var type))
        result.Add(new Replacement(offset, length, type.Value));
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
  private Range TrimExtraCharacters(ReadOnlySpan<char> input, Range range)
  {
    (var offset, var length) = range.GetOffsetAndLength(input.Length);

    // Trim some trailing characters (even though they could technically be part of the URL).
    while (length > 0 && _trimCharacters.Contains(input[offset + length - 1]))
      length--;

    // Trim parentheses and brackets in pairs.
    while (length > 1 && AreParenthesesOrBrackets(input[offset], input[offset + length - 1]))
    {
      offset++;
      length -= 2;
    }

    return new Range(offset, offset + length);
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
  private bool TryGetReplacementType(ReadOnlySpan<char> candidate, [NotNullWhen(true)] out ReplacementType? type)
  {
    type = null;

    // Discard too short candidates as the shortest possible link is either "ab://c" or "a@b.de".
    if (candidate.Length < 6)
      return false;

    // We assume a link without a scheme for candidates starting with the common subdomain.
    if (candidate.StartsWith("www."))
    {
      type = GetLinkType(candidate, false);
      return true;
    }

    // We assume a fully qualified link with a scheme if the separator is found anywhere.
    if (candidate.Contains("://", StringComparison.OrdinalIgnoreCase))
    {
      type = GetLinkType(candidate, true);
      return true;
    }

    // We assume an email address if the candidate contains exactly one 'at' character
    // (that is not at the beginning, as this would more likely be some sort of handle).
    if (candidate.IndexOf('@') >= 1 && candidate.IndexOf('@') == candidate.LastIndexOf('@'))
    {
      type = candidate.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
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
  /// <returns></returns>
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
    if (IsInternalHost(link))
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
  /// Detemines if the host of the given link matches
  /// the internal host given in the options.
  /// </summary>
  /// <param name="link">The assumed link with or without scheme.</param>
  /// <returns>True if the host of the link matches the internal host of the options.</returns>
  private bool IsInternalHost(ReadOnlySpan<char> link)
  {
    // Wrapping this method in a try/catch block as parsing the URI
    // could throw all kinds of exceptions that we don't care about.
    try
    {
      // Prepend the default scheme if necessary so that we have the best
      // change of being able to parse the link as an absolute URI.
      var possibleUri = link.StartsWith("www.")
        ? _options.DefaultScheme + link.ToString()
        : link.ToString();

      if (!Uri.TryCreate(possibleUri, UriKind.Absolute, out var uri))
        return false;

      return uri.Host.Equals(_options.InternalHost, StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
      return false;
    }
  }

  /// <summary>
  /// Constructs and appends the part of the given input
  /// based on the given replacement to the result.
  /// </summary>
  /// <param name="input">The complete input.</param>
  /// <param name="replacement">The information about what should be replaced.</param>
  /// <param name="stringBuilder">The result where the replaced value should be appended to.</param>
  private void AppendReplacedValue(ReadOnlySpan<char> input, Replacement replacement, StringBuilder stringBuilder)
  {
    var slice = input.Slice(replacement.Offset, replacement.Length);

    switch (replacement.Type)
    {
      case ReplacementType.InternalWithScheme:
      case ReplacementType.EmailWithScheme:
        stringBuilder.Append("<a href=\"");
        stringBuilder.Append(slice);
        stringBuilder.Append("\">");
        stringBuilder.Append(slice);
        stringBuilder.Append("</a>");
        break;
      case ReplacementType.InternalWithoutScheme:
        stringBuilder.Append("<a href=\"");
        stringBuilder.Append(_options.DefaultScheme);
        stringBuilder.Append(slice);
        stringBuilder.Append("\">");
        stringBuilder.Append(slice);
        stringBuilder.Append("</a>");
        break;
      case ReplacementType.ExternalWithScheme:
        stringBuilder.Append("<a href=\"");
        stringBuilder.Append(slice);
        stringBuilder.Append("\" target=\"_blank\">");
        stringBuilder.Append(slice);
        stringBuilder.Append("</a>");
        break;
      case ReplacementType.ExternalWithoutScheme:
        stringBuilder.Append("<a href=\"");
        stringBuilder.Append(_options.DefaultScheme);
        stringBuilder.Append(slice);
        stringBuilder.Append("\" target=\"_blank\">");
        stringBuilder.Append(slice);
        stringBuilder.Append("</a>");
        break;
      case ReplacementType.EmailWithoutScheme:
        stringBuilder.Append("<a href=\"mailto:");
        stringBuilder.Append(slice);
        stringBuilder.Append("\">");
        stringBuilder.Append(slice);
        stringBuilder.Append("</a>");
        break;
    }
  }
}
