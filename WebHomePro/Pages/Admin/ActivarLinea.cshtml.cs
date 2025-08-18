using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using WebHomePro.Services.IProveedorService;

namespace WebHomePro.Pages.Admin
{
    public class ActivarLineaModel : PageModel
    {
        private readonly IProveedorService _svc;
        public ActivarLineaModel(IProveedorService svc) => _svc = svc;

        public List<LineaDisponibleDto> Lineas { get; set; } = new();

        [BindProperty] public string? NumeroTelefono { get; set; }
        [BindProperty] public string? IdTelefono { get; set; }
        [BindProperty] public string? IdTarjeta { get; set; }
        [BindProperty] public string? Tipo { get; set; }

        [BindProperty, Required, MinLength(9), MaxLength(12)]
        public string Cedula { get; set; } = "";

        public string Mensaje { get; set; } = "";
        public bool EsExito { get; set; }

        public async Task OnGetAsync()
        {
            Lineas = await _svc.ObtenerLineasDisponiblesAsync();
        }

        public async Task<IActionResult> OnPostSelectAsync(string numero, string idTel, string idTarj, string tipo)
        {
            Lineas = await _svc.ObtenerLineasDisponiblesAsync();

            NumeroTelefono = numero;
            IdTelefono = idTel;
            IdTarjeta = idTarj;
            Tipo = tipo;

            return Page();
        }

        public async Task<IActionResult> OnPostActivarAsync()
        {
            Lineas = await _svc.ObtenerLineasDisponiblesAsync();

            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(NumeroTelefono)
                || string.IsNullOrWhiteSpace(IdTelefono) || string.IsNullOrWhiteSpace(IdTarjeta)
                || string.IsNullOrWhiteSpace(Tipo))
            {
                Mensaje = "Datos incompletos o inválidos.";
                EsExito = false;
                return Page();
            }

            var ced = Cedula.Trim();
            if (ced.Length < 9 || ced.Length > 12 || !ced.All(char.IsDigit))
            {
                Mensaje = "Cédula inválida.";
                EsExito = false;
                return Page();
            }

            var resp = await _svc.ActivarLineaAsync(NumeroTelefono!, IdTelefono!, IdTarjeta!, Tipo!, ced);

            EsExito = resp.Exitoso;
            Mensaje = EsExito ? "¡Proceso finalizado de forma exitosa!" : $"Error al realizar el proceso: {resp.Mensaje}";

            if (EsExito)
            {
                Lineas = await _svc.ObtenerLineasDisponiblesAsync();
                NumeroTelefono = IdTelefono = IdTarjeta = Tipo = "";
                Cedula = "";
            }

            return Page();
        }
    }
}
