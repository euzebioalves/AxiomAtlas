using Audit.Core;
using Axiom.Atlas.Api.Transformers;
using Axiom.Atlas.Application.Interfaces;
using Axiom.Atlas.Application.Services;
using Axiom.Atlas.Domain.Entities.AuditLogs;
using Axiom.Atlas.Domain.Entities.Users;
using Axiom.Atlas.Domain.Interfaces.Auth;
using Axiom.Atlas.Domain.Interfaces.Mail;
using Axiom.Atlas.Domain.Interfaces.Users;
using Axiom.Atlas.Infrastructure.Repositories.Users;
using Axiom.Atlas.Infrastructure.Services.Auth;
using Axiom.Atlas.Infrastructure.Services.Mail;
using Axiom.Atlas.Infrastructure.Services.Notifications;
using Axiom.Atlas.Infrastructure.Services.ServiceDesk;
using Axiom.Atlas.Infrastructure.Services.TimeEntries;
using Axiom.Atlas.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException(
            "DataProtection:KeysPath deve apontar para um diretório persistente na produção.");
    }

    // Preserve the framework default development key ring so existing encrypted
    // local integration settings remain readable after application upgrades.
    dataProtectionKeysPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ASP.NET",
        "DataProtection-Keys");
}

dataProtectionKeysPath = Path.GetFullPath(dataProtectionKeysPath);
Directory.CreateDirectory(dataProtectionKeysPath);

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var jwtSecretKey = GetJwtSecretKey(jwtSettings, builder.Environment);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("Axiom.Atlas");
builder.Services.AddControllers();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddIdentityApiEndpoints<User>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITimeConverterService, TimeConverterService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddSchemaTransformer<TimeSpanSchemaTransformer>();
    options.AddSchemaTransformer<DateTimeSchemaTransformer>();
    options.AddSchemaTransformer<EnumSchemaTransformer>();
    options.AddOperationTransformer<HideOcelotOperationsTransformer>();
});
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"\n🚨 [ERRO JWT] Falha na Autenticação: {context.Exception.Message}\n");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine($"\n✅ [SUCESSO JWT] Token validado para o usuário: {context.Principal?.Identity?.Name}\n");
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdministrationOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => context.User.Claims
            .Where(claim => claim.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Any(role => role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                         role.Equals("Administrador", StringComparison.OrdinalIgnoreCase)));
    });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AxiomAtlasPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:7255")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<OpenProjectService>();
builder.Services.AddScoped<GlpiService>();
builder.Services.AddSingleton<GlpiImprovementTicketSynchronizationQueue>();
builder.Services.AddHostedService<GlpiImprovementTicketSynchronizationHostedService>();
builder.Services.AddScoped<OpenProjectWorkPackageStatusMonitor>();
builder.Services.AddHostedService<OpenProjectWorkPackageStatusMonitoringHostedService>();
builder.Services.AddHttpClient();

// Injeta o serviço de email para ser usado em notificações, resets de senha, etc
builder.Services.AddScoped<IEmailService, EmailService>();

// Injeta o HttpContextAccessor para permitir acesso ao contexto HTTP (e.g., para pegar o usuário logado) dentro do Audit.NET
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ==========================================
// INICIALIZAÇÃO DO AUDIT.NET
// ==========================================
var httpContextAccessor = app.Services.GetRequiredService<IHttpContextAccessor>();

