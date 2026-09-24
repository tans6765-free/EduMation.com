using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using EduMation.Models;

namespace EduMation.Controllers
{
    public class SignInManager
    {
        private readonly SignInManager<ApplicationUser> _signInManager;

        public SignInManager(SignInManager<ApplicationUser> signInManager)
        {
            _signInManager = signInManager;
        }

        public async Task SignInAsync(ApplicationUser user)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
        }

        public async Task SignOutAsync()
        {
            await _signInManager.SignOutAsync();
        }
    }
}