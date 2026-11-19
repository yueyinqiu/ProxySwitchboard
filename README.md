# ProxySwitchboard

Switch the proxy used by the **Nix daemon** (`nix-daemon`) on the fly, without rebuilding your NixOS configuration or editing `/etc` by hand.

`nix-daemon` is normally started by systemd, and its proxy settings come from its service environment — so changing them used to require a configuration rebuild. `ProxySwitchboard` solves this by running a small local proxy that always stays in the environment, while the *upstream* it forwards to can be switched at runtime with a single command.

## Why not just configure the proxy in NixOS?

- Every switch (different proxy, different credentials, back to direct) would mean editing the config, rebuilding, and probably rebooting.
- A proxy is often **personal and private** (e.g. your own Clash/V2Ray with credentials), while the NixOS config is usually shared and admin-maintained — private details don't belong there.

So the NixOS side stays proxy-free and just runs the local proxy server. *Which* upstream to use is decided at runtime by any user in the `proxy-switchboard` group.

> **Caveat:** the *setting* is per-user, but the *effect* is global — there's only one `nix-daemon` per machine. While your proxy is active, other users building with that daemon go through it too. Unavoidable with a single shared daemon.

## How it works

The systemd service of `nix-daemon` always points at a **local proxy server** (`ProxySwitchboard.Server`). The local proxy is the *only* hop configured in the daemon's environment, so nothing has to be rewritten to switch proxies:

```
 nix-daemon
     │   http_proxy = http://user:password@127.0.0.1:<port>/
     ▼
 ┌─────────────────────────────────────────────┐
 │ ProxySwitchboard.Server (local proxy)         │
 │   listens on 127.0.0.1, Basic-auth protected│
 │   control socket: /run/proxy-switchboard.sock│
 └─────────────────────────────────────────────┘
     │  upstream switchable at runtime
     ▼
 external proxy (HTTP / SOCKS4 / SOCKS5)  ──►  internet
```

The flow is:

1. The server starts, writes a systemd **drop-in** for `nix-daemon.service` that sets `all_proxy`, `http_proxy` and `https_proxy` to the local proxy, and restarts `nix-daemon`.
2. You run the **client** (`ProxySwitchboard.Client`), which talks to the control socket over a Unix domain socket and tells the server which *upstream* proxy to use (HTTP, SOCKS4 or SOCKS5).
3. The server switches its upstream and everything `nix-daemon` does is proxied through your chosen proxy — instantly.
4. When the server stops, it removes the drop-in and restarts `nix-daemon` to restore direct access.

This means you can switch between a VPN, a public proxy, or "direct" as often as you like — no rebuild, no reboot, no manual file editing.

## Installation

### NixOS

