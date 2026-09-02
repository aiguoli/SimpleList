using System;

namespace SimpleList.Core.Models;

public class ShareLink
{
    public string WebUrl { get; set; }
    public string Token { get; set; }
    public DateTimeOffset? Expiration { get; set; }
    public bool HasPassword { get; set; }
    public bool IsShared { get; set; }
}
