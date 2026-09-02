using System.Collections.Generic;

namespace SimpleList.Core.Models;

public class PageResult<T>
{
    public IReadOnlyList<T> Items { get; set; }
    public string NextPageToken { get; set; }
}
