using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Frontend.Pages.Chats;

public class IndexModel : PageModel
{
    public UserInfo CurrentUser { get; set; }
    
    public void OnGet()
    {
        // В реальном проекте здесь получаем данные пользователя из сессии
        CurrentUser = new UserInfo
        {
            Id = 1,
            Username = "User",
            AvatarUrl = null
        };
    }
}

public class UserInfo
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string AvatarUrl { get; set; }
}