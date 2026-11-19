using System.Text.Json.Serialization;
using ProxySwitchboard.Interface;

namespace ProxySwitchboard.Server;

[JsonSourceGenerationOptions(RespectNullableAnnotations = true, UseStringEnumConverter = true, RespectRequiredConstructorParameters = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ProxyFingerprint))]
partial class ProxyFingerprintJsonSerializerContext : JsonSerializerContext
{
}