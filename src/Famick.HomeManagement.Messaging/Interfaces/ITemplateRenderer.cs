using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Messaging.Interfaces;

/// <summary>
/// Renders Mustache templates for a given message type and transport channel.
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// Renders the template for the specified message type and transport channel using the provided data.
    /// </summary>
    Task<string> RenderAsync(MessageType type, TransportChannel channel, IMessageData data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a template exists for the specified message type and transport channel.
    /// </summary>
    bool HasTemplate(MessageType type, TransportChannel channel);
}
