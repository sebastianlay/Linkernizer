# Linkernizer

Welcome to Linkernizer, the (possibly best) .NET library for automatically linking URLs and email addresses.  
It takes in plain text and wraps anything it considers a link with the HTML markup for a hyperlink / anchor.

You can use it like this:
```c#
var linkernizer = new Linkernizer();
var input = "Visit us at www.example.org or email us at mail@example.org!";
var output = linkernizer.Linkernize(input);
// Visit us at <a href="https://www.example.org">www.example.org</a> or email us at <a href="mailto:mail@example.org">mail@example.org</a>!
```

You can configure a few additional options like this:
```c#
var linkernizer = new Linkernizer(options => {
  options.OpenExternalLinksInNewTab = true; // inserts target="_blank"
  options.InternalHost = "www.example.com"; // do not insert target="_blank" on these links
  options.DefaultScheme = "http://"; // use this scheme for links starting with www.
});
```

## Why should I use or not use Linkernizer?

You should use it because:
- It is **fast** (see the [benchmarks](#benchmarks) below for more details)
- It uses **little memory** (there are zero allocations after the setup when the input does not contain links)
- It has **only one dependency** (to [Microsoft.SourceLink.GitHub](https://www.nuget.org/packages/microsoft.sourcelink.github/), which is only a build-time dependency)
- It **provides an interface** so that it can be used via dependency injection

However, there are also some limitations:
- Links are not validated against a full list of [top-level domains](https://en.wikipedia.org/wiki/List_of_Internet_top-level_domains) (as this list will be outdated quickly)
- Links are not validated against a full list of [URI schemes](https://en.wikipedia.org/wiki/List_of_URI_schemes) (as these could change quickly as well)
- Telephone numbers are not linked automatically (as this usually generates too many false positives for my taste)
- It assumes the input does not already contain HTML (and will likely lead to invalid markup if this is not the case)

## Installation

The library can be used like any other NuGet package. For example:
```PowerShell
> dotnet add package Linkernizer
```

You can find more information on [https://www.nuget.org/packages/Linkernizer](https://www.nuget.org/packages/Linkernizer).

## Benchmarks

The benchmarks compare the following libraries in their default configuration:
- [Linkernizer](https://github.com/sebastianlay/Linkernizer) (1.0.6)
- [AutoLink](https://github.com/mganss/AutoLink) (2.0.5)
- [TextHelper](https://github.com/tylermercier/TextHelper) (0.2.0)

The execution time (excluding initial setup):

![Execution time](https://raw.githubusercontent.com/sebastianlay/Linkernizer/refs/heads/main/images/execution-time.svg)

The memory allocated (excluding initial setup):

![Memory allocated](https://raw.githubusercontent.com/sebastianlay/Linkernizer/refs/heads/main/images/memory-allocated.svg)

Keep in mind that the exact values will vary from run to run.
```
| Method                         | Mean          | Error       | StdDev      | Gen0   | Allocated |
|------------------------------- |--------------:|------------:|------------:|-------:|----------:|
| LinkernizerShortTextNoMatches  |      9.591 ns |   0.1126 ns |   0.0940 ns |      - |         - |
| LinkernizerLongTextNoMatches   |     31.349 ns |   0.3125 ns |   0.2923 ns |      - |         - |
| LinkernizerShortTextTwoMatches |    642.736 ns |  10.4223 ns |   9.7490 ns | 0.2747 |     576 B |
| LinkernizerLongTextFourMatches |  3,464.762 ns |  65.1401 ns |  66.8941 ns | 1.0948 |    2296 B |
|------------------------------- |--------------:|------------:|------------:|-------:|----------:|
| AutoLinkShortTextNoMatches     |    888.333 ns |  13.5546 ns |  12.6790 ns | 0.0420 |      88 B |
| AutoLinkLongTextNoMatches      |  6,579.750 ns |  83.2998 ns |  77.9186 ns | 0.0381 |      88 B |
| AutoLinkShortTextTwoMatches    |  1,485.751 ns |  20.6070 ns |  18.2676 ns | 0.5875 |    1232 B |
| AutoLinkLongTextFourMatches    |  8,308.058 ns | 137.4510 ns | 128.5717 ns | 4.0283 |    8448 B |
|------------------------------- |--------------:|------------:|------------:|-------:|----------:|
| TextHelperShortTextNoMatches   |  3,046.595 ns |  17.1640 ns |  16.0552 ns | 0.0801 |     168 B |
| TextHelperLongTextNoMatches    | 21,997.788 ns | 276.9698 ns | 259.0778 ns | 0.0610 |     168 B |
| TextHelperShortTextTwoMatches  |  4,308.261 ns |  40.2772 ns |  37.6753 ns | 0.4959 |    1040 B |
| TextHelperLongTextFourMatches  | 23,032.028 ns | 137.4207 ns | 114.7525 ns | 1.4954 |    3136 B |
```
You can verify the results by running [the same benchmarks](https://github.com/sebastianlay/Linkernizer/blob/main/Linkernizer.Benchmarks/Program.cs) on your machine.
