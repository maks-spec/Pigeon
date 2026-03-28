using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Frontend.Pages.Account;

public class VerifyCodeModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string PhoneNumber { get; set; }
    
    [BindProperty]
    public string Code { get; set; }
    
    public void OnGet()
    {
    }
}