using System.Text.Json.Serialization;
using ProxySwitchboard.Interface;

namespace ProxySwitchboard.Server;

[JsonSourceGenerationOptions(RespectNullableAnnotations = true, UseStringEnumConverter = true, RespectRequiredConstructorParameters = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ProxyStatus))]
partial class ProxyStatusJsonSerializerContext : JsonSerializerContext
{
}