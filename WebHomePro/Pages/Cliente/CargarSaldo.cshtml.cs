using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WSProveedorRef;
using Microsoft.AspNetCore.Http; // necesario para Session

namespace WebHomePro.Pages.Cliente
{
    public class CargarSaldoModel : PageModel
    {
        private readonly WSProveedorSoapClient _ws;

        public CargarSaldoModel(WSProveedorSoapClient ws)
        {
            _ws = ws;
        }

        // ----------- Estado de la vista -----------
        public bool MostrarFormulario { get; set; }
        public List<LineaPrepagoDto> LineasPrepago { get; set; } = new();
        [BindProperty] public RecargaInputModel Recarga { get; set; } = new();
        public decimal SaldoActual { get; set; }

        // GET: desde menú (lista) o desde CLIENTE4 (/{telefono})
        public async Task<IActionResult> OnGetAsync(string? telefono)
        {
            var cedula = GetClienteCedula();
            if (string.IsNullOrEmpty(cedula))
                return RedirectToPage("/Login");

            if (!string.IsNullOrWhiteSpace(telefono))
            {
                // Abrir directamente el formulario de recarga (flujo CLIENTE4 -> CLIENTE5)
                MostrarFormulario = true;
                Recarga.NumeroTelefono = telefono;
                SaldoActual = await ObtenerSaldoPorFallbackAsync(telefono, cedula);
                return Page();
            }

            // Desde menú principal: listar líneas prepago
            var resp = await _ws.ListarLineasPrepagoPorClienteAsync(cedula);
            var dto = resp.Body.ListarLineasPrepagoPorClienteResult;

            LineasPrepago = new List<LineaPrepagoDto>();
            if (dto != null)
            {
                foreach (var l in dto)
                {
                    LineasPrepago.Add(new LineaPrepagoDto
                    {
                        NumeroTelefono = l.Telefono,
                        Saldo = l.SaldoDisponible
                    });
                }
            }

            MostrarFormulario = false;
            return Page();
        }

        // POST: procesar la recarga
        public async Task<IActionResult> OnPostPagarAsync()
        {
            var cedula = GetClienteCedula();
            if (string.IsNullOrEmpty(cedula))
                return RedirectToPage("/Login", new { error = "SesionCaduca" });

            if (!ValidaExpiracion(Recarga.FechaVencimiento, out var err))
                ModelState.AddModelError(nameof(Recarga.FechaVencimiento), err ?? "Fecha inválida.");

            if (!ModelState.IsValid)
            {
                MostrarFormulario = true;
                SaldoActual = await ObtenerSaldoPorFallbackAsync(Recarga.NumeroTelefono, cedula);
                return Page();
            }

            // 🔹 Llamada al WS_PROVEEDOR para recargar
            var r = await _ws.RecargarSaldoPrepagoAsync(
                telefono: Recarga.NumeroTelefono,
                monto: Recarga.Monto.ToString("0.00"), // formato decimal con 2 decimales
                tarjeta: Recarga.NumeroTarjeta,
                titular: Recarga.NombreTarjeta,
                exp: Recarga.FechaVencimiento,
                cvv: Recarga.CVV
            );

            var res = r.Body.RecargarSaldoPrepagoResult;

            if (res?.Resultado == true)
            {
                TempData["Success"] = "Recarga exitosa";
                return RedirectToPage("./CargarSaldo"); 
            }

            TempData["Error"] = res?.Mensaje ?? "Error al realizar el proceso";

            // Volver a mostrar formulario con saldo actualizado
            MostrarFormulario = true;
            SaldoActual = await ObtenerSaldoPorFallbackAsync(Recarga.NumeroTelefono, cedula);

            return Page();
        }

        // ------------ Helpers ------------
        private string? GetClienteCedula()
        {
            return HttpContext.Session.GetString("Cedula");
        }

        private static bool ValidaExpiracion(string? mmYY, out string? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(mmYY) || mmYY.Length != 5 || mmYY[2] != '/')
            { error = "Formato inválido (MM/YY)."; return false; }

            var mmStr = mmYY[..2];
            var yyStr = mmYY[^2..];

            if (!int.TryParse(mmStr, out var mm) || mm < 1 || mm > 12)
            { error = "Mes inválido."; return false; }

            if (!int.TryParse(yyStr, out var yy))
            { error = "Año inválido."; return false; }

            var year = 2000 + yy;
            var now = DateTime.Now;
            var expLastDay = new DateTime(year, mm, 1).AddMonths(1).AddDays(-1);
            var compare = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
            if (expLastDay < compare)
            { error = "La tarjeta está vencida."; return false; }

            return true;
        }

        private async Task<decimal> ObtenerSaldoPorFallbackAsync(string telefono, string cedula)
        {
            // Fallback: listar prepago y filtrar por teléfono
            var telNorm = SoloDigitos(telefono);
            var resp = await _ws.ListarLineasPrepagoPorClienteAsync(cedula);
            var dto = resp.Body.ListarLineasPrepagoPorClienteResult;

            if (dto != null)
            {
                var match = dto.FirstOrDefault(x => SoloDigitos(x.Telefono) == telNorm);
                if (match != null) return match.SaldoDisponible;
            }
            return 0m;
        }

        private static string SoloDigitos(string? s)
            => new string((s ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    // ----------- DTOs de la vista -----------
    public class LineaPrepagoDto
    {
        public string NumeroTelefono { get; set; } = default!;
        public decimal Saldo { get; set; }
    }

    public class RecargaInputModel
    {
        [Required]
        [Display(Name = "Número de teléfono")]
        public string NumeroTelefono { get; set; } = default!;

        [Required]
        [RegularExpression(@"^\d{4}\s\d{4}\s\d{4}$", ErrorMessage = "Debe contener 12 dígitos en grupos de 4 (ej. 1234 5678 9012).")]
        public string NumeroTarjeta { get; set; } = default!;

        [Required, StringLength(80, MinimumLength = 3)]
        public string NombreTarjeta { get; set; } = default!;

        [Required]
        [RegularExpression(@"^\d{2}\/\d{2}$", ErrorMessage = "Formato MM/YY.")]
        public string FechaVencimiento { get; set; } = default!;

        [Required]
        [RegularExpression(@"^\d{3}$", ErrorMessage = "CVV debe tener 3 dígitos.")]
        public string CVV { get; set; } = default!;

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "El monto debe ser un entero positivo.")]
        public int Monto { get; set; }
    }
}
