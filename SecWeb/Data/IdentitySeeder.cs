using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecWeb.Data.Models;

namespace SecWeb.Data
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(
            IServiceProvider services)
        {
            RoleManager<IdentityRole> roleManager =
                services.GetRequiredService<RoleManager<IdentityRole>>();

            UserManager<ApplicationUser> userManager =
                services.GetRequiredService<UserManager<ApplicationUser>>();

            ApplicationDbContext db =
                services.GetRequiredService<ApplicationDbContext>();

            ILogger logger =
                services
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("IdentitySeeder");

            await EnsureRolesAsync(roleManager);

            List<ApplicationUser> users =
                await userManager.Users.ToListAsync();

            foreach (ApplicationUser user in users)
            {
                await NormalizeUserRolesAsync(
                    userManager,
                    user);
            }

            await RemoveLegacyRoleIfUnusedAsync(
                roleManager,
                userManager);

            ApplicationUser? permanentAdmin =
                await userManager.FindByEmailAsync(
                    AccountRoles.PermanentAdminEmail);

            if (permanentAdmin == null)
            {
                logger.LogWarning(
                    "The permanent Admin account {Email} does not exist yet.",
                    AccountRoles.PermanentAdminEmail);
            }

            ApplicationUser? projectOwner = permanentAdmin;

            if (projectOwner == null)
            {
                IList<ApplicationUser> admins =
                    await userManager.GetUsersInRoleAsync(
                        AccountRoles.Admin);

                projectOwner = admins.FirstOrDefault();
            }

            if (projectOwner != null)
            {
                await EnsureAviRoboticsProjectAsync(
                    db,
                    projectOwner.Id);
            }
            else
            {
                logger.LogWarning(
                    "AVI Robotics could not be seeded because no Admin account exists yet.");
            }
        }

        private static async Task EnsureRolesAsync(
            RoleManager<IdentityRole> roleManager)
        {
            foreach (string roleName in AccountRoles.All)
            {
                if (await roleManager.RoleExistsAsync(roleName))
                {
                    continue;
                }

                IdentityResult result =
                    await roleManager.CreateAsync(
                        new IdentityRole(roleName));

                ThrowIfFailed(
                    result,
                    $"Unable to create the role '{roleName}'.");
            }
        }

        private static async Task NormalizeUserRolesAsync(
            UserManager<ApplicationUser> userManager,
            ApplicationUser user)
        {
            IList<string> roles =
                await userManager.GetRolesAsync(user);

            bool changed = false;

            if (roles.Contains(
                AccountRoles.LegacySubsystemLead,
                StringComparer.OrdinalIgnoreCase))
            {
                IdentityResult removeLegacyResult =
                    await userManager.RemoveFromRoleAsync(
                        user,
                        AccountRoles.LegacySubsystemLead);

                ThrowIfFailed(
                    removeLegacyResult,
                    $"Unable to remove the legacy Subsystem Lead role from {user.Email}.");

                changed = true;
                roles = await userManager.GetRolesAsync(user);
            }

            bool isPermanentAdmin =
                string.Equals(
                    user.Email,
                    AccountRoles.PermanentAdminEmail,
                    StringComparison.OrdinalIgnoreCase);

            if (isPermanentAdmin)
            {
                List<string> rolesToRemove =
                    roles
                        .Where(role =>
                            !string.Equals(
                                role,
                                AccountRoles.Admin,
                                StringComparison.OrdinalIgnoreCase))
                        .ToList();

                if (rolesToRemove.Count > 0)
                {
                    IdentityResult removeResult =
                        await userManager.RemoveFromRolesAsync(
                            user,
                            rolesToRemove);

                    ThrowIfFailed(
                        removeResult,
                        "Unable to clean up the permanent Admin account roles.");

                    changed = true;
                }

                if (!await userManager.IsInRoleAsync(
                    user,
                    AccountRoles.Admin))
                {
                    IdentityResult addResult =
                        await userManager.AddToRoleAsync(
                            user,
                            AccountRoles.Admin);

                    ThrowIfFailed(
                        addResult,
                        "Unable to assign the permanent Admin role.");

                    changed = true;
                }
            }
            else if (roles.Contains(
                AccountRoles.Admin,
                StringComparer.OrdinalIgnoreCase))
            {
                if (roles.Contains(
                    AccountRoles.Member,
                    StringComparer.OrdinalIgnoreCase))
                {
                    IdentityResult removeMemberResult =
                        await userManager.RemoveFromRoleAsync(
                            user,
                            AccountRoles.Member);

                    ThrowIfFailed(
                        removeMemberResult,
                        $"Unable to remove the Member role from Admin {user.Email}.");

                    changed = true;
                }
            }
            else if (!roles.Contains(
                AccountRoles.Member,
                StringComparer.OrdinalIgnoreCase))
            {
                IdentityResult addMemberResult =
                    await userManager.AddToRoleAsync(
                        user,
                        AccountRoles.Member);

                ThrowIfFailed(
                    addMemberResult,
                    $"Unable to assign the Member role to {user.Email}.");

                changed = true;
            }

            if (changed)
            {
                IdentityResult stampResult =
                    await userManager.UpdateSecurityStampAsync(user);

                ThrowIfFailed(
                    stampResult,
                    $"Unable to update the security stamp for {user.Email}.");
            }
        }

        private static async Task RemoveLegacyRoleIfUnusedAsync(
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager)
        {
            IdentityRole? legacyRole =
                await roleManager.FindByNameAsync(
                    AccountRoles.LegacySubsystemLead);

            if (legacyRole == null)
            {
                return;
            }

            IList<ApplicationUser> remainingUsers =
                await userManager.GetUsersInRoleAsync(
                    AccountRoles.LegacySubsystemLead);

            if (remainingUsers.Count > 0)
            {
                return;
            }

            IdentityResult deleteResult =
                await roleManager.DeleteAsync(legacyRole);

            ThrowIfFailed(
                deleteResult,
                "Unable to remove the unused legacy Subsystem Lead role.");
        }

        private static async Task EnsureAviRoboticsProjectAsync(
            ApplicationDbContext db,
            string creatorUserId)
        {
            const string projectName = "AVI Robotics";

            Project? project =
                await db.Projects.FirstOrDefaultAsync(
                    existing => existing.Name == projectName);

            if (project == null)
            {
                db.Projects.Add(
                    new Project
                    {
                        Name = projectName,
                        Description = "Club-wide AVI Robotics activities.",
                        CreatedAt = DateTime.UtcNow,
                        CreatedByUserId = creatorUserId,
                        IsClubWide = true,
                        IsProtected = true
                    });

                await db.SaveChangesAsync();
                return;
            }

            bool changed = false;

            if (!project.IsClubWide)
            {
                project.IsClubWide = true;
                changed = true;
            }

            if (!project.IsProtected)
            {
                project.IsProtected = true;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(project.Description))
            {
                project.Description =
                    "Club-wide AVI Robotics activities.";

                changed = true;
            }

            if (changed)
            {
                await db.SaveChangesAsync();
            }
        }

        private static void ThrowIfFailed(
            IdentityResult result,
            string message)
        {
            if (result.Succeeded)
            {
                return;
            }

            string errors =
                string.Join(
                    "; ",
                    result.Errors.Select(
                        error => error.Description));

            throw new InvalidOperationException(
                $"{message} {errors}");
        }
    }
}
