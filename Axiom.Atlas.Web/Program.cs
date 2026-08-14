using Axiom.Atlas.Web.Configuration;
using Axiom.Atlas.Web.Handlers.Auth;
using Axiom.Atlas.Web.Health;
using Axiom.Atlas.Web.Services.Auth;
using Axiom.Atlas.Web.Services.Releases;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;
var builder = WebApplication.CreateBuilder(args);
ProductionConfigurationValidator.Validate(builder.Configuration, builder.Environment);
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"]
    ?? throw new InvalidOperationException("ApiSettings:BaseUrl não configurada.");
var acceptDevelopmentCertificates = builder.Environment.IsDevelopment();
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtectionKeysPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ASP.NET",
        "AxiomAtlas-Web-DataProtection-Keys");
}

Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("Axiom.Atlas.Web");

// 1. Registros Básicos do MVC
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.Configure<ReleaseNotesOptions>(
    builder.Configuration.GetSection(ReleaseNotesOptions.SectionName));

builder.Services.AddHttpClient<IGitHubReleaseNotesService, GitHubReleaseNotesService>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AxiomAtlas-ReleaseNotes/1.0");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
});

// 2. Registrando o Handler (Interceptador)
builder.Services.AddTransient<AuthHeaderHandler>();

// 3. Registrando o Serviço HTTP (Apenas UMA vez, com todas as regras)
var apiClientBuilder = builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

if (acceptDevelopmentCertificates)
{
    apiClientBuilder.ConfigurePrimaryHttpMessageHandler(CreateDevelopmentHttpClientHandler);
}

var authClientBuilder = builder.Services.AddHttpClient<IAuthService, AuthService>(client =>
{
    // A URL base que ele vai usar para bater na API
    client.BaseAddress = new Uri(apiBaseUrl);
});

if (acceptDevelopmentCertificates)
{
    authClientBuilder.ConfigurePrimaryHttpMessageHandler(CreateDevelopmentHttpClientHandler);
}

authClientBuilder.AddHttpMessageHandler<AuthHeaderHandler>();

// 4. Configuração do Cookie de Autenticação (A "memória" do login no navegador)
builder.Services.AddAuthentication("CookieAuth")
    .AddCookie("CookieAuth", options =>
    {
        options.Cookie.Name = acceptDevelopmentCertificates ? "AxiomAtlas.Auth" : "__Host-AxiomAtlas.Auth";
        options.Cookie.Path = "/";
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = acceptDevelopmentCertificates
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(builder.Configuration.GetValue("Authentication:CookieExpirationMinutes", 480));
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
builder.Services.AddHealthChecks()
    .AddCheck<ApiReadinessHealthCheck>("api-ready", tags: ["ready"])
    .AddCheck<PdfRuntimeHealthCheck>("pdf-runtime", tags: ["ready"]);

var app = builder.Build();

// 5. Configurações de Pipeline
if (!app.Environment.IsDevelopment())
{
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        ForwardLimit = 1
    };
    forwardedHeadersOptions.KnownIPNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    forwardedHeadersOptions.KnownProxies.Add(System.Net.IPAddress.Parse("172.30.0.2"));
    app.UseForwardedHeaders(forwardedHeadersOptions);
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Authentication must be registered before authorization.
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
}).AllowAnonymous();

try
{
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.MapControllers();
}
catch (System.Reflection.ReflectionTypeLoadException ex)
{
    throw new InvalidOperationException("Não foi possível inicializar as rotas MVC.", ex);
}

app.Run();

static HttpClientHandler CreateDevelopmentHttpClientHandler() => new()
{
    // O certificado de desenvolvimento local não deve ser aceito fora deste ambiente.
    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
};
