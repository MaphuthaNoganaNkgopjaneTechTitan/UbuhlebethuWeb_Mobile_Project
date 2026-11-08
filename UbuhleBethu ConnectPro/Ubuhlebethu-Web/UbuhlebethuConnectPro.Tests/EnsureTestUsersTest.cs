using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.WebApp.Data;
using Xunit;

namespace UbuhlebethuConnectPro.Tests
{
    public class EnsureTestUsersTest
    {
        private const string ConnectionString = "Host=ep-dark-bird-b1kvlfcb-pooler.c-5.eu-central-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=npg_rXyEz5DkcLx8;SslMode=Require;";

        [Fact]
        public async Task EnsureRequestedLoginCredentialsWork()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection();
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(ConnectionString));
            services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequiredLength = 6;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            var provider = services.BuildServiceProvider();
            var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();

            var usersToEnsure = new (string username, string email, string password, string role)[]
            {
                ("admin", "admin@connectpro.local", "Admin123!", "Admin"),
                ("manager", "manager@connectpro.local", "Manager123!", "Manager"),
                ("client", "client@connectpro.local", "Client123!", "Client"),
                ("office", "office@connectpro.local", "Office123!", "Office"),
                ("site", "site@connectpro.local", "Site123!", "SiteManager"),
                ("worker", "worker@connectpro.local", "Worker123!", "FieldWorker")
            };

            foreach (var req in usersToEnsure)
            {
                if (!await roleManager.RoleExistsAsync(req.role))
                    await roleManager.CreateAsync(new IdentityRole(req.role));

                var user = await userManager.FindByNameAsync(req.username);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = req.username,
                        Email = req.email,
                        EmailConfirmed = true,
                        IsSuperAdmin = (req.role == "Admin")
                    };
                    var res = await userManager.CreateAsync(user, req.password);
                    if (res.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, req.role);
                    }
                }
                else
                {
                    // Ensure password matches req.password
                    var check = await userManager.CheckPasswordAsync(user, req.password);
                    if (!check)
                    {
                        var token = await userManager.GeneratePasswordResetTokenAsync(user);
                        var resetRes = await userManager.ResetPasswordAsync(user, token, req.password);
                        Assert.True(resetRes.Succeeded);
                    }
                    if (!await userManager.IsInRoleAsync(user, req.role))
                    {
                        await userManager.AddToRoleAsync(user, req.role);
                    }
                }

                // Verify login succeeds
                var verifiedUser = await userManager.FindByNameAsync(req.username);
                Assert.NotNull(verifiedUser);
                Assert.True(await userManager.CheckPasswordAsync(verifiedUser, req.password));
            }
        }
    }
}
