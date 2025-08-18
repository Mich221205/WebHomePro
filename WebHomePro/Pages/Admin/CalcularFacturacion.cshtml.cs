using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using WebHomePro.Services.IFaturacionService;
using WebHomePro.Services.Dto;

namespace WebHomePro.Pages.Admin
{
    public class CalcularFacturacionModel : PageModel
    {
        private readonly IFacturacionService _svc;

        public CalcularFacturacionModel(IFacturacionService svc)
        {
            _svc = svc;
        }

        public UltimaFacturacionDto? Ultima { get; set; }

        [BindProperty, Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
        public DateTime? NuevoInicio { get; set; }

        [BindProperty, Required(ErrorMessage = "La fecha de fin es obligatoria.")]
        public DateTime? NuevoFin { get; set; }

        [TempData] public string? Mensaje { get; set; }
        [TempData] public bool EsExito { get; set; }

        public async Task OnGetAsync()
        {
            Ultima = await _svc.GetUltimaFacturacionAsync();
        }

        public async Task<IActionResult> OnPostCalcularAsync()
        {
            Ultima = await _svc.GetUltimaFacturacionAsync();

            if (!ModelState.IsValid)
            {
                Mensaje = "Datos incompletos o inválidos.";
                EsExito = false;
                return Page();
            }

            var ini = NuevoInicio!.Value.Date;
            var fin = NuevoFin!.Value.Date;

            // Validaciones básicas
            if (fin < ini)
            {
                ModelState.AddModelError(nameof(NuevoFin), "La fecha fin no puede ser menor que la fecha inicio.");
            }

            // Valida contra la última ejecución, si existe
            if (Ultima?.FechaMPago != null)
            {
                var lastEnd = Ultima.FechaMPago.Value.Date;

                // i) No iniciar antes (ni igual) que la última fecha fin
                if (ini <= lastEnd)
                {
                    ModelState.AddModelError(nameof(NuevoInicio),
                        $"La nueva ejecución debe iniciar después de {lastEnd:yyyy-MM-dd}.");
                }

                // ii) Sin huecos: debe ser exactamente el día siguiente
                var esperado = lastEnd.AddDays(1);
                if (ini != esperado)
                {
                    ModelState.AddModelError(nameof(NuevoInicio),
                        $"No debe dejar días entre procesos. Use {esperado:yyyy-MM-dd} como inicio.");
                }
            }

            if (!ModelState.IsValid)
            {
                Mensaje = "Revise las validaciones de fecha.";
                EsExito = false;
                return Page();
            }

            // Ejecutar cálculo (SP) y mostrar resultado
            var resp = await _svc.EjecutarCalculoFacturacionAsync(ini, fin);
            Mensaje = resp.Mensaje;
            EsExito = resp.Exitoso;

            // Refrescar “última facturación”
            Ultima = await _svc.GetUltimaFacturacionAsync();

            return Page();
        }
    }
}
