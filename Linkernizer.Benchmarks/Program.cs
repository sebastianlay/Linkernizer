using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Running;
using Ganss.Text;
using TextHelper;

namespace Linkernizer.Benchmarks;

/// <summary>
/// The values used in the benchmarks.
/// </summary>
internal static class Values
{
  internal const string ShortTextNoMatches = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed eiusmod tempor incidunt ut labore et dolore magna aliqua.";
  internal const string LongTextNoMatches = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed eiusmod tempor incidunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquid ex ea commodi consequat. Quis aute iure reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint obcaecat cupiditat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum. Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed eiusmod tempor incidunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquid ex ea commodi consequat. Quis aute iure reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint obcaecat cupiditat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";
  internal const string ShortTextTwoMatches = "Lorem ipsum dolor sit amet, www.example.org consectetur adipiscing elit, example@example.org sed eiusmod tempor incidunt ut labore et dolore magna aliqua.";
  internal const string LongTextFourMatches = "Lorem ipsum dolor sit amet, www.example.org consectetur adipiscing elit, example@example.org sed eiusmod tempor incidunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquid ex ea commodi consequat. Quis aute iure reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint obcaecat cupiditat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum. Lorem ipsum dolor sit amet, www.example.org consectetur adipiscing elit, example@example.org sed eiusmod tempor incidunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquid ex ea commodi consequat. Quis aute iure reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint obcaecat cupiditat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";
}

/// <summary>
/// The benchmarks for Linkernizer (2026) (https://github.com/sebastianlay/linkernizer).
/// </summary>
public class LinkernizerBenchmarks
{
  private readonly Linkernizer _linkernizer = new();

  [Benchmark]
  public string LinkernizerShortTextNoMatches() => _linkernizer.Linkernize(Values.ShortTextNoMatches);

  [Benchmark]
  public string LinkernizerLongTextNoMatches() => _linkernizer.Linkernize(Values.LongTextNoMatches);

  [Benchmark]
  public string LinkernizerShortTextTwoMatches() => _linkernizer.Linkernize(Values.ShortTextTwoMatches);

  [Benchmark]
  public string LinkernizerLongTextFourMatches() => _linkernizer.Linkernize(Values.LongTextFourMatches);
}

/// <summary>
/// The benchmarks for AutoLink (2015-2022) (https://github.com/mganss/AutoLink).
/// </summary>
public class AutoLinkBenchmarks
{
  private readonly AutoLink _autoLink = new();

  [Benchmark]
  public string AutoLinkShortTextNoMatches() => _autoLink.Link(Values.ShortTextNoMatches);

  [Benchmark]
  public string AutoLinkLongTextNoMatches() => _autoLink.Link(Values.LongTextNoMatches);

  [Benchmark]
  public string AutoLinkShortTextTwoMatches() => _autoLink.Link(Values.ShortTextTwoMatches);

  [Benchmark]
  public string AutoLinkLongTextFourMatches() => _autoLink.Link(Values.LongTextFourMatches);
}

/// <summary>
/// The benchmarks for TextHelper (2011) (https://github.com/tylermercier/TextHelper).
/// </summary>
public class TextHelperBenchmarks
{
  [Benchmark]
  public string TextHelperShortTextNoMatches() => Values.ShortTextNoMatches.AutoLink();

  [Benchmark]
  public string TextHelperLongTextNoMatches() => Values.LongTextNoMatches.AutoLink();

  [Benchmark]
  public string TextHelperShortTextTwoMatches() => Values.ShortTextTwoMatches.AutoLink();

  [Benchmark]
  public string TextHelperLongTextFourMatches() => Values.LongTextFourMatches.AutoLink();
}

/// <summary>
/// The main entry point for the console program.
/// </summary>
public static class Program
{
  /// <summary>
  /// Runs the actual benchmarks.
  /// </summary>
  public static void Main(string[] args)
  {
    var config = DefaultConfig.Instance.AddDiagnoser(MemoryDiagnoser.Default);

    BenchmarkSwitcher.FromTypes([
      typeof(LinkernizerBenchmarks),
      typeof(AutoLinkBenchmarks),
      typeof(TextHelperBenchmarks)
    ]).Run(args, config);
  }
}
