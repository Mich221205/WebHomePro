using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using WSProveedorRef;

/*namespace WebHomePro.Pages.Admin
{
    public class ActivarLineaModel : PageModel
    {
        public List<LineaDisponibleVm> LineasDisponibles { get; set; } = new();

        // Campos del formulario
        [BindProperty]
        public string NumeroTelefono { get; set; } = "";
        [BindProperty]
        public string IdTarjeta { get; set; } = "";
        [BindProperty]
        public string IdTelefono { get; set; } = "";
        [BindProperty]
        public string TipoServicio { get; set; } = "";
        [BindProperty]
        public string CedulaCliente { get; set; } = "";

        // Mensajes
        [TempData]
        public string Flash { get; set; } = "";
        public string MensajeValidacion { get; set; } = "";

        public async Task OnGet()
        {
            await CargarLineasDisponibles();
        }

        public async Task<IActionResult> OnPostActivar()
        {
            if (!ValidarCedula(CedulaCliente))
            {
                MensajeValidacion = "C�dula inv�lida. Debe tener 9 d�gitos num�ricos.";
                await CargarLineasDisponibles();
                return Page();
            }

            try
            {
                var client = new WSProveedorSoapClient(WSProveedorSoapClient.EndpointConfiguration.WSProveedorSoap);

                // Llamada al servicio web
                var respuesta = await client.ActivarDesactivarLineaAsync(
                    numeroTelefono: NumeroTelefono,
                    idTelefono: IdTelefono,
                    idTarjeta: IdTarjeta,
                    tipo: TipoServicio,
                    identificacionDuenio: CedulaCliente,
                    estado: "activar");

                var resultado = respuesta?.Body?.ActivarDesactivarLineaResult;

                if (resultado?.Resultado == true)
                {
                    Flash = "�L�nea activada correctamente!";
                }
                else
                {
                    Flash = $"Error: {resultado?.Mensaje ?? "No se pudo activar la l�nea"}";
                }
            }
            catch (Exception ex)
            {
                Flash = $"Error en el servicio: {ex.Message}";
            }

            return RedirectToPage();
        }

        private async Task CargarLineasDisponibles()
        {
            try
            {
                var client = new WSProveedorSoapClient(WSProveedorSoapClient.EndpointConfiguration.WSProveedorSoap);

                var respuesta = await client.ObtenerLineasDisponiblesAsync();

                // Asegurar MISMO TIPO en el null-coalescing:
                var arreglo = respuesta?.Body?.ObtenerLineasDisponiblesResult
                              ?? Array.Empty<LineaDisponible>();

                LineasDisponibles = arreglo.Select(d => new LineaDisponibleVm
                {
                    NumeroTelefono = d.Numero,
                    IdentificadorTarjeta = d.IdTarjeta,
                    IdTelefono = d.IdTelefono,
                    TipoServicio = d.Tipo
                }).ToList();
            }
            catch (Exception ex)
            {
                Flash = $"Error al cargar l�neas: {ex.Message}";
                LineasDisponibles = new List<LineaDisponibleVm>();
            }
        }


        private bool ValidarCedula(string cedula)
        {
            
            return !string.IsNullOrWhiteSpace(cedula)
                && cedula.Length == 9
                && cedula.All(char.IsDigit);
        }
    }

    // Clase para el ViewModel
    public class LineaDisponibleVm
    {
        public string NumeroTelefono { get; set; } = "";
        public string IdentificadorTarjeta { get; set; } = "";
        public string IdTelefono { get; set; } = "";
        public string TipoServicio { get; set; } = "";
    }
} */

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WSProveedorRef;

namespace WebHomePro.Pages.Admin
{
    public class ActivarLineaModel : PageModel
    {
        public List<LineaDisponibleVm> LineasDisponibles { get; set; } = new();

        // Campos del formulario (se llenan al seleccionar una fila y/o escribir la c�dula)
        [BindProperty] public string NumeroTelefono { get; set; } = "";
        [BindProperty] public string IdTarjeta { get; set; } = "";
        [BindProperty] public string IdTelefono { get; set; } = "";
        [BindProperty] public string TipoServicio { get; set; } = "";
        [BindProperty] public string CedulaCliente { get; set; } = "";

