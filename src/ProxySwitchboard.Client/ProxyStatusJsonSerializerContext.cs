using System.Text.Json.Serialization;
using ProxySwitchboard.Interface;

namespace ProxySwitchboard.Client;

[JsonSourceGenerationOptions(RespectNullableAnnotations = true, UseStringEnumConverter = true, RespectRequiredConstructorParameters = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, WriteIndented = true)]
[JsonSerializable(typeof(ProxyStatus))]
partial class ProxyStatusJsonSerializerContext : JsonSerializerContext
{
}