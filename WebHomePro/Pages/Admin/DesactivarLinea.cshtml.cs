using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;         
using System.Threading.Tasks;
using WSProveedorRef;                 // Namespace del proxy generado por tu Reference.cs

namespace WebHomePro.Pages.Admin
{
    public class DesactivarLineaModel : PageModel
    {
        // Grid
        public List<LineaEnUsoVm> LineasEnUso { get; set; } = new();

        // Campos que viajan en el POST
        [BindProperty] public string NumeroTelefono { get; set; } = "";
        [BindProperty] public string IdTelefono { get; set; } = "";
        [BindProperty] public string IdTarjeta { get; set; } = "";
        [BindProperty] public string IdCliente { get; set; } = "";
        [BindProperty] public string Tipo { get; set; } = ""; // "prepago" / "postpago"

        [TempData] public string Flash { get; set; } = "";  //mensaje que sobrevive al redirect
        // Mensaje para la vista
        public string Mensaje { get; set; } = "";

        // GET: carga el grid
        public async Task OnGet()
        {
            await CargarLineasEnUso();
        }

        // POST: desactivar
        public async Task<IActionResult> OnPostDesactivar()
        {
            try
            {
                var client = new WSProveedorSoapClient(WSProveedorSoapClient.EndpointConfiguration.WSProveedorSoap);
                var tipoProv = NormalizarTipo(Tipo);

                var resp = await client.ActivarDesactivarLineaAsync(
                    NumeroTelefono, IdTelefono, IdTarjeta, tipoProv, IdCliente, "desactivar");

                var r = resp?.Body?.ActivarDesactivarLineaResult;
                Flash = (r?.Resultado == true)
                    ? "¡Línea desactivada correctamente!"
                    : $"Error al desactivar: {r?.Mensaje ?? "Respuesta nula"}";
            }
            catch (System.Exception ex)
            {
                Flash = "Error: " + ex.Message;
            }

            // PRG: redirige al GET -> vuelve a ejecutar CargarLineasEnUso()
            return RedirectToPage();
        }

        private async Task CargarLineasEnUso()
        {
            try
            {
                var client = new WSProveedorSoapClient(WSProveedorSoapClient.EndpointConfiguration.WSProveedorSoap);
                var resp = await client.ListarLineasEnUsoAsync();
                var data = resp?.Body?.ListarLineasEnUsoResult ?? System.Array.Empty<LineaEnUso>();

                LineasEnUso = data.Select(d => new LineaEnUsoVm
                {
                    NumeroTelefono = d.NUM_TELEFONO,
                    IdentificadorTarjeta = d.IDENTIFICADOR_TARJETA,
                    IdentificadorTelefono = d.IDENTIFICADOR_TELEFONO,
                    IdCliente = d.CEDULA,
                    NombreCliente = d.NOMBRE,
                    TipoServicio = d.TIPO_TELEFONO
                }).ToList();
            }
            catch (System.Exception ex)
            {
                Flash = "Error al cargar líneas: " + ex.Message;
                LineasEnUso = new List<LineaEnUsoVm>();
            }
        }
        private static string NormalizarTipo(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return "";
            t = t.Trim().ToLowerInvariant();
            // Unifica a los valores que el servidor proveedor espera:
            if (t == "pago" || t == "postpago" || t == "post-pago" || t == "pospago") return "postpago";
            if (t == "prepago") return "prepago";
            return t;
        }
    }

    public class LineaEnUsoVm
    {
        public string NumeroTelefono { get; set; } = "";
        public string IdentificadorTarjeta { get; set; } = "";
        public string IdentificadorTelefono { get; set; } = "";
        public string IdCliente { get; set; } = "";
        public string NombreCliente { get; set; } = "";
        public string TipoServicio { get; set; } = "";
    }
}