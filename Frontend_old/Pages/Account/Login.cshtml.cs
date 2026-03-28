using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Frontend.Pages.Account;

public class LoginModel : PageModel
{
    [BindProperty]
    public string PhoneNumber { get; set; }
    
    public void OnGet()
    {
    }
}