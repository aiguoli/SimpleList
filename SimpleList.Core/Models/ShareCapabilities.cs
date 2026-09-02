namespace SimpleList.Core.Models;

public sealed record ShareCapabilities(
    bool CanCreatePublicLink,
    bool SupportsPassword,
    bool SupportsExpiration)
{
    public static ShareCapabilities Unsupported { get; } = new(false, false, false);
}
