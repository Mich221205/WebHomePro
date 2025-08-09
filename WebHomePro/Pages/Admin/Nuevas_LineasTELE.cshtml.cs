using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WSProveedorRef;

namespace WebHomePro.Pages.Admin
{
    public class Nuevas_LineasTELEModel : PageModel
    {
        [BindProperty] public string NumeroTelefono { get; set; } = string.Empty;
        [BindProperty] public string IdTelefono { get; set; } = string.Empty;
        [BindProperty] public string IdTarjeta { get; set; } = string.Empty;
        [BindProperty] public string Tipo { get; set; } = string.Empty;

        public async Task<IActionResult> OnPostNueva()
        {
            // 🔹 Validaciones
            if (string.IsNullOrWhiteSpace(NumeroTelefono) ||
                string.IsNullOrWhiteSpace(IdTelefono) ||
                string.IsNullOrWhiteSpace(IdTarjeta) ||
                string.IsNullOrWhiteSpace(Tipo))
            {
                TempData["Error"] = "Todos los campos son obligatorios.";
                return RedirectToPage();
            }

            if (IdTelefono.Length != 16 || !IdTelefono.All(char.IsDigit))
            {
                TempData["Error"] = "Identificador teléfono debe tener exactamente 16 dígitos.";
                return RedirectToPage();
            }

            if (IdTarjeta.Length != 19 || !IdTarjeta.All(char.IsDigit))
            {
                TempData["Error"] = "Identificador tarjeta debe tener exactamente 19 dígitos.";
                return RedirectToPage();
            }

            try
            {
                // 🔹 Cifrado antes de enviar al WS
                string numEnc = AESEncryptor.Encrypt(NumeroTelefono);
                string idTelEnc = AESEncryptor.Encrypt(IdTelefono);
                string idTarEnc = AESEncryptor.Encrypt(IdTarjeta);

                using var ws = new WSProveedorSoapClient(WSProveedorSoapClient.EndpointConfiguration.WSProveedorSoap);
                var resp = await ws.IngresarNuevoServicioAsync(numEnc, idTelEnc, idTarEnc, Tipo, "disponible");

                if (resp.Body.IngresarNuevoServicioResult.Resultado)
                    TempData["Success"] = "✅ ¡Proceso finalizado de forma exitosa!";
                else
                    TempData["Error"] = resp.Body.IngresarNuevoServicioResult.Mensaje;
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al agregar línea: " + ex.Message;
            }

            return RedirectToPage();
        }
    }
}
