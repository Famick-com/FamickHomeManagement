using System.Text.Json.Serialization;

namespace Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.Protocol;

/// <summary>
/// Mirror of the AuthProxy-side wire format (see
/// <c>Famick.AuthProxy.Tunnel.Protocol.TunnelEnvelope</c>). Kept in
/// lock-step manually because the AuthProxy repo isn't a project
/// dependency — the wire is small enough that duplication beats a
/// NuGet boundary. If you change anything here, change the AuthProxy
/// side too.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Challenge), "challenge")]
[JsonDerivedType(typeof(Handshake), "handshake")]
[JsonDerivedType(typeof(HandshakeOk), "handshake_ok")]
[JsonDerivedType(typeof(HandshakeFail), "handshake_fail")]
[JsonDerivedType(typeof(HttpRequestFrame), "http_request")]
[JsonDerivedType(typeof(HttpResponseFrame), "http_response")]
[JsonDerivedType(typeof(UserRegister), "user_register")]
[JsonDerivedType(typeof(UserUnregister), "user_unregister")]
[JsonDerivedType(typeof(UserSync), "user_sync")]
[JsonDerivedType(typeof(Ping), "ping")]
[JsonDerivedType(typeof(Pong), "pong")]
public abstract record TunnelEnvelope;

public sealed record Challenge(string NonceBase64Url) : TunnelEnvelope;

public sealed record Handshake(
    Guid HomeServerId,
    string PublicKeyPem,
    string NonceSignatureBase64Url,
    string? AgentVersion) : TunnelEnvelope;

public sealed record HandshakeOk : TunnelEnvelope;
public sealed record HandshakeFail(string Reason) : TunnelEnvelope;

public sealed record HttpRequestFrame(
    Guid RequestId,
    string Method,
    string Path,
    Dictionary<string, string[]> Headers,
    string? BodyBase64) : TunnelEnvelope;

public sealed record HttpResponseFrame(
    Guid RequestId,
    int Status,
    Dictionary<string, string[]> Headers,
    string? BodyBase64) : TunnelEnvelope;

public sealed record UserRegister(string Email) : TunnelEnvelope;
public sealed record UserUnregister(string Email) : TunnelEnvelope;
public sealed record UserSync(string[] Emails) : TunnelEnvelope;

public sealed record Ping(long Ts) : TunnelEnvelope;
public sealed record Pong(long Ts) : TunnelEnvelope;