var efProvider = new Audit.EntityFramework.Providers.EntityFrameworkDataProvider()
{
    // Usa um escopo seguro para obter o AppDbContext
    DbContextBuilder = ev => app.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>(),
    AuditTypeMapper = (t, entry) => typeof(AuditLog),
    IgnoreMatchedPropertiesFunc = t => true,

    AuditEntityAction = async (ev, entry, auditEntity) =>
    {
        var log = (AuditLog)auditEntity;
        log.DataHora = DateTime.UtcNow;

        // 1. Tenta pegar o E-mail ou NameIdentifier direto do Token JWT (usuário logado)
        var httpContext = httpContextAccessor.HttpContext;
        var loggedUser = httpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                      ?? httpContext?.User?.Identity?.Name;
        log.IpAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();

        // 2. Define o usuário obedecendo a prioridade:
        if (ev.CustomFields.TryGetValue("Username", out var username) && username != null)
        {
            // Cenário A: Logs Manuais (ex: tela de Login, onde o token ainda não existe)
            log.Usuario = username.ToString();
        }
        else if (!string.IsNullOrEmpty(loggedUser))
        {
            // Cenário B: Logs Automáticos (Insert/Update/Delete de um usuário autenticado no sistema)
            log.Usuario = loggedUser;
        }
        else
        {
            // Cenário C: Rotinas automáticas em background ou acessos anônimos
            log.Usuario = "Sistema";
        }

        if (entry != null) // Logs automáticos (Insert/Update/Delete)
        {
            log.TipoAcao = entry.Action;
            log.Tabela = entry.Table;
            log.ChavePrimaria = string.Join(",", entry.PrimaryKey.Values);

            if (entry.Action == "Delete")
            {
                // No Delete, o registro que está morrendo é o "Antigo". Não há valor novo.
                log.ValoresAntigos = System.Text.Json.JsonSerializer.Serialize(entry.ColumnValues);
                log.ValoresNovos = null;
            }
            else if (entry.Action == "Insert")
            {
                // No Insert, o registro inteiro é "Novo". Não há valor antigo.
                log.ValoresAntigos = null;
                log.ValoresNovos = System.Text.Json.JsonSerializer.Serialize(entry.ColumnValues);
            }
            else // Update
            {
                // No Update, gravamos o JSON apenas das colunas que foram alteradas no "Antigo"...
                log.ValoresAntigos = System.Text.Json.JsonSerializer.Serialize(entry.Changes?.ToDictionary(c => c.ColumnName, c => c.OriginalValue));
                // ...e o estado completo do registro no "Novo"
                log.ValoresNovos = System.Text.Json.JsonSerializer.Serialize(entry.ColumnValues);
            }
        }
        else // Logs manuais
        {
            log.TipoAcao = ev.EventType;
            log.ValoresNovos = System.Text.Json.JsonSerializer.Serialize(ev.CustomFields);
        }

        return true;
    }
};

// Registrar o provedor GLOBALMENTE
Audit.Core.Configuration.DataProvider = efProvider;

// Ativar o rastreamento do EF
Audit.EntityFramework.Configuration.Setup()
    .ForContext<AppDbContext>(config => config.IncludeEntityObjects())
    .UseOptOut();
// ==========================================


if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Axiom Atlas API v1");
    });

    app.MapGet("/", () => Results.Redirect("/swagger"));
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
}
app.UseRouting();

app.UseCors("AxiomAtlasPolicy");

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.UseStaticFiles();

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "Healthy",
    service = "Axiom Atlas API",
    timestamp = DateTimeOffset.UtcNow
})).AllowAnonymous();

app.MapGet("/health/ready", async (AppDbContext context, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("Axiom.Atlas.Health");
    try
    {
        if (!await context.Database.CanConnectAsync(cancellationToken))
        {
            return Results.Problem(
                title: "Banco de dados indisponível.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(new
        {
            status = "Healthy",
            database = "Available",
            timestamp = DateTimeOffset.UtcNow
        });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "O health check de prontidão não conseguiu acessar o banco de dados.");
        return Results.Problem(
            title: "Banco de dados indisponível.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

app.MapControllers();
app.MapOpenApi("/openapi/{documentName}.json");

app.MapIdentityApi<User>();

app.Run();

static string GetJwtSecretKey(IConfigurationSection jwtSettings, IHostEnvironment environment)
{
    var secretKey = jwtSettings["SecretKey"];
    var issuer = jwtSettings["Issuer"];
    var audience = jwtSettings["Audience"];

    if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
    {
        throw new InvalidOperationException(
            "JwtSettings:SecretKey, JwtSettings:Issuer e JwtSettings:Audience devem estar configurados.");
    }

    if (environment.IsProduction() &&
        (secretKey.Length < 32 || secretKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException(
            "JwtSettings:SecretKey deve ser uma chave exclusiva de pelo menos 32 caracteres na produção.");
    }

    return secretKey;
}
