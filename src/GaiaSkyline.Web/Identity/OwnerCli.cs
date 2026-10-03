using GaiaSkyline.Infrastructure.Identity;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Web.Identity;

/// <summary>
/// One-off command: <c>dotnet run -- create-owner --email you@example.com --password ****</c>.
/// Applies migrations, ensures the roles exist and provisions the Owner. The password can also come
/// from the <c>Owner:Password</c> user secret so it never lands in shell history. Never echoes secrets.
/// </summary>
public static class OwnerCli
{
    public const string CommandName = "create-owner";

    public static async Task<int> RunAsync(WebApplication app, string[] args)
    {
        var email = GetArg(args, "--email") ?? app.Configuration["Owner:Email"];
        var password = GetArg(args, "--password") ?? app.Configuration["Owner:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await Console.Error.WriteLineAsync(
                "Usage: create-owner --email <email> --password <password>  " +
                "(password may instead be set as the Owner:Password user secret).");
            return 1;
        }

        using var scope = app.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await database.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        var created = await seeder.EnsureOwnerAsync(email, password);

        Console.WriteLine(created
            ? $"Owner created: {email}"
            : $"Owner already existed: {email} (role ensured).");
        return 0;
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
