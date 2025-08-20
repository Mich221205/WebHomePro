using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.ServiceModel;                       // BasicHttpBinding / EndpointAddress
using System.Threading.Tasks;
using WebHomePro.Services;
using WebHomePro.Services.IProveedorService;
using WSProveedorRef;
using WSAUTENTICACION;
using WebHomePro.Services.IFacturacionServices;

var builder = WebApplication.CreateBuilder(args);

// -------------------- Servicios existentes --------------------
builder.Services.AddRazorPages();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IFacturacionService, FacturacionService>();

builder.Services.AddAuthentication("CookieAuth")
    .AddCookie("CookieAuth", options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
    });

// -------------------- Registrar clientes SOAP --------------------
builder.Services.AddScoped<WSProveedorSoapClient>(provider =>
{
    var binding = new BasicHttpBinding
    {
        Security = new BasicHttpSecurity
        {
            Mode = BasicHttpSecurityMode.None
        }
    };

    var endpoint = new EndpointAddress("http://localhost:1234/WSProveedor.asmx");
    return new WSProveedorSoapClient(binding, endpoint);
});

builder.Services.AddScoped<AuthServiceClient>(provider =>
{
    var binding = new BasicHttpBinding
    {
        Security = new BasicHttpSecurity
        {
            Mode = BasicHttpSecurityMode.None
        }
    };

    var endpoint = new EndpointAddress("http://localhost:50339/AuthService.svc");
    return new AuthServiceClient(binding, endpoint);
});

// -------------------- Wrappers para inyección --------------------
// Para IProveedorService
builder.Services.AddScoped<IProveedorService, WebHomePro.Services.IProveedorService.ProveedorService>();

// Para ProveedorService2
builder.Services.AddScoped<ProveedorService2, WebHomePro.Services.ProveedorService>();

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
