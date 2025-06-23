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
- It has **only one dependency** (to [Microsoft.Extensions.ObjectPool](https://www.nuget.org/packages/microsoft.extensions.objectpool/))
- It **provides an interface** so that it can be used via dependency injection

However, there are also some limitations:
- Links are not validated against a full list of [top-level domains](https://en.wikipedia.org/wiki/List_of_Internet_top-level_domains) (as this list will be outdated quickly)
- Links are not validated against a full list of [URI schemes](https://en.wikipedia.org/wiki/List_of_URI_schemes) (as these could change quickly as well)
- Telephone numbers are not linked automatically (as this usually generates too many false positives for my taste)
- It assumes the input does not already contain HTML (and will likely lead to invalid markup if this is not the case)

## Installation

The library can be used like any other NuGet package, for example:
```PowerShell
> dotnet add package Linkernizer --version 1.0.0
```

You can find more information on [https://www.nuget.org/packages/Linkernizer](https://www.nuget.org/packages/Linkernizer).

## Benchmarks

The benchmarks compare the following libraries in their default configuration:
- [Linkernizer](https://github.com/sebastianlay/Linkernizer) (1.0.0)
- [AutoLink](https://github.com/mganss/AutoLink) (2.0.5)
- [TextHelper](https://github.com/tylermercier/TextHelper) (0.2.0)

The execution time (excluding initial setup):

![Execution time](https://raw.githubusercontent.com/sebastianlay/Linkernizer/refs/heads/main/images/execution-time.svg)

The memory allocated (excluding initial setup):

![Memory allocated](https://raw.githubusercontent.com/sebastianlay/Linkernizer/refs/heads/main/images/memory-allocated.svg)

Keep in mind that the exact values will vary from run to run.
```
| Method                         | Mean         | Error      | StdDev     | Gen0   | Allocated |
|------------------------------- |-------------:|-----------:|-----------:|-------:|----------:|
| LinkernizerShortTextNoMatches  |     10.04 ns |   0.031 ns |   0.028 ns |      - |         - |
| LinkernizerLongTextNoMatches   |     38.89 ns |   0.104 ns |   0.092 ns |      - |         - |
| LinkernizerShortTextTwoMatches |    644.91 ns |   4.310 ns |   3.599 ns | 0.1411 |     592 B |
| LinkernizerLongTextFourMatches |  3,248.53 ns |  15.657 ns |  13.880 ns | 0.5493 |    2312 B |
|------------------------------- |-------------:|-----------:|-----------:|-------:|----------:|
| AutoLinkShortTextNoMatches     |  1,112.75 ns |   3.894 ns |   3.643 ns | 0.0210 |      88 B |
| AutoLinkLongTextNoMatches      |  8,240.50 ns |  63.245 ns |  56.065 ns | 0.0153 |      88 B |
| AutoLinkShortTextTwoMatches    |  1,752.95 ns |   6.943 ns |   5.798 ns | 0.2937 |    1232 B |
| AutoLinkLongTextFourMatches    |  9,930.48 ns | 103.320 ns |  96.645 ns | 2.0142 |    8448 B |
|------------------------------- |-------------:|-----------:|-----------:|-------:|----------:|
| TextHelperShortTextNoMatches   |  2,860.00 ns |   9.203 ns |   7.185 ns | 0.0381 |     168 B |
| TextHelperLongTextNoMatches    | 20,015.26 ns |  33.143 ns |  25.876 ns | 0.0305 |     168 B |
| TextHelperShortTextTwoMatches  |  3,962.34 ns |  35.980 ns |  30.045 ns | 0.2441 |    1040 B |
| TextHelperLongTextFourMatches  | 22,227.44 ns | 123.171 ns | 115.214 ns | 0.7324 |    3136 B |
```
You can verify the results by running [the same benchmarks](https://github.com/sebastianlay/Linkernizer/blob/main/Linkernizer.Benchmarks/Program.cs) on your machine.
