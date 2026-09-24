using EduMation.Models;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace EduMation.Controllers
{
    public class UserManager
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserManager(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<ApplicationUser> CreateAsync(ApplicationUser user, string password)
        {
            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                return user;
            }
            throw new InvalidOperationException("User creation failed.");
        }

        public async Task<ApplicationUser?> FindByNameAsync(string username)
        {
            return await _userManager.FindByNameAsync(username);
        }
    }
}