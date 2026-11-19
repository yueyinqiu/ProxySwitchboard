namespace ProxySwitchboard.Interface;

public sealed record ProxyStatus(
    string? Fingerprint,
    ProxyType? Type,
    string Comment
);