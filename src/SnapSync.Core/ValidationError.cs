namespace SnapSync.Core;

/// <summary>
/// Represents a validation error in a synchronization configuration.
/// </summary>
/// <param name="PropertyName">The name of the property or component with an error.</param>
/// <param name="Message">The description of the validation error.</param>
public sealed record ValidationError(string PropertyName, string Message);
