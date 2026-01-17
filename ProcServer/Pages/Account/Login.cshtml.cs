using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProcServer.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace ProcServer.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly IUserRepository userRepository;

        [TempData]
		public string ErrorMessage { get; set; } = string.Empty;
		public string ReturnUrl { get; set; } = string.Empty;
		[BindProperty, Required]
		public string Username { get; set; } = string.Empty;
		[BindProperty, DataType(DataType.Password)]
		public string Password { get; set; } = string.Empty;

        public LoginModel(IUserRepository userRepository)
        {
            this.userRepository = userRepository;
        }

        public void OnGet(string? returnUrl = null)
        {
			if (!string.IsNullOrEmpty(ErrorMessage))
			{
				ModelState.AddModelError(string.Empty, ErrorMessage);
			}

			returnUrl = returnUrl ?? Url.Content("~/");

			ReturnUrl = returnUrl;
		}

		public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
		{
			returnUrl = returnUrl ?? Url.Content("~/");

			if (ModelState.IsValid)
			{
				var user = await userRepository.GetByUsernameAndPassword(Username, Password);

				if (user != null)
				{
					//var claims = new List<Claim> { new Claim(ClaimTypes.Name, Username) };
					var identity = userRepository.CreateIdentity(user); // new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
					await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

					return Redirect(returnUrl);
				}

				ModelState.AddModelError(string.Empty, "Invalid login attempt.");
			}

			// If we got this far, something failed, redisplay form
			return Page();
		}
	}
}
