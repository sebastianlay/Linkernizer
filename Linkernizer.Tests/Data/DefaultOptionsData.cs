using Xunit;

namespace Linkernizer.Tests.Data;

/// <summary>
/// The data that is used to test the library with the default options.
/// </summary>
internal sealed class DefaultOptionsData : TheoryData<string?, string?>
{
  /// <summary>
  /// The constructor will be implicitly called by the test framework.
  /// </summary>
  public DefaultOptionsData()
  {
    // Empty values
    Add(null, null);
    Add(string.Empty, string.Empty);
    Add(" ", " ");
    Add("\n", "\n");
    Add("\u00A0", "\u00A0");

    // Basic URLs
    Add("example.org", "example.org");
    Add("www.example.org", """<a href="https://www.example.org">www.example.org</a>""");
    Add("http://www.example.org", """<a href="http://www.example.org">http://www.example.org</a>""");
    Add("https://www.example.org", """<a href="https://www.example.org">https://www.example.org</a>""");
    Add("example.com", "example.com");
    Add("www.example.com", """<a href="https://www.example.com">www.example.com</a>""");
    Add("http://www.example.com", """<a href="http://www.example.com">http://www.example.com</a>""");
    Add("https://www.example.com", """<a href="https://www.example.com">https://www.example.com</a>""");

    // Basic URLs in context
    Add("Lorem example.org ipsum", "Lorem example.org ipsum");
    Add("Lorem www.example.org ipsum", """Lorem <a href="https://www.example.org">www.example.org</a> ipsum""");
    Add("Lorem https://www.example.org ipsum", """Lorem <a href="https://www.example.org">https://www.example.org</a> ipsum""");
    Add("Lorem example.com ipsum", "Lorem example.com ipsum");
    Add("Lorem www.example.com ipsum", """Lorem <a href="https://www.example.com">www.example.com</a> ipsum""");
    Add("Lorem https://www.example.com ipsum", """Lorem <a href="https://www.example.com">https://www.example.com</a> ipsum""");

    // Leading and trailing whitespaces
    Add(" example.org ", " example.org ");
    Add(" www.example.org ", " <a href=\"https://www.example.org\">www.example.org</a> ");
    Add(" https://www.example.org ", " <a href=\"https://www.example.org\">https://www.example.org</a> ");
    Add("\u00A0example.org\u00A0", "\u00A0example.org\u00A0");
    Add("\u00A0www.example.org\u00A0", "\u00A0<a href=\"https://www.example.org\">www.example.org</a>\u00A0");
    Add("\u00A0https://www.example.org\u00A0", "\u00A0<a href=\"https://www.example.org\">https://www.example.org</a>\u00A0");
    Add("\nexample.org\n", "\nexample.org\n");
    Add("\nwww.example.org\n", "\n<a href=\"https://www.example.org\">www.example.org</a>\n");
    Add("\nhttps://www.example.org\n", "\n<a href=\"https://www.example.org\">https://www.example.org</a>\n");

    // Leading and trailing whitespaces in context
    Add(" Lorem example.org ipsum ", " Lorem example.org ipsum ");
    Add(" Lorem www.example.org ipsum ", " Lorem <a href=\"https://www.example.org\">www.example.org</a> ipsum ");
    Add(" Lorem https://www.example.org ipsum ", " Lorem <a href=\"https://www.example.org\">https://www.example.org</a> ipsum ");
    Add("\u00A0Lorem\u00A0example.org\u00A0ipsum\u00A0", "\u00A0Lorem\u00A0example.org\u00A0ipsum\u00A0");
    Add("\u00A0Lorem\u00A0www.example.org\u00A0ipsum\u00A0", "\u00A0Lorem\u00A0<a href=\"https://www.example.org\">www.example.org</a>\u00A0ipsum\u00A0");
    Add("\u00A0Lorem\u00A0https://www.example.org\u00A0ipsum\u00A0", "\u00A0Lorem\u00A0<a href=\"https://www.example.org\">https://www.example.org</a>\u00A0ipsum\u00A0");
    Add("\nLorem\nexample.org\nipsum\n", "\nLorem\nexample.org\nipsum\n");
    Add("\nLorem\nwww.example.org\nipsum\n", "\nLorem\n<a href=\"https://www.example.org\">www.example.org</a>\nipsum\n");
    Add("\nLorem\nhttps://www.example.org\nipsum\n", "\nLorem\n<a href=\"https://www.example.org\">https://www.example.org</a>\nipsum\n");

    // Trailing characters
    Add("https://www.example.org/", """<a href="https://www.example.org/">https://www.example.org/</a>""");
    Add("https://www.example.org?", """<a href="https://www.example.org">https://www.example.org</a>?""");
    Add("https://www.example.org#", """<a href="https://www.example.org#">https://www.example.org#</a>""");
    Add("https://www.example.org/example", """<a href="https://www.example.org/example">https://www.example.org/example</a>""");
    Add("https://www.example.org/example/", """<a href="https://www.example.org/example/">https://www.example.org/example/</a>""");
    Add("https://www.example.org/example?", """<a href="https://www.example.org/example">https://www.example.org/example</a>?""");
    Add("https://www.example.org/example#", """<a href="https://www.example.org/example#">https://www.example.org/example#</a>""");
    Add("https://www.example.org/example_example", """<a href="https://www.example.org/example_example">https://www.example.org/example_example</a>""");
    Add("https://www.example.org/example_example/", """<a href="https://www.example.org/example_example/">https://www.example.org/example_example/</a>""");
    Add("https://www.example.org/example_example?", """<a href="https://www.example.org/example_example">https://www.example.org/example_example</a>?""");
    Add("https://www.example.org/example_example#", """<a href="https://www.example.org/example_example#">https://www.example.org/example_example#</a>""");

    // Trailing characters in context
    Add("Lorem https://www.example.org/ ipsum", """Lorem <a href="https://www.example.org/">https://www.example.org/</a> ipsum""");
    Add("Lorem https://www.example.org// ipsum", """Lorem <a href="https://www.example.org//">https://www.example.org//</a> ipsum""");
    Add("Lorem https://www.example.org/example ipsum", """Lorem <a href="https://www.example.org/example">https://www.example.org/example</a> ipsum""");
    Add("Lorem https://www.example.org/example/ ipsum", """Lorem <a href="https://www.example.org/example/">https://www.example.org/example/</a> ipsum""");
    Add("Lorem https://www.example.org/example// ipsum", """Lorem <a href="https://www.example.org/example//">https://www.example.org/example//</a> ipsum""");
    Add("Lorem https://www.example.org/example_example ipsum", """Lorem <a href="https://www.example.org/example_example">https://www.example.org/example_example</a> ipsum""");
    Add("Lorem https://www.example.org/example_example/ ipsum", """Lorem <a href="https://www.example.org/example_example/">https://www.example.org/example_example/</a> ipsum""");
    Add("Lorem https://www.example.org/example_example// ipsum", """Lorem <a href="https://www.example.org/example_example//">https://www.example.org/example_example//</a> ipsum""");

    // Parentheses
    Add("https://www.example.org/example_(example)", """<a href="https://www.example.org/example_(example)">https://www.example.org/example_(example)</a>""");
    Add("https://www.example.org?example=(example)", """<a href="https://www.example.org?example=(example)">https://www.example.org?example=(example)</a>""");
    Add("https://www.example.org/?example=(example)", """<a href="https://www.example.org/?example=(example)">https://www.example.org/?example=(example)</a>""");
    Add("https://www.example.org/?example=example%5Borg%5D", """<a href="https://www.example.org/?example=example%5Borg%5D">https://www.example.org/?example=example%5Borg%5D</a>""");
    Add("(https://www.example.org/example_(example))", """(<a href="https://www.example.org/example_(example)">https://www.example.org/example_(example)</a>)""");
    Add("(https://www.example.org?example=(example))", """(<a href="https://www.example.org?example=(example)">https://www.example.org?example=(example)</a>)""");
    Add("(https://www.example.org/?example=(example))", """(<a href="https://www.example.org/?example=(example)">https://www.example.org/?example=(example)</a>)""");
    Add("(https://www.example.org/?example=example%5Borg%5D)", """(<a href="https://www.example.org/?example=example%5Borg%5D">https://www.example.org/?example=example%5Borg%5D</a>)""");

    // Parentheses in context
    Add("Lorem https://www.example.org/example_(example) ipsum", """Lorem <a href="https://www.example.org/example_(example)">https://www.example.org/example_(example)</a> ipsum""");
    Add("Lorem https://www.example.org?example=(example) ipsum", """Lorem <a href="https://www.example.org?example=(example)">https://www.example.org?example=(example)</a> ipsum""");
    Add("Lorem https://www.example.org/?example=(example) ipsum", """Lorem <a href="https://www.example.org/?example=(example)">https://www.example.org/?example=(example)</a> ipsum""");
    Add("Lorem https://www.example.org/?example=example%5Borg%5D ipsum", """Lorem <a href="https://www.example.org/?example=example%5Borg%5D">https://www.example.org/?example=example%5Borg%5D</a> ipsum""");
    Add("Lorem (https://www.example.org/example_(example)) ipsum", """Lorem (<a href="https://www.example.org/example_(example)">https://www.example.org/example_(example)</a>) ipsum""");
    Add("Lorem (https://www.example.org?example=(example)) ipsum", """Lorem (<a href="https://www.example.org?example=(example)">https://www.example.org?example=(example)</a>) ipsum""");
    Add("Lorem (https://www.example.org/?example=(example)) ipsum", """Lorem (<a href="https://www.example.org/?example=(example)">https://www.example.org/?example=(example)</a>) ipsum""");
    Add("Lorem (https://www.example.org/?example=example%5Borg%5D) ipsum", """Lorem (<a href="https://www.example.org/?example=example%5Borg%5D">https://www.example.org/?example=example%5Borg%5D</a>) ipsum""");

    // Punctuation
    Add("Lorem https://www.example.org. Ipsum.", """Lorem <a href="https://www.example.org">https://www.example.org</a>. Ipsum.""");
    Add("Lorem https://www.example.org/. Ipsum.", """Lorem <a href="https://www.example.org/">https://www.example.org/</a>. Ipsum.""");
    Add("Lorem https://www.example.org? Ipsum.", """Lorem <a href="https://www.example.org">https://www.example.org</a>? Ipsum.""");
    Add("Lorem https://www.example.org#. Ipsum.", """Lorem <a href="https://www.example.org#">https://www.example.org#</a>. Ipsum.""");
    Add("Lorem https://lorem.ipsum.example.co.uk. Ipsum.", """Lorem <a href="https://lorem.ipsum.example.co.uk">https://lorem.ipsum.example.co.uk</a>. Ipsum.""");
    Add("Lorem https://lorem.ipsum.example.co.uk! Ipsum.", """Lorem <a href="https://lorem.ipsum.example.co.uk">https://lorem.ipsum.example.co.uk</a>! Ipsum.""");
    Add("Lorem https://www.example.org/example_(example). Ipsum.", """Lorem <a href="https://www.example.org/example_(example)">https://www.example.org/example_(example)</a>. Ipsum.""");
    Add("Lorem https://www.example.org/example_(example)! Ipsum.", """Lorem <a href="https://www.example.org/example_(example)">https://www.example.org/example_(example)</a>! Ipsum.""");

    // Ports
    Add("example.org:80", "example.org:80");
    Add("www.example.org:80", """<a href="https://www.example.org:80">www.example.org:80</a>""");
    Add("https://www.example.org:80", """<a href="https://www.example.org:80">https://www.example.org:80</a>""");
    Add("example.org:80/", "example.org:80/");
    Add("www.example.org:80/", """<a href="https://www.example.org:80/">www.example.org:80/</a>""");
    Add("https://www.example.org:80/", """<a href="https://www.example.org:80/">https://www.example.org:80/</a>""");

    // Email
    Add("@example.org", "@example.org");
    Add("mail@example.org", """<a href="mailto:mail@example.org">mail@example.org</a>""");
    Add("mailto:mail@example.org", """<a href="mailto:mail@example.org">mailto:mail@example.org</a>""");
    Add("mail+mail.mail@example.org", """<a href="mailto:mail+mail.mail@example.org">mail+mail.mail@example.org</a>""");

    // Email in context
    Add("Lorem @example.org ipsum", "Lorem @example.org ipsum");
    Add("Lorem mailto:mail@example.org ipsum", """Lorem <a href="mailto:mail@example.org">mailto:mail@example.org</a> ipsum""");
    Add("Lorem mail@example.org ipsum", """Lorem <a href="mailto:mail@example.org">mail@example.org</a> ipsum""");
    Add("Lorem mail+mail.mail@example.org ipsum", """Lorem <a href="mailto:mail+mail.mail@example.org">mail+mail.mail@example.org</a> ipsum""");

    // Other schemes
    Add("file://example/org/example.txt", """<a href="file://example/org/example.txt">file://example/org/example.txt</a>""");
    Add("ftp://example/org/example/", """<a href="ftp://example/org/example/">ftp://example/org/example/</a>""");
    Add("git://example/org/example.git", """<a href="git://example/org/example.git">git://example/org/example.git</a>""");
    Add("irc://example.org:6667/example", """<a href="irc://example.org:6667/example">irc://example.org:6667/example</a>""");
    Add("slack://example?org=example", """<a href="slack://example?org=example">slack://example?org=example</a>""");

    // Multiple links
    Add("Lorem www.example.org ipsum mail@example.org dolor.", """Lorem <a href="https://www.example.org">www.example.org</a> ipsum <a href="mailto:mail@example.org">mail@example.org</a> dolor.""");
  }
}
