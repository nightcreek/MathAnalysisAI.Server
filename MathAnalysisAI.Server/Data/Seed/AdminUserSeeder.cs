using MathAnalysisAI.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Data.Seed
{
    public static class AdminUserSeeder
    {
        public static async Task<int?> SeedAdminAsync(
            ApplicationDbContext db,
            string adminUsername,
            string adminPassword,
            int bcryptWorkFactor = 12,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(adminUsername) || string.IsNullOrWhiteSpace(adminPassword))
                return null;

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword, bcryptWorkFactor);

            var existing = await db.AppUsers
                .FirstOrDefaultAsync(x => x.Username == adminUsername, cancellationToken);

            if (existing != null)
            {
                existing.RealName ??= "系统管理员";
                existing.Role = AppUserRole.Admin;
                existing.PasswordHash = passwordHash;
                await db.SaveChangesAsync(cancellationToken);
                return existing.Id;
            }

            var user = new AppUser
            {
                Username = adminUsername,
                PasswordHash = passwordHash,
                RealName = "系统管理员",
                Role = AppUserRole.Admin,
                CreatedAt = DateTime.UtcNow
            };

            db.AppUsers.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            return user.Id;
        }
    }
}
