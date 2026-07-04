using Microsoft.Extensions.Options;

namespace Linkernizer.DependencyInjection;

/// <summary>
/// Validates <see cref="LinkernizerOptions"/> for the options infrastructure by delegating
/// to the same validation that the constructor uses, so that the dependency injection
/// integration and the core library can never disagree on which options are valid.
/// </summary>
internal sealed class LinkernizerOptionsValidator : IValidateOptions<LinkernizerOptions>
{
  /// <summary>
  /// Validates the given options using the core validation rules.
  /// </summary>
  /// <param name="name">The name of the options instance being validated (unused).</param>
  /// <param name="options">The options to validate.</param>
  /// <returns>A successful result when the options are valid, otherwise a failure result.</returns>
  public ValidateOptionsResult Validate(string? name, LinkernizerOptions options)
  {
    var error = options.Validate();
    return error is null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(error);
  }
}