Nix packaging lives in the separate [`ProxySwitchboard-Nix`](https://github.com/yueyinqiu/ProxySwitchboard-Nix) repository, which exposes packages and a NixOS module. Add it as a flake input and enable the module:

```nix
{
  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    proxy-switchboard.url = "github:yueyinqiu/ProxySwitchboard-Nix";
  };

  outputs = { nixpkgs, proxy-switchboard, ... }: {
    nixosConfigurations.your-host = nixpkgs.lib.nixosSystem {
      system = "x86_64-linux";
      modules = [
        proxy-switchboard.nixosModules.proxy-switchboard
        {
          services.proxy-switchboard.enable = true;

          # optional: also install a wrapped client on the system PATH
          services.proxy-switchboard.installClient = true;
        }
      ];
    };
  };
}
```

The module runs the server as a `systemd` service (`proxy-switchboard-server`) and creates the `proxy-switchboard` group.

> To avoid building from source, you can use the `yueyinqiu` Cachix binary cache:
>
> ```nix
> {
>   nix.settings.extra-substituters = [
>     "https://yueyinqiu.cachix.org"
>   ];
>   nix.settings.extra-trusted-public-keys = [
>     "yueyinqiu.cachix.org-1:iooLFYpS7e6KAU4+QM5Zoj6Tq76jRGo+kjeAbu8JxAc="
>   ];
> }
> ```

#### Module options

Options under `services.proxy-switchboard`:

| Name | Type | Default | Description |
| --- | --- | --- | --- |
| `enable` | bool | `false` | Whether to enable ProxySwitchboard |
| `package` | package | the flake's `proxy-switchboard-server` | The server package to run |
| `installClient` | bool | `false` | Also install a wrapped client into `environment.systemPackages` |
| `group` | str | `"proxy-switchboard"` | Group whose members can access the control socket |
| `controlSocket` | str | `"/run/proxy-switchboard.sock"` | Unix socket path the control server listens on |
| `proxyPort` | nullOr port | `null` | TCP port the local proxy listens on (`127.0.0.1`); random when `null` |
| `nixDaemonService` | nullOr str | `"nix-daemon"` | systemd service to configure; `null` disables daemon configuration |

The proxy password is always generated randomly per boot by the server.

### Other distributions

If you don't use NixOS, just create the `proxy-switchboard` group and run the server as root. Take Ubuntu (x86_64) as an example — on ARM64, use `ProxySwitchboard.Server-linux-arm64` instead:

```bash
sudo wget -O /usr/local/bin/ProxySwitchboard.Server https://github.com/yueyinqiu/ProxySwitchboard/releases/latest/download/ProxySwitchboard.Server-linux-x64
sudo chmod +x /usr/local/bin/ProxySwitchboard.Server

sudo groupadd -f proxy-switchboard

sudo tee /etc/systemd/system/proxy-switchboard-server.service > /dev/null << 'EOF'
[Unit]
Description=Nix Daemon Proxy Server

[Service]
ExecStart=/usr/local/bin/ProxySwitchboard.Server
Restart=on-failure
RestartSec=5s
ExecStartPre=/bin/rm -f /run/proxy-switchboard.sock

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable --now proxy-switchboard-server

sudo usermod -aG proxy-switchboard <trusted-users>    # won't take effect in current login session.
```

## Server options

The server binary accepts a few options (defaults shown). Every option can also be supplied via its environment variable; the command line takes precedence:

| Option                  | Environment variable                               | Default                   | Meaning                                                                                                                             |
| ----------------------- | -------------------------------------------------- | ------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| `--control-socket`      | `PROXY_SWITCHBOARD_SERVER_ARGUMENT_CONTROL_SOCKET`  | `/run/proxy-switchboard.sock` | Unix socket path the control HTTP server listens on                                                                            |
| `--control-group`       | `PROXY_SWITCHBOARD_SERVER_ARGUMENT_CONTROL_GROUP`   | `proxy-switchboard`        | Group that gets read/write access to the control socket                                                                             |
| `--proxy-password`      | `PROXY_SWITCHBOARD_SERVER_ARGUMENT_PROXY_PASSWORD`  | *(random, generated)*     | Basic-auth password for the local proxy. Random per boot by default; set it explicitly to keep it stable                            |
| `--proxy-port`          | `PROXY_SWITCHBOARD_SERVER_ARGUMENT_PROXY_PORT`      | `0` *(random)*            | TCP port the local proxy listens on (`127.0.0.1`)                                                                                   |
| `--nix-daemon-service`  | `PROXY_SWITCHBOARD_SERVER_ARGUMENT_NIX_DAEMON_SERVICE` | `nix-daemon`            | Name of the systemd service to configure. Pass the flag without a value (`--nix-daemon-service`) to disable daemon configuration |

## Client setup

The client (`ProxySwitchboard.Client`) is exposed by the same flake. The simplest way is to set `services.proxy-switchboard.installClient = true` (see above), which installs a wrapped client named `proxy-switchboard` with the control socket already preset.

To install the plain client per-user instead, e.g. in `home.packages`:

```nix
proxy-switchboard.packages.${system}.proxy-switchboard-client
```

Or run it directly without installing anything:

```console
$ nix run github:yueyinqiu/ProxySwitchboard-Nix#proxy-switchboard-client -- http -H 127.0.0.1 -P 7890
```

Or enter a shell that has it on `PATH`:

```console
$ nix shell github:yueyinqiu/ProxySwitchboard-Nix#proxy-switchboard-client
```

Or install it into your profile:

```console
$ nix profile install github:yueyinqiu/ProxySwitchboard-Nix#proxy-switchboard-client
```

## Usage

The client talks to the server over the control socket, so **you must be a member of the `proxy-switchboard` group** to use it.

> **Security:** never pass a password on the command line. Options like `-p` show up in `ps` and are visible to any local user. Use the `PROXY_SWITCHBOARD_ARGUMENT_PASSWORD` environment variable instead — the command line takes precedence if both are set:
>
> ```sh
> export PROXY_SWITCHBOARD_ARGUMENT_PASSWORD=secret
> ProxySwitchboard.Client http -H 127.0.0.1 -P 7890 -u user
> ```
>
> `from-json` reads its JSON from stdin, so it is safe to put passwords there.

### Switch to an HTTP proxy

```sh
ProxySwitchboard.Client http -H 127.0.0.1 -P 7890
```

### Switch to a SOCKS5 proxy

```sh
ProxySwitchboard.Client socks5 -H 192.168.1.2 -P 1080
```

### Switch back to direct access

```sh
ProxySwitchboard.Client direct
```

### Show or verify the current upstream

Every switch prints the new state in the same format as `status` below: the type (and fingerprint) on the first line, the `Comment` on the second — or an object with `--json`. The `Fingerprint` is a hex SHA-256 of the `Proxy` (credentials and `NextHop` included), so different configs give different fingerprints while no sensitive detail is revealed. Use `--dry-run` to print the state without actually switching.

```sh
ProxySwitchboard.Client status
```

re-prints the current state, by default as two lines:

```text
Http:1A2B3C…
clash
```

When direct, the first line is `Direct` (no fingerprint):

```text
Direct
home
```

(the `Comment` may be empty, in which case the second line is blank.)

With `--json`:

```json
{
  "Fingerprint": "1A2B3C…",
  "Type": "Http",
  "Comment": "clash"
}
```

(`Type` and `Fingerprint` are `null` when direct.)

### Advanced usage

Use `from-json` for full control — e.g. chaining two proxies (`NextHop`), or setting DNS proxy / localhost-bypass flags. It maps to a `CommentedProxy` (a `Proxy` plus a `Comment`) — see [`src/ProxySwitchboard.Interface`](src/ProxySwitchboard.Interface), with `ProxyType` being one of `Http`, `Socks4` or `Socks5`. Every field is required (nullable ones too — write them as `null`), and no extra fields are allowed. Set `Proxy` to `null` to switch to direct:

```sh
ProxySwitchboard.Client from-json <<'EOF'
{
  "Proxy": {
    "ProxyType": "Http",
    "HostName": "192.168.1.2",
    "Port": 7890,
    "UserName": null,
    "Password": null,
    "ProxyDnsRequests": true,
    "BypassLocalhost": false,
    "NextHop": {
      "ProxyType": "Http",
      "HostName": "192.168.1.3",
      "Port": 7890,
      "UserName": "user",
      "Password": "secret",
      "ProxyDnsRequests": true,
      "BypassLocalhost": true,
      "NextHop": null
    }
  },
  "Comment": "a two-hop chain"
}
EOF
```

### Client command reference

| Command | Options | Description |
| --- | --- | --- |
| `http` | `-H <host>`, `-P <port>`, `-u <user>`, `-p <password>`, `--proxy-dns-requests`, `--bypass-localhost`, `--comment <text>`, `--dry-run`, `--json`, `--control-socket <path>` | Use an HTTP(S) proxy upstream |
| `socks5` | same as `http` | Use a SOCKS5 proxy upstream |
| `direct` | `--comment <text>`, `--json`, `--control-socket <path>` | Remove the upstream proxy (go direct), optionally with a comment |
| `from-json` | `--dry-run`, `--json`, `--control-socket <path>` | Read a JSON `CommentedProxy` from stdin and set it as the upstream (supports `NextHop` chains) |
| `status` | `--json`, `--control-socket <path>` | Print the current state (`Type`/fingerprint on one line, `Comment` on the next), or JSON with `--json` |

Shared options (each can also be supplied via its environment variable; the command line takes precedence):

- `--host-name` / `-H`: upstream proxy host. Environment variable: `PROXY_SWITCHBOARD_ARGUMENT_HOST_NAME`.
- `--port` / `-P`: upstream proxy port. Environment variable: `PROXY_SWITCHBOARD_ARGUMENT_PORT`.
- `--user-name` / `-u`: upstream proxy username (optional). Environment variable: `PROXY_SWITCHBOARD_ARGUMENT_USER_NAME`.
- `--password` / `-p`: upstream proxy password (optional). Environment variable: `PROXY_SWITCHBOARD_ARGUMENT_PASSWORD`.
- `--proxy-dns-requests` (default `true`): let the proxy resolve DNS instead of the local machine. Environment variable: `PROXY_SWITCHBOARD_ARGUMENT_PROXY_DNS_REQUESTS`.
- `--bypass-localhost` (default `false`): do not proxy requests to localhost. Environment variable: `PROXY_SWITCHBOARD_ARGUMENT_BYPASS_LOCALHOST`.
- `--comment <text>`: free-form label attached to the current upstream — `direct` included (shown by `status`; never treated as a secret). Environment variable: `PROXY_SWITCHBOARD_ARGUMENT_COMMENT`.
- `--dry-run`: compute and print the resulting state without actually switching (`http` / `socks5` / `from-json` only). Environment variable: `PROXY_SWITCHBOARD_ARGUMENT_DRY_RUN`.
- `--json`: print the state as JSON instead of the plain two-line form. Environment variable: `PROXY_SWITCHBOARD_ARGUMENT_JSON`.
- `--control-socket` (default `/run/proxy-switchboard.sock`): path of the server's control socket, in case it was changed. Environment variable: `PROXY_SWITCHBOARD_ARGUMENT_CONTROL_SOCKET`.

## Rescue: proxy switched but not working

This should be next to impossible, but just in case: the switch to the proxy succeeded (`nix-daemon` restarted with the proxy env vars), but then the server crashed — and keeps crashing on restart. The control socket is gone, and `nix-daemon` is pointed at a dead proxy, so you can't even rebuild NixOS.

Don't worry. The proxy only reaches `nix-daemon` through a runtime systemd **drop-in**, so remove it as root and restart the daemon will save you:

```sh
sudo systemctl stop proxy-switchboard-server
sudo rm /run/systemd/system/nix-daemon.service.d/proxy-switchboard-02f2de3ae7134c999a969d2b8f6f2f46.conf
sudo systemctl daemon-reload
sudo systemctl restart nix-daemon
```

## Security notes

- The local proxy is bound to `127.0.0.1` and protected by Basic auth, but only the server knows the (random) password.
- Access to the control socket is restricted to the `proxy-switchboard` group, so only members can switch the proxy.

## License

[MIT](LICENSE)

---

The documentation is AI-generated.
