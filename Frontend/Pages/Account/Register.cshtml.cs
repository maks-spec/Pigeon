using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Frontend.Pages.Account;

public class RegisterModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int UserId { get; set; }
    
    [BindProperty]
    public string? Username { get; set; }  // Добавлен знак ? для nullable
    
    [BindProperty]
    public IFormFile? Avatar { get; set; }  // Добавлен знак ? для nullable
    
    public void OnGet()
    {
    }
}