namespace Linkernizer;

/// <summary>
///   <para>
///     The options that are used to influence how the HTML markup will be generated.
///   </para>
///   <example>
///     Usually you should not instantiate this class directly,
///     but instead configure the options on the constructor directly:
///     <code>
///       var linkernizer = new Linkernizer(options => {
///         options.DefaultScheme = "https://";
///         options.InternalHost = "www.example.org";
///         options.OpenExternalLinksInNewTab = true;
///       });
///     </code>
///   </example>
/// </summary>
public class LinkernizerOptions
{
  /// <summary>
  ///   <para>
  ///     The default URI scheme will be used for links without a scheme specified (like www.example.org).
  ///   </para>
  ///   <example>
  ///     This option will only be used in the href attribute:
  ///     <code>
  ///       <![CDATA[<a href="{DefaultScheme}www.example.org">www.example.org</a>]]>
  ///     </code>
  ///   </example>
  /// </summary>
  public string DefaultScheme { get; set; } = "https://";

  /// <summary>
  ///   <para>
  ///     The host or domain for which links should be considered internal links.
  ///     This option is only relevant when <see cref="OpenExternalLinksInNewTab"/> is set to true.
  ///   </para>
  ///   <example>
  ///     The value should not contain any trailing slashes, for example: <c>www.example.org</c>
  ///   </example>
  /// </summary>
  public string InternalHost { get; set; } = string.Empty;

  /// <summary>
  ///   <para>
  ///     Determines if the target attribute should be set for external links.
  ///     All links will be considered external links if no <see cref="InternalHost"/> is set.
  ///   </para>
  ///   <example>
  ///     This option will set the target attribute to "_blank":
  ///     <code>
  ///       <![CDATA[<a href="www.example.org" target="_blank">www.example.org</a>]]>
  ///     </code>
  ///   </example>
  /// </summary>
  public bool OpenExternalLinksInNewTab { get; set; }
}
