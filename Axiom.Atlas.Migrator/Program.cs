using Axiom.Atlas.Domain.Entities.Users;
using Axiom.Atlas.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var command = args.FirstOrDefault()?.Trim().ToLowerInvariant() ?? "migrate";
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("A connection string de banco de dados não foi configurada.");
    return 2;
}

var services = new ServiceCollection();
services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
services.AddIdentityCore<User>(options => ConfigureIdentity(options))
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>();

await using var serviceProvider = services.BuildServiceProvider();

try
{
    switch (command)
    {
        case "migrate":
            await ApplyMigrationsAsync(serviceProvider);
            return 0;
        case "bootstrap-admin":
            return await BootstrapAdministratorAsync(serviceProvider);
        default:
            Console.Error.WriteLine("Comando inválido. Use 'migrate' ou 'bootstrap-admin'.");
            return 2;
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Falha no processo '{command}': {exception.GetType().Name}.");
    return 1;
}

static void ConfigureIdentity(IdentityOptions options)
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 10;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}

static async Task ApplyMigrationsAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
    Console.WriteLine("Migrations aplicadas com sucesso.");
}

static async Task<int> BootstrapAdministratorAsync(IServiceProvider services)
{
    var username = RequireEnvironment("BOOTSTRAP_ADMIN_USERNAME");
    var email = RequireEnvironment("BOOTSTRAP_ADMIN_EMAIL");
    var fullName = RequireEnvironment("BOOTSTRAP_ADMIN_FULL_NAME");
    var password = RequireEnvironment("BOOTSTRAP_ADMIN_PASSWORD");

    await using var scope = services.CreateAsyncScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

    foreach (var roleName in new[] { "Admin", "Administrador" })
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var createRole = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            if (!createRole.Succeeded)
            {
                Console.Error.WriteLine("Não foi possível criar os perfis administrativos exigidos.");
                return 1;
            }
        }
    }

    var existingAdministrators = (await userManager.GetUsersInRoleAsync("Admin"))
        .Concat(await userManager.GetUsersInRoleAsync("Administrador"))
        .DistinctBy(user => user.Id)
        .ToList();
    if (existingAdministrators.Count > 0)
    {
        Console.WriteLine("Já existe um administrador. Nenhuma conta foi modificada.");
        return 0;
    }

    if (await userManager.FindByNameAsync(username) is not null || await userManager.FindByEmailAsync(email) is not null)
    {
        Console.Error.WriteLine("O usuário ou e-mail informado já existe. Nenhuma conta foi modificada.");
        return 1;
    }

    var user = new User
    {
        UserName = username,
        Email = email,
        FullName = fullName,
        EmailConfirmed = true,
        IsActive = true
    };
    var createUser = await userManager.CreateAsync(user, password);
    if (!createUser.Succeeded)
    {
        Console.Error.WriteLine("Não foi possível criar o administrador inicial. Verifique os requisitos de senha.");
        return 1;
    }

    var addRole = await userManager.AddToRoleAsync(user, "Admin");
    if (!addRole.Succeeded)
    {
        await userManager.DeleteAsync(user);
        Console.Error.WriteLine("Não foi possível atribuir o perfil administrativo.");
        return 1;
    }

    Console.WriteLine("Administrador inicial provisionado com sucesso.");
    return 0;
}

static string RequireEnvironment(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(value) || value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"A variável obrigatória '{name}' não foi informada.");
    }

    return value;
}
