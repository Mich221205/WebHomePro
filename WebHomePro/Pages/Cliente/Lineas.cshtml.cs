using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebHomePro.Services;
using WebHomePro.Services.IProveedorService;
using WSProveedorRef;

namespace WebHomePro.Pages.Cliente
{
    public class LineasModel : PageModel
    {
        private readonly ProveedorService2 _prov;

        public List<LineaPrepagoVm> Prepago { get; private set; } = new List<LineaPrepagoVm>();
        public List<LineaPostpagoVm> Postpago { get; private set; } = new List<LineaPostpagoVm>();
        public string ErrorMessage { get; private set; } = string.Empty;

        public LineasModel(ProveedorService2 prov)
        {
            _prov = prov;
        }

        // Carga (y recarga) de datos
        public async Task<IActionResult> OnGetAsync(bool refresh = false)
        {
            // refresh queda por si en el futuro quieres distinguir lógica; por ahora
            // simplemente volvemos a consultar al servicio siempre que entramos aquí.
            await CargarDatosAsync();
            return Page();
        }

        // ---- Helpers ----
        private async Task CargarDatosAsync()
        {
            try
            {
                Prepago = await _prov.GetLineasPrepagoAsync();
                Postpago = await _prov.GetLineasPostpagoAsync();
                ErrorMessage = string.Empty;
            }
            catch (System.Exception ex)
            {
                ErrorMessage = "No fue posible cargar tus líneas. " + ex.Message;
                Prepago = new List<LineaPrepagoVm>();
                Postpago = new List<LineaPostpagoVm>();
            }
        }

        // Útil si más adelante agregas un botón/acción server-side para refrescar
        public IActionResult OnPostRefrescar()
        {
            // Redirige a GET con el flag refresh=1 (gatilla recarga desde base)
            return RedirectToPage("/Cliente/Lineas", new { refresh = 1 });
        }
    }
}
