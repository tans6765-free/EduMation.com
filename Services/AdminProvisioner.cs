using EduMation.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EduMation.Services;

public sealed class AdminProvisioner
{
    private readonly AdminProvisioningOptions _options;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<AdminProvisioner> _logger;

    public AdminProvisioner(IOptions<AdminProvisioningOptions> options, UserManager<ApplicationUser> users, ILogger<AdminProvisioner> logger)
    {
        _options = options.Value;
        _users = users;
        _logger = logger;
    }

    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hasEmail = !string.IsNullOrWhiteSpace(_options.Email);
        var hasPassword = !string.IsNullOrWhiteSpace(_options.Password);
        if (!hasEmail && !hasPassword) return;
        if (!hasEmail || !hasPassword || _options.Password.Length < 12 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(_options.Email))
            throw new InvalidOperationException("Configure AdminProvisioning:Email and a strong AdminProvisioning:Password together to provision the administrator.");

        var user = await _users.FindByEmailAsync(_options.Email);
        if (user == null)
        {
            user = new ApplicationUser { UserName = _options.Email, Email = _options.Email, EmailConfirmed = true };
            var created = await _users.CreateAsync(user, _options.Password);
            if (!created.Succeeded)
                throw new InvalidOperationException("Administrator provisioning failed. Check password policy and deployment logs.");
        }

        if (!await _users.IsInRoleAsync(user, "Admin"))
        {
            var assigned = await _users.AddToRoleAsync(user, "Admin");
            if (!assigned.Succeeded) throw new InvalidOperationException("Administrator role assignment failed.");
            _logger.LogInformation("Provisioned administrator access for configured account.");
        }
    }
}
