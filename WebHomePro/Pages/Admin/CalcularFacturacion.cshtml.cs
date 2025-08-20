using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using WebHomePro.Services.Dto;
using WebHomePro.Services.IFacturacionServices;

namespace WebHomePro.Pages.Admin
{
    public class CalcularFacturacionModel : PageModel
    {
        private readonly IFacturacionService _svc;

        public CalcularFacturacionModel(IFacturacionService svc)
        {
            _svc = svc;
        }

        // Última facturación mostrada al cargar
        public UltimaFacturacionDto? Ultima { get; private set; }

        // Campos del formulario
        [BindProperty, Display(Name = "Fecha de cálculo (inicio)"), DataType(DataType.Date)]
        public DateTime? NuevoInicio { get; set; }

        [BindProperty, Display(Name = "Fecha máxima de pago (fin)"), DataType(DataType.Date)]
        public DateTime? NuevoFin { get; set; }

        // Mensajes en UI
        public string? Mensaje { get; private set; }
        public bool EsExito { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Ultima = await _svc.GetUltimaFacturacionAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Ultima = await _svc.GetUltimaFacturacionAsync();

            if (!NuevoInicio.HasValue) ModelState.AddModelError(nameof(NuevoInicio), "Requerido.");
            if (!NuevoFin.HasValue) ModelState.AddModelError(nameof(NuevoFin), "Requerido.");
            if (!ModelState.IsValid) return Page();

            var ini = NuevoInicio.Value.Date;
            var fin = NuevoFin.Value.Date;

            if (fin < ini)
            {
                ModelState.AddModelError(nameof(NuevoFin), "La fecha fin no puede ser menor a la fecha inicio.");
                return Page();
            }

            // Reglas HU ADM6:
            // i) No iniciar antes de la última ejecución.
            // ii) No dejar días entre la última ejecución y el nuevo inicio (es decir, el nuevo inicio debe ser el día siguiente a la última fecha de pago).
            if (Ultima?.FechaMPago != null)
            {
                var lastEnd = Ultima.FechaMPago.Value.Date;

                if (ini <= lastEnd)
                    ModelState.AddModelError(nameof(NuevoInicio), $"Debe iniciar DESPUÉS de {lastEnd:yyyy-MM-dd}.");

                var esperado = lastEnd.AddDays(1);
                if (ini != esperado)
                    ModelState.AddModelError(nameof(NuevoInicio), $"No puede dejar días entre períodos. El inicio correcto es {esperado:yyyy-MM-dd}.");

                if (!ModelState.IsValid) return Page();
            }

            // Ejecutar cálculo por WS_PROVEEDOR3
            var resp = await _svc.EjecutarCalculoFacturacionAsync(ini, fin);
            Mensaje = resp.Mensaje;
            EsExito = resp.Exitoso;

            // Refresca “última facturación”
            Ultima = await _svc.GetUltimaFacturacionAsync();

            return Page();
        }
    }
}
