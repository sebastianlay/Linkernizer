using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Ganss.Text;
using TextHelper;

namespace Linkernizer.Benchmarks;

/// <summary>
/// Compares the following libraries in regards to execution time and memory footprint:
///   <list type="bullet">
///     <item>
///       <term>Linkernizer (2025)</term>
///       <description>https://github.com/sebastianlay/linkernizer</description>
///     </item>
///     <item>
///       <term>AutoLink (2015-2022)</term>
///       <description>https://github.com/mganss/AutoLink</description>
///     </item>
///     <item>
///       <term>TextHelper (2011)</term>
///       <description>https://github.com/tylermercier/TextHelper</description>
///     </item>
///   </list>
/// </summary>
[MemoryDiagnoser]
public class Benchmarks
{
  private const string ShortTextNoMatches = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed eiusmod tempor incidunt ut labore et dolore magna aliqua.";
  private const string LongTextNoMatches = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed eiusmod tempor incidunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquid ex ea commodi consequat. Quis aute iure reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint obcaecat cupiditat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum. Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed eiusmod tempor incidunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquid ex ea commodi consequat. Quis aute iure reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint obcaecat cupiditat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";
  private const string ShortTextTwoMatches = "Lorem ipsum dolor sit amet, www.example.org consectetur adipiscing elit, example@example.org sed eiusmod tempor incidunt ut labore et dolore magna aliqua.";
  private const string LongTextFourMatches = "Lorem ipsum dolor sit amet, www.example.org consectetur adipiscing elit, example@example.org sed eiusmod tempor incidunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquid ex ea commodi consequat. Quis aute iure reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint obcaecat cupiditat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum. Lorem ipsum dolor sit amet, www.example.org consectetur adipiscing elit, example@example.org sed eiusmod tempor incidunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquid ex ea commodi consequat. Quis aute iure reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint obcaecat cupiditat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";

  private readonly Linkernizer linkernizer = new();
  private readonly AutoLink autoLink = new();

  #region Linkernizer

  [Benchmark]
  public string LinkernizerShortTextNoMatches() => linkernizer.Linkernize(ShortTextNoMatches);

  [Benchmark]
  public string LinkernizerLongTextNoMatches() => linkernizer.Linkernize(LongTextNoMatches);

  [Benchmark]
  public string LinkernizerShortTextTwoMatches() => linkernizer.Linkernize(ShortTextTwoMatches);

  [Benchmark]
  public string LinkernizerLongTextFourMatches() => linkernizer.Linkernize(LongTextFourMatches);

  #endregion

  #region AutoLink

  [Benchmark]
  public string AutoLinkShortTextNoMatches() => autoLink.Link(ShortTextNoMatches);

  [Benchmark]
  public string AutoLinkLongTextNoMatches() => autoLink.Link(LongTextNoMatches);

  [Benchmark]
  public string AutoLinkShortTextTwoMatches() => autoLink.Link(ShortTextTwoMatches);

  [Benchmark]
  public string AutoLinkLongTextFourMatches() => autoLink.Link(LongTextFourMatches);

  #endregion

  #region TextHelper

  [Benchmark]
  public string TextHelperShortTextNoMatches() => ShortTextNoMatches.AutoLink();

  [Benchmark]
  public string TextHelperLongTextNoMatches() => LongTextNoMatches.AutoLink();

  [Benchmark]
  public string TextHelperShortTextTwoMatches() => ShortTextTwoMatches.AutoLink();

  [Benchmark]
  public string TextHelperLongTextFourMatches() => LongTextFourMatches.AutoLink();

  #endregion
}

/// <summary>
/// The main entry point for the console program.
/// </summary>
public static class Program
{
  /// <summary>
  /// Runs the actual benchmarks.
  /// </summary>
  public static void Main()
  {
    BenchmarkRunner.Run<Benchmarks>();
  }
}
