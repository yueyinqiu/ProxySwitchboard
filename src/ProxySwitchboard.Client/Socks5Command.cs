using System.Net.Http.Json;
using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using ProxySwitchboard.Interface;

namespace ProxySwitchboard.Client;


[Command("socks5")]
partial class Socks5Command : ICommand
{
    [CommandOption("host-name", 'H', EnvironmentVariable = "PROXY_SWITCHBOARD_ARGUMENT_HOST_NAME")]
    public required string HostName { get; set; }

    [CommandOption("port", 'P', EnvironmentVariable = "PROXY_SWITCHBOARD_ARGUMENT_PORT")]
    public required int Port { get; set; }

    [CommandOption("user-name", 'u', EnvironmentVariable = "PROXY_SWITCHBOARD_ARGUMENT_USER_NAME")]
    public string? UserName { get; set; } = null;

    [CommandOption("password", 'p', EnvironmentVariable = "PROXY_SWITCHBOARD_ARGUMENT_PASSWORD")]
    public string? Password { get; set; } = null;

    [CommandOption("proxy-dns-requests", EnvironmentVariable = "PROXY_SWITCHBOARD_ARGUMENT_PROXY_DNS_REQUESTS")]
    public bool ProxyDnsRequests { get; set; } = true;

    [CommandOption("bypass-localhost", EnvironmentVariable = "PROXY_SWITCHBOARD_ARGUMENT_BYPASS_LOCALHOST")]
    public bool BypassLocalhost { get; set; } = false;

    [CommandOption("comment", EnvironmentVariable = "PROXY_SWITCHBOARD_ARGUMENT_COMMENT")]
    public string Comment { get; set; } = "";

    [CommandOption("dry-run", EnvironmentVariable = "PROXY_SWITCHBOARD_ARGUMENT_DRY_RUN")]
    public bool DryRun { get; set; } = false;

    [CommandOption("json", EnvironmentVariable = "PROXY_SWITCHBOARD_ARGUMENT_JSON")]
    public bool Json { get; set; } = false;
    
    [CommandOption("control-socket", EnvironmentVariable = "PROXY_SWITCHBOARD_ARGUMENT_CONTROL_SOCKET")]
    public string ControlSocket { get; set; } = "/run/proxy-switchboard.sock";

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var proxy = new Proxy(
            ProxyType.Socks5,
            this.HostName, this.Port,
            this.UserName, this.Password,
            this.ProxyDnsRequests, this.BypassLocalhost, null
        );
        var client = new ProxySwitchboardClient(ControlSocket);
        var commentedProxy = new CommentedProxy(proxy, this.Comment);
        var status = await (DryRun ?
            client.FingerprintAsync(commentedProxy) :
            client.SwitchAsync(commentedProxy)
        );
        console.WriteProxyStatus(status, this.Json);
    }
}