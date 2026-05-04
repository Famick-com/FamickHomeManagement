namespace Famick.HomeManagement.Logging.Redaction;

/// <summary>
/// Applies every registered <see cref="IRedactor"/> in sequence. Order is registration
/// order; later redactors see the output of earlier ones. The composite is itself an
/// <see cref="IRedactor"/> so it can be passed to logging infrastructure as a single
/// pluggable rule.
/// </summary>
public sealed class CompositeRedactor : IRedactor
{
    private readonly IRedactor[] _redactors;

    public CompositeRedactor(IEnumerable<IRedactor> redactors)
    {
        // Materialize so multiple invocations see a stable order even if the
        // underlying enumerable would re-evaluate.
        _redactors = redactors.Where(r => r is not CompositeRedactor).ToArray();
    }

    public string Redact(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var current = input;
        foreach (var redactor in _redactors)
        {
            current = redactor.Redact(current);
        }
        return current;
    }
}
