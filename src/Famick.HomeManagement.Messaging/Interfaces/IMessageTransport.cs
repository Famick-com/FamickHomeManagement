using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Messaging.Interfaces;

/// <summary>
/// Sends a rendered message through a specific transport channel (email, SMS, push, in-app).
/// Multiple transports are registered in DI and resolved as IEnumerable&lt;IMessageTransport&gt;.
/// </summary>
public interface IMessageTransport
{
    /// <summary>
    /// The transport channel this implementation handles.
    /// </summary>
    TransportChannel Channel { get; }

    /// <summary>
    /// Sends the rendered message through this transport channel.
    /// </summary>
    Task SendAsync(RenderedMessage message, CancellationToken cancellationToken = default);
}
