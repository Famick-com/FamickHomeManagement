namespace Famick.HomeManagement.Logging.Redaction;

/// <summary>
/// Replaces sensitive substrings in a log message with a redaction marker.
///
/// Implementations are registered as singletons and applied in registration order
/// inside <see cref="RedactingLoggerProvider"/>. Each redactor is responsible for one
/// concern (one regex or one header/query rule) so the rule set is composable and
/// the unit-test surface stays small.
/// </summary>
public interface IRedactor
{
    string Redact(string input);
}
