using Microsoft.AspNetCore.Identity;
using UbuhlebethuConnectPro.Core.Models;

namespace UbuhlebethuConnectPro.Api.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndUsersAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            string[] roles = { "Admin", "Office", "SiteManager", "FieldWorker", "Manager", "Client" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Demo accounts use well-known passwords, so they are only created in Development,
            // or elsewhere when Seed:DemoUsers is true AND Seed:DemoPassword is set to a private password.
            var cfg = serviceProvider.GetRequiredService<IConfiguration>();
            var env = serviceProvider.GetRequiredService<IHostEnvironment>();
            var demoPassword = cfg["Seed:DemoPassword"];
            bool seedDemo = cfg.GetValue<bool?>("Seed:DemoUsers") ?? env.IsDevelopment();
            if (!seedDemo) return;
            if (!env.IsDevelopment() && string.IsNullOrWhiteSpace(demoPassword))
            {
                Console.WriteLine("Demo users were not created: set Seed__DemoPassword to create them outside Development.");
                return;
            }

            var users = new (string username, string email, string password, string role)[]
            {
                ("St10456109", "St10456109@myemeris.edu.za", "St10456109", "Admin"),
                ("St10384345", "St10384345@myemeris.edu.za", "St10384345", "Office"),
                ("St10450570", "St10450570@myemeris.edu.za", "St10450570", "SiteManager"),
                ("St10461617", "St10461617@myemeris.edu.za", "St10461617", "FieldWorker"),
                ("St10377354", "St10377354@myemeris.edu.za", "St10377354", "Manager"),
                ("St10452632", "St10452632@myemeris.edu.za", "St10452632", "Client"),
                ("admin", "admin@connectpro.local", "Admin123!", "Admin"),
                ("office", "office@connectpro.local", "Office123!", "Office"),
                ("client", "client@connectpro.local", "Client123!", "Client"),
                ("manager", "manager@connectpro.local", "Manager123!", "Manager"),
                ("site", "site@connectpro.local", "Site123!", "SiteManager"),
                ("worker", "worker@connectpro.local", "Worker123!", "FieldWorker")
            };

            foreach (var u in users)
            {
              try
              {
                // never fail on a role that has not been created yet
                if (!await roleManager.RoleExistsAsync(u.role))
                    await roleManager.CreateAsync(new IdentityRole(u.role));

                var user = await userManager.FindByNameAsync(u.username);
                if (user == null)
                {
                    var newUser = new ApplicationUser
                    {
                        UserName = u.username,
                        Email = u.email,
                        EmailConfirmed = true,
                        IsSuperAdmin = (u.role == "Admin")
                    };

                    var result = await userManager.CreateAsync(newUser, string.IsNullOrWhiteSpace(demoPassword) ? u.password : demoPassword);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(newUser, u.role);
                    }
                }
                else
                {
                    if (!await userManager.IsInRoleAsync(user, u.role))
                    {
                        await userManager.AddToRoleAsync(user, u.role);
                    }
                }
              }
              catch (Exception ex)
              {
                  Console.WriteLine($"Seeding user '{u.username}' failed: {ex.Message}");
              }
            }
        }
    }
}
