using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebHomePro.Services;
using WebHomePro.Services.IProveedorService;

namespace WebHomePro.Pages.Cliente
{
    public class MenuClienteModel : PageModel
    {
        private readonly IProveedorService _prov;

        public MenuClienteModel(IProveedorService prov)
        {
            _prov = prov;
        }

        // Propiedades que la vista usar�
        public List<LineaPrepagoVm> LineasPrepago { get; set; } = new();
        public List<LineaPostpagoVm> LineasPostpago { get; set; } = new();
        public string Error { get; set; } = string.Empty;
        public string? ClienteNombre { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Recuperar datos de la sesi�n
                ClienteNombre = HttpContext.Session.GetString("ClienteNombre");
                var cedula = HttpContext.Session.GetString("Cedula");

                if (string.IsNullOrEmpty(cedula))
                {
                    Error = "Sesi�n inv�lida. Inicie sesi�n nuevamente.";
                    return RedirectToPage("/Login");
                }

                // Pasar la c�dula al servicio
                LineasPrepago = await _prov.GetLineasPrepagoAsync(cedula);
                LineasPostpago = await _prov.GetLineasPostpagoAsync(cedula);
            }
            catch (System.Exception ex)
            {
                Error = "No fue posible cargar tus l�neas. " + ex.Message;
            }

            return Page();
        }

    }
}
