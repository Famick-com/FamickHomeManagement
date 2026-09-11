namespace Famick.HomeManagement.Core.Exceptions;

/// <summary>
/// Thrown when a store integration cannot be used because it is not set up: the shopping
/// location has no integration or no external location id, or the plugin is missing or
/// unconfigured.
/// <para>
/// This exists so callers can tell "this store is not wired up" from "the wiring is fine
/// and the call went wrong". Both used to arrive as a bare
/// <see cref="InvalidOperationException"/>, and the API layer reported the pair as
/// "store integration not available" — which is the right answer for the first and a
/// misdiagnosis for the second, one that also hid genuine faults from the logs.
/// </para>
/// <para>
/// It derives from <see cref="InvalidOperationException"/> on purpose. That is what every
/// existing store-integration endpoint already catches, and those endpoints are right to
/// keep treating an unconfigured store the way they always have. Callers that need to tell
/// the two apart do it by catching this type <em>before</em> the broader one, which leaves
/// a plain <see cref="InvalidOperationException"/> meaning "a genuine fault on the plugin
/// path" at the sites that care.
/// </para>
/// </summary>
public class StoreIntegrationUnavailableException : InvalidOperationException
{
    public StoreIntegrationUnavailableException(string message)
        : base(message)
    {
    }

    public StoreIntegrationUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
