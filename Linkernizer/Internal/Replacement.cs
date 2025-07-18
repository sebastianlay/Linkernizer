﻿namespace Linkernizer.Internal;

/// <summary>
/// Represents a part of the original input that will be replaced.
/// </summary>
internal readonly struct Replacement(int offset, int length, ReplacementType type)
{
  /// <summary>
  /// The offset to the start of the original input.
  /// </summary>
  internal int Offset { get; } = offset;

  /// <summary>
  /// The length of the match in the original input.
  /// </summary>
  internal int Length { get; } = length;

  /// <summary>
  /// The type of the match that determines how the match will be replaced in the output.
  /// </summary>
  internal ReplacementType Type { get; } = type;
}

/// <summary>
/// Represents the type of match.
/// </summary>
internal enum ReplacementType
{
  InternalWithScheme,
  InternalWithoutScheme,
  ExternalWithScheme,
  ExternalWithoutScheme,
  EmailWithScheme,
  EmailWithoutScheme
}