        // Mensajes
        [TempData] public string Flash { get; set; } = "";
        public string MensajeValidacion { get; set; } = "";

        public async Task OnGet()
        {
            await CargarLineasDisponibles();
        }

        public async Task<IActionResult> OnPostActivar()
        {
            // Normaliza c�dula (solo d�gitos) antes de validar y enviar
            var cedulaNorm = NormalizeCedula(CedulaCliente);

            if (!ValidarCedula(cedulaNorm))
            {
                MensajeValidacion = "C�dula inv�lida. Debe ser num�rica de 9 a 12 d�gitos.";
                await CargarLineasDisponibles();
                return Page();
            }

            // Validaci�n b�sica de que seleccionaste una l�nea de la lista
            if (string.IsNullOrWhiteSpace(NumeroTelefono) ||
                string.IsNullOrWhiteSpace(IdTelefono) ||
                string.IsNullOrWhiteSpace(IdTarjeta) ||
                string.IsNullOrWhiteSpace(TipoServicio))
            {
                MensajeValidacion = "Selecciona una l�nea de la lista antes de activar.";
                await CargarLineasDisponibles();
                return Page();
            }

            try
            {
                var client = new WSProveedorSoapClient(WSProveedorSoapClient.EndpointConfiguration.WSProveedorSoap);

                // IMPORTANTE: enviar la c�dula normalizada; el WS debe asegurar/crear el cliente si no existe
                var respuesta = await client.ActivarDesactivarLineaAsync(
                    numeroTelefono: (NumeroTelefono ?? string.Empty).Trim(),
                    idTelefono: (IdTelefono ?? string.Empty).Trim(),
                    idTarjeta: (IdTarjeta ?? string.Empty).Trim(),
                    tipo: (TipoServicio ?? string.Empty).Trim(),
                    identificacionDuenio: cedulaNorm,
                    estado: "activar"
                );

                var resultado = respuesta?.Body?.ActivarDesactivarLineaResult;

                if (resultado?.Resultado == true)
                    Flash = "�L�nea activada correctamente!";
                else
                    Flash = $"Error: {resultado?.Mensaje ?? "No se pudo activar la l�nea"}";
            }
            catch (Exception ex)
            {
                Flash = $"Error en el servicio: {ex.Message}";
            }

            return RedirectToPage();
        }

        private async Task CargarLineasDisponibles()
        {
            try
            {
                var client = new WSProveedorSoapClient(WSProveedorSoapClient.EndpointConfiguration.WSProveedorSoap);
                var respuesta = await client.ObtenerLineasDisponiblesAsync();

                var arreglo = respuesta?.Body?.ObtenerLineasDisponiblesResult
                              ?? Array.Empty<LineaDisponible>();

                LineasDisponibles = arreglo.Select(d => new LineaDisponibleVm
                {
                    NumeroTelefono = d.Numero,
                    IdentificadorTarjeta = d.IdTarjeta,
                    IdTelefono = d.IdTelefono,
                    TipoServicio = d.Tipo
                }).ToList();
            }
            catch (Exception ex)
            {
                Flash = $"Error al cargar l�neas: {ex.Message}";
                LineasDisponibles = new List<LineaDisponibleVm>();
            }
        }

        // ===== Helpers de c�dula =====

        /// Quita todo lo que no sea d�gito
        private string NormalizeCedula(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var sb = new StringBuilder(raw.Length);
            foreach (var ch in raw)
                if (char.IsDigit(ch)) sb.Append(ch);
            return sb.ToString();
        }

        /// Acepta 9�12 d�gitos (ajusta si tu formato es fijo)
        private bool ValidarCedula(string cedulaSoloDigitos)
        {
            return !string.IsNullOrWhiteSpace(cedulaSoloDigitos)
                   && cedulaSoloDigitos.Length >= 9
                   && cedulaSoloDigitos.Length <= 12
                   && cedulaSoloDigitos.All(char.IsDigit);
        }
    }

    public class LineaDisponibleVm
    {
        public string NumeroTelefono { get; set; } = "";
        public string IdentificadorTarjeta { get; set; } = "";
        public string IdTelefono { get; set; } = "";
        public string TipoServicio { get; set; } = "";
    }
}
