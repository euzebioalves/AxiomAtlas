using Axiom.Atlas.Web.Handlers.Auth;
using Axiom.Atlas.Web.Services.Auth;

var builder = WebApplication.CreateBuilder(args);
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7255/";

// 1. Registros Básicos do MVC
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// 2. Registrando o Handler (Interceptador)
builder.Services.AddTransient<AuthHeaderHandler>();

// 3. Registrando o Serviço HTTP (Apenas UMA vez, com todas as regras)
builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
});

builder.Services.AddHttpClient<IAuthService, AuthService>(client =>
{
    // A URL base que ele vai usar para bater na API
    client.BaseAddress = new Uri(apiBaseUrl);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Ignora erros de certificado SSL em ambiente de desenvolvimento local
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
})
.AddHttpMessageHandler<AuthHeaderHandler>();

// 4. Configuração do Cookie de Autenticação (A "memória" do login no navegador)
builder.Services.AddAuthentication("CookieAuth")
    .AddCookie("CookieAuth", options =>
    {
        options.Cookie.Name = "AxiomAtlas_AuthToken";
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.SlidingExpiration = true;
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
