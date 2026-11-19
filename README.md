# ProxySwitchboard

A local HTTP proxy whose upstream can be switched on the fly.

ProxySwitchboard runs a small proxy on `127.0.0.1` that always stays in place; what *changes* is the upstream it forwards to. Point your long-running service at it once, then switch between HTTP / SOCKS4 / SOCKS5 / direct without ever touching that service again.

## Why

There are two situations where this is useful:

1. **The upstream isn't known until after deployment.** Tools like mihomo (Clash Meta) can switch proxies, but they want you to pre-configure a fixed list of selectors up front. If you're deploying first and only learn the actual proxy (host, port, credentials) *later*, you need to be able to point at it at runtime — not bake it into config ahead of time.

2. **The consumer is a fixed service you don't want to reconfigure.** Some daemons (like `nix-daemon`) get their proxy from the service environment, so changing the proxy used to mean editing config, rebuilding, or restarting. By pointing that daemon once at a fixed local address, you can switch the upstream as often as you like without touching the daemon — or even needing any privelege on it.

## How it works

```
 your service
     │   http_proxy = http://user:password@127.0.0.1:<port>/
     ▼
 ┌──────────────────────────────────────────┐
 │ ProxySwitchboard.Server (local proxy)    │
 │   listens on 127.0.0.1, Basic-auth       │
 │   control socket (Unix domain socket)    │
 └──────────────────────────────────────────┘
     │  upstream switchable at runtime
     ▼
 HTTP / SOCKS4 / SOCKS5 / direct  ──►  internet
```

- The **server** is a plain proxy: it binds `127.0.0.1:<port>` (Basic-auth protected) plus a Unix control socket. It does **not** need root — no rewriting of downstream config, no restarting services.
- The **client** talks to the control socket and tells the server which upstream to use. Only members of a configured group can do this — enforced purely by the Unix socket's own file permissions.
- Each switch returns a **fingerprint** (SHA-256 of the upstream config) so you can verify at any time which upstream is active, without leaking credentials.

## Installation

For Nix/NixOS, check <https://github.com/yueyinqiu/ProxySwitchboard-Nix>.

For other systems, grab a binary from the [Releases](https://github.com/yueyinqiu/ProxySwitchboard/releases) page — the `*-linux-x64` binaries are single-file AOT executables and `*-linux-arm64` are self-contained; none of them need a .NET runtime. There are also `.zip` archives (framework-dependent, complete with symbols) for when you already have a .NET runtime installed.

## Usage

### Server

The server takes three options (all required):

| Option | Environment variable | Meaning |
| --- | --- | --- |
| `--control-socket <path>` | `PROXY_SWITCHBOARD_SERVER_ARGUMENT_CONTROL_SOCKET` | Unix socket the control HTTP server listens on |
| `--proxy-password <password>` | `PROXY_SWITCHBOARD_SERVER_ARGUMENT_PROXY_PASSWORD` | Basic-auth password for the local proxy |
| `--proxy-port <port>` | `PROXY_SWITCHBOARD_SERVER_ARGUMENT_PROXY_PORT` | TCP port the local proxy listens on; `0` means a random port |

On startup it prints two `key=value` lines, so the launcher can discover the actual values:

```
ProxyPort=37137
ControlSocket=/run/proxy-switchboard.sock
```

> **Security:** pass `--proxy-password` via the `PROXY_SWITCHBOARD_SERVER_ARGUMENT_PROXY_PASSWORD` environment variable, not on the command line — a password given as an argument is visible in `ps`.

### Client

The client must be a member of the group that owns the control socket. Commands:

```sh
ProxySwitchboard.Client http   -H <host> -P <port> --control-socket <path>  [--comment <text>] [--dry-run] [--json]
ProxySwitchboard.Client socks5 -H <host> -P <port> --control-socket <path>  [--comment <text>] [--dry-run] [--json]
ProxySwitchboard.Client direct --control-socket <path>  [--comment <text>] [--json]
ProxySwitchboard.Client from-json --control-socket <path>  [--dry-run] [--json]
ProxySwitchboard.Client status --control-socket <path>  [--json]
```

- `http` / `socks5` / `direct` / `from-json` switch the upstream and print the new state.
- `--dry-run` computes and prints the state without actually switching.
- `status` re-prints the current state.
- By default the state prints as two lines (`Type:HASH`, then the `Comment`); `--json` prints an object.

`from-json` reads a `CommentedProxy` — a `Proxy` plus a `Comment` — from stdin:

```json
{
  "Proxy": {
    "ProxyType": "Http",
    "HostName": "192.168.1.2",
    "Port": 7890,
    "UserName": null,
    "Password": null,
    "ProxyDnsRequests": true,
    "BypassLocalhost": false,
    "NextHop": null
  },
  "Comment": "home-clash"
}
```

`ProxyType` is `Http` / `Socks4` / `Socks5`; `NextHop` optionally chains another `Proxy`; `"Proxy": null` means direct; every field is required.

> **Security:** never pass a password on the command line — options like `-p` are visible in `ps`. Use the `PROXY_SWITCHBOARD_ARGUMENT_PASSWORD` environment variable (or `from-json` on stdin) instead.

## Security model

- The local proxy binds `127.0.0.1` and requires Basic auth, so only consumers that know the password can go through it.
- The control socket's permissions restrict who can switch the upstream (typically a dedicated group).
- The server needs no root: everything privileged (generating the password, writing any downstream config, socket ownership, restarts) belongs to the launcher — e.g. a systemd unit — not the server process.

## License

[MIT](LICENSE)

---

The documentation is AI-generated.