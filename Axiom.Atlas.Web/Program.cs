using Axiom.Atlas.Web.Handlers.Auth;
using Axiom.Atlas.Web.Services.Auth;
using Axiom.Atlas.Web.Services.Releases;

var builder = WebApplication.CreateBuilder(args);
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7255/";
var acceptDevelopmentCertificates = builder.Environment.IsDevelopment();

// 1. Registros Básicos do MVC
builder.Services.AddControllersWithViews();
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
        options.Cookie.Name = "AxiomAtlas_AuthToken";
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = acceptDevelopmentCertificates
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
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

var app = builder.Build();

// 5. Configurações de Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// 🚨 Muito importante: Autenticação SEMPRE antes da Autorização
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

try
{
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.MapControllers();
}
catch (System.Reflection.ReflectionTypeLoadException ex)
{
    var errosReais = string.Join(" | ", ex.LoaderExceptions.Select(e => e?.Message));
    throw new Exception($"O CULPADO É: {errosReais}");
}

app.Run();

static HttpClientHandler CreateDevelopmentHttpClientHandler() => new()
{
    // O certificado de desenvolvimento local não deve ser aceito fora deste ambiente.
    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
};
