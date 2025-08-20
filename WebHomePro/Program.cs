using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.ServiceModel;
using System.Threading.Tasks;

using WebHomePro.Services;                         // FacturacionService
using WebHomePro.Services.IFacturacionServices;    // IFacturacionService
using WebHomePro.Services.IProveedorService;       // IProveedorService, ProveedorService (implementación)
using WSProveedorRef;                              // WSProveedorSoapClient (Connected Service)
using WSAUTENTICACION;                             // AuthServiceClient (WCF/Service Reference)

var builder = WebApplication.CreateBuilder(args);

// -------------------- Servicios base --------------------
builder.Services.AddRazorPages();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();

builder.Services
    .AddAuthentication("CookieAuth")
    .AddCookie("CookieAuth", options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
    });

// -------------------- Servicios de dominio --------------------
builder.Services.AddScoped<IFacturacionService, FacturacionService>();
builder.Services.AddScoped<IProveedorService, ProveedorService>();

// -------------------- Clientes SOAP (DI) --------------------
// WSProveedorSoapClient (URL desde appsettings.json con fallback)
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var url = cfg["WSProveedor:BaseAddress"];
    if (string.IsNullOrWhiteSpace(url))
    {
        // Fallback seguro (dev)
        url = "http://localhost:1234/WSProveedor.asmx";
    }

    var isHttps = url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    var binding = new BasicHttpBinding(isHttps ? BasicHttpSecurityMode.Transport : BasicHttpSecurityMode.None)
    {
        MaxReceivedMessageSize = 10 * 1024 * 1024,
        OpenTimeout = TimeSpan.FromSeconds(15),
        CloseTimeout = TimeSpan.FromSeconds(15),
        SendTimeout = TimeSpan.FromSeconds(30),
        ReceiveTimeout = TimeSpan.FromSeconds(30)
    };

    var endpoint = new EndpointAddress(url);
    return new WSProveedorSoapClient(binding, endpoint);
});

// AuthServiceClient (URL desde appsettings.json con fallback)
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var url = cfg["AuthService:BaseAddress"];
    if (string.IsNullOrWhiteSpace(url))
    {
        // Fallback seguro (dev)
        url = "http://localhost:50339/AuthService.svc";
    }

    var isHttps = url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    var binding = new BasicHttpBinding(isHttps ? BasicHttpSecurityMode.Transport : BasicHttpSecurityMode.None)
    {
        MaxReceivedMessageSize = 10 * 1024 * 1024,
        OpenTimeout = TimeSpan.FromSeconds(15),
        CloseTimeout = TimeSpan.FromSeconds(15),
        SendTimeout = TimeSpan.FromSeconds(30),
        ReceiveTimeout = TimeSpan.FromSeconds(30)
    };

    var endpoint = new EndpointAddress(url);
    return new AuthServiceClient(binding, endpoint);
});

// -------------------- Pipeline --------------------
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", context =>
{
    context.Response.Redirect("/Login");
    return Task.CompletedTask;
});

app.MapRazorPages();

app.Run();
