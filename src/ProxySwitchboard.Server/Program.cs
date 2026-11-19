using System.Net;
using System.Text.Json.Serialization.Metadata;
using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ProxySwitchboard.Interface;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.Models;

namespace ProxySwitchboard.Server;


[Command]
public partial class Program : ICommand
{
    [CommandOption("control-socket", EnvironmentVariable = "PROXY_SWITCHBOARD_SERVER_ARGUMENT_CONTROL_SOCKET")]
    public required string ControlSocket { get; set; }

    [CommandOption("proxy-password", EnvironmentVariable = "PROXY_SWITCHBOARD_SERVER_ARGUMENT_PROXY_PASSWORD")]
    public required string ProxyPassword { get; set; }

    [CommandOption("proxy-port", EnvironmentVariable = "PROXY_SWITCHBOARD_SERVER_ARGUMENT_PROXY_PORT")]
    public required int ProxyPort { get; set; }

    private readonly Lock locker = new();
    private ProxyStatus current = new(null, null, "Not set");

    public async ValueTask ExecuteAsync(IConsole console)
    {
        using var proxyServer = new ProxyServer(false);
        proxyServer.AddEndPoint(new ExplicitProxyEndPoint(new IPAddress([127, 0, 0, 1]), this.ProxyPort, false));
        proxyServer.ProxyBasicAuthenticateFunc = async (_, _, x) => x == this.ProxyPassword;
        proxyServer.Start(false);

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseKestrel();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenUnixSocket(this.ControlSocket);
        });
        builder.Services.AddRouting();
        builder.Services.ConfigureHttpJsonOptions(x =>
        {
            x.SerializerOptions.PropertyNamingPolicy = null;
            x.SerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
                ProxyJsonSerializerContext.Default,
                CommentedProxyJsonSerializerContext.Default,
                ProxyFingerprintJsonSerializerContext.Default,
                ProxyStatusJsonSerializerContext.Default
            );
        });
        var app = builder.Build();


        app.MapPost("/switch", ([FromBody] CommentedProxy proxy) =>
        {
            var externalProxy = proxy.Proxy?.ToExternalProxy();
            var status = new ProxyStatus(
                proxy.Proxy?.GetFingerprint(),
                proxy.Proxy?.ProxyType,
                proxy.Comment
            );
            lock (this.locker)
            {
                proxyServer.UpStreamHttpProxy = externalProxy;
                proxyServer.UpStreamHttpsProxy = externalProxy;
                this.current = status;
            }
            return new ProxyFingerprint(status.Fingerprint);
        });

        app.MapPost("/status", () => this.current);

        app.MapPost("/fingerprint", ([FromBody] Proxy? proxy) => new ProxyFingerprint(proxy?.GetFingerprint()));

        app.Run();
    }
}