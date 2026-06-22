using System.Buffers;

namespace Linkernizer.Internal;

/// <summary>
/// Represents the state passed to <see cref="string.Create{TState}"/> for constructing the output string.
/// This is a ref struct so that the input can be carried as a span without materializing it as a string.
/// </summary>
/// <param name="input">The complete input text.</param>
/// <param name="replacements">The replacements to be made.</param>
/// <param name="defaultScheme">The default scheme to prepend for links without a scheme.</param>
/// <param name="openingTagEndExternal">The opening tag end to use for external links.</param>
internal readonly ref struct State(
  ReadOnlySpan<char> input,
  ReadOnlySpan<Replacement> replacements,
  string defaultScheme,
  string openingTagEndExternal)
{
  /// <summary>The complete input text.</summary>
  public ReadOnlySpan<char> Input { get; } = input;

  /// <summary>The replacements to be made.</summary>
  public ReadOnlySpan<Replacement> Replacements { get; } = replacements;

  /// <summary>The default scheme to prepend for links without a scheme.</summary>
  public string DefaultScheme { get; } = defaultScheme;

  /// <summary>The opening tag end to use for external links.</summary>
  public string OpeningTagEndExternal { get; } = openingTagEndExternal;
}

/// <summary>
/// Represents a part of the original input that will be replaced.
/// </summary>
/// <param name="Offset">The offset of the match from the start of the original input.</param>
/// <param name="Length">The length of the match in the original input.</param>
/// <param name="Type">The type of the match that determines how the match will be replaced in the output.</param>
internal readonly record struct Replacement(int Offset, ushort Length, ReplacementType Type);

/// <summary>
/// Represents the type of match.
/// </summary>
internal enum ReplacementType : byte
{
  InternalWithScheme,
  InternalWithoutScheme,
  ExternalWithScheme,
  ExternalWithoutScheme,
  EmailWithScheme,
  EmailWithoutScheme
}

/// <summary>
/// Represents a growable collection of replacements that starts out in a
/// caller-provided (usually stack-allocated) buffer and only grows into
/// pooled arrays for inputs with an excessive number of links.
/// </summary>
/// <param name="initialBuffer">The initial (usually stack-allocated) buffer.</param>
internal ref struct ReplacementList(Span<Replacement> initialBuffer)
{
  private Span<Replacement> _buffer = initialBuffer;
  private Replacement[]? _rentedArray;
  private int _count;

  /// <summary>The replacements added so far.</summary>
  public readonly ReadOnlySpan<Replacement> Replacements => _buffer[.._count];

  /// <summary>
  /// Adds the given replacement to the buffer and grows it beforehand when it is full.
  /// </summary>
  /// <param name="replacement">The replacement to add.</param>
  public void Add(Replacement replacement)
  {
    if (_count == _buffer.Length)
      Grow();

    _buffer[_count++] = replacement;
  }

  /// <summary>
  /// Returns the rented array back to the pool in case the buffer was grown.
  /// The list must not be used after it has been disposed.
  /// </summary>
  public readonly void Dispose()
  {
    if (_rentedArray is not null)
      ArrayPool<Replacement>.Shared.Return(_rentedArray);
  }

  /// <summary>
  /// Moves the contents of the buffer into a larger array rented from the pool
  /// and returns the previously rented array in case the buffer was grown before.
  /// </summary>
  private void Grow()
  {
    var rentedArray = ArrayPool<Replacement>.Shared.Rent(_buffer.Length * 2);
    _buffer.CopyTo(rentedArray);

    if (_rentedArray is not null)
      ArrayPool<Replacement>.Shared.Return(_rentedArray);

    _buffer = rentedArray;
    _rentedArray = rentedArray;
  }
}
