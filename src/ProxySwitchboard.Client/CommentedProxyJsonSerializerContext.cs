using System.Text.Json.Serialization;
using ProxySwitchboard.Interface;

namespace ProxySwitchboard.Client;

[JsonSourceGenerationOptions(RespectNullableAnnotations = true, UseStringEnumConverter = true, RespectRequiredConstructorParameters = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(CommentedProxy))]
partial class CommentedProxyJsonSerializerContext : JsonSerializerContext
{
}