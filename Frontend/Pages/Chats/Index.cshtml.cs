using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Frontend.Pages.Chats;

public class IndexModel : PageModel
{
    public UserInfo? CurrentUser { get; set; }  // Добавлен знак ?

    public void OnGet()
    {
        // В реальном проекте здесь будет загрузка пользователя из сессии
        // Пока берем из sessionStorage на клиенте
    }
}

public class UserInfo
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? PhoneNumber { get; set; }
}