using Microsoft.EntityFrameworkCore;
using Tau.KeyVault.Data;
using Tau.KeyVault.Models;

namespace Tau.KeyVault.Services;

public class AuthService
{
    private readonly AppDbContext _db;

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AppUser?> ValidateAsync(string username, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null) return null;
        return DbSeeder.VerifyPassword(password, user.PasswordHash) ? user : null;
    }

    public async Task<bool> ChangePasswordAsync(string username, string currentPassword, string newPassword)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null || !DbSeeder.VerifyPassword(currentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = DbSeeder.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        return true;
    }
}
