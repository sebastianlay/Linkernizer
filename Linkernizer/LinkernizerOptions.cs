namespace Linkernizer;

/// <summary>
///   <para>
///     The options that are used to influence how the HTML markup will be generated.
///   </para>
///   <para>
///     The options become read-only once the instance has been constructed and
///     any further attempt to change them will throw an <see cref="InvalidOperationException"/>.
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
  private bool _isReadOnly;

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
  /// <exception cref="InvalidOperationException">The option is changed after the library has been constructed.</exception>
  public string DefaultScheme
  {
    get;
    set
    {
      ThrowIfReadOnly();
      field = value;
    }
  } = "https://";

  /// <summary>
  ///   <para>
  ///     The host or domain for which links should be considered internal links.
  ///     This option is only relevant when <see cref="OpenExternalLinksInNewTab"/> is set to true.
  ///   </para>
  ///   <example>
  ///     The value should not contain any trailing slashes, for example: <c>www.example.org</c>
  ///   </example>
  /// </summary>
  /// <exception cref="InvalidOperationException">The option is changed after the library has been constructed.</exception>
  public string InternalHost
  {
    get;
    set
    {
      ThrowIfReadOnly();
      field = value;
    }
  } = string.Empty;

  /// <summary>
  ///   <para>
  ///     Determines if the target attribute should be set for external links.
  ///     All links will be considered external links if no <see cref="InternalHost"/> is set.
  ///   </para>
  ///   <para>
  ///     External links always include <c>rel="noopener"</c> to prevent the opened page
  ///     from gaining access to the originating window. Use <see cref="NoReferrerOnExternalLinks"/>
  ///     to additionally omit the referrer.
  ///   </para>
  ///   <example>
  ///     This option will set the target attribute to "_blank":
  ///     <code>
  ///       <![CDATA[<a href="www.example.org" target="_blank" rel="noopener">www.example.org</a>]]>
  ///     </code>
  ///   </example>
  /// </summary>
  /// <exception cref="InvalidOperationException">The option is changed after the library has been constructed.</exception>
  public bool OpenExternalLinksInNewTab
  {
    get;
    set
    {
      ThrowIfReadOnly();
      field = value;
    }
  }

  /// <summary>
  ///   <para>
  ///     Determines if the referrer should be omitted for external links by adding
  ///     <c>noreferrer</c> to their rel attribute. This prevents the destination from
  ///     learning which page the user came from. This option is only relevant when
  ///     <see cref="OpenExternalLinksInNewTab"/> is set to true.
  ///   </para>
  ///   <example>
  ///     This option will set the rel attribute to "noopener noreferrer":
  ///     <code>
  ///       <![CDATA[<a href="www.example.org" target="_blank" rel="noopener noreferrer">www.example.org</a>]]>
  ///     </code>
  ///   </example>
  /// </summary>
  /// <exception cref="InvalidOperationException">The option is changed after the library has been constructed.</exception>
  public bool NoReferrerOnExternalLinks
  {
    get;
    set
    {
      ThrowIfReadOnly();
      field = value;
    }
  }

  /// <summary>
  /// Validates the option values and returns the first error message that is found,
  /// or null when the options are valid. This is the single source of truth that is
  /// used both by the constructor and by the dependency injection integration, so that
  /// the two can never disagree on which options are considered valid.
  /// </summary>
  /// <returns>The first validation error message, or null when the options are valid.</returns>
  internal string? Validate()
  {
    if (string.IsNullOrWhiteSpace(DefaultScheme))
      return "DefaultScheme must not be null or empty.";

    if (!DefaultScheme.EndsWith("://", StringComparison.Ordinal))
      return "DefaultScheme must end with \"://\".";

    if (InternalHost is null)
      return "InternalHost must not be null.";

    if (InternalHost.Contains("://", StringComparison.Ordinal))
      return "InternalHost must not contain a scheme.";

    return null;
  }

  /// <summary>
  /// Marks the options as read-only so that the validated values
  /// cannot be changed after the library has been constructed.
  /// </summary>
  internal void MakeReadOnly() => _isReadOnly = true;

  /// <summary>
  /// Ensures that the options can no longer be changed
  /// once they have been marked as read-only.
  /// </summary>
  /// <exception cref="InvalidOperationException">The options are already marked as read-only.</exception>
  private void ThrowIfReadOnly()
  {
    if (_isReadOnly)
      throw new InvalidOperationException("The options cannot be changed after the instance has been constructed.");
  }
}
