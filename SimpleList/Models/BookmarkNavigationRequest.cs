using SimpleList.Core.Models.DTO;
using SimpleList.ViewModels;

namespace SimpleList.Models;

public class BookmarkNavigationRequest
{
    public DriveViewModel Drive { get; set; }
    public BookmarkDTO Bookmark { get; set; }
}
