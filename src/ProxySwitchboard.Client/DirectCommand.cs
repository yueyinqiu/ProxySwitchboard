using System.Net.Http.Json;
using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using ProxySwitchboard.Interface;

namespace ProxySwitchboard.Client;


[Command("direct")]
partial class DirectCommand : ICommand
{
    [CommandOption("comment", EnvironmentVariable = "PROXY_SWITCHBOARD_ARGUMENT_COMMENT")]
    public string Comment { get; set; } = "";

    [CommandOption("json", EnvironmentVariable = "PROXY_SWITCHBOARD_ARGUMENT_JSON")]
    public bool Json { get; set; } = false;

    [CommandOption("control-socket", EnvironmentVariable = "PROXY_SWITCHBOARD_ARGUMENT_CONTROL_SOCKET")]
    public required string ControlSocket { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var client = new ProxySwitchboardClient(ControlSocket);
        var status = await client.SwitchAsync(new CommentedProxy(null, this.Comment));
        console.WriteProxyStatus(status, this.Json);
    }
}