using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;

namespace ProxySwitchboard.Client;

[Command("status")]
partial class StatusCommand : ICommand
{
    [CommandOption("json", EnvironmentVariable = "PROXY_SWITCHBOARD_ARGUMENT_JSON")]
    public bool Json { get; set; } = false;

    [CommandOption("control-socket", EnvironmentVariable = "PROXY_SWITCHBOARD_ARGUMENT_CONTROL_SOCKET")]
    public required string ControlSocket { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var client = new ProxySwitchboardClient(ControlSocket);
        var status = await client.StatusAsync();
        console.WriteProxyStatus(status, this.Json);
    }
}