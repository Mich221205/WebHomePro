using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Cryptography;
using System.Text;
using WSProveedorRef;

namespace WebHomePro.Pages.Admin
{
    public class Nuevas_LineasTELEModel : PageModel
    {
        public List<WSProveedorRef.LineaDisponible> LineasDisponibles { get; set; } = new();

        [BindProperty] public string NumeroTelefono { get; set; } = string.Empty;
        [BindProperty] public string IdTelefono { get; set; } = string.Empty;
        [BindProperty] public string IdTarjeta { get; set; } = string.Empty;
        [BindProperty] public string Tipo { get; set; } = string.Empty;

        public async Task OnGet()
        {
            using var ws = new WSProveedorSoapClient(WSProveedorSoapClient.EndpointConfiguration.WSProveedorSoap);
            var resp = await ws.ObtenerLineasDisponiblesAsync();
            LineasDisponibles = resp.Body.ObtenerLineasDisponiblesResult.ToList();
        }

        public async Task<IActionResult> OnPostEliminar(string numeroTelefono)
        {
            try
            {
                using var ws = new WSProveedorSoapClient(WSProveedorSoapClient.EndpointConfiguration.WSProveedorSoap);
                var resp = await ws.EliminarLineaAsync(numeroTelefono);

                if (resp.Body.EliminarLineaResult.Resultado)
                    TempData["Success"] = resp.Body.EliminarLineaResult.Mensaje;
                else
                    TempData["Error"] = resp.Body.EliminarLineaResult.Mensaje;
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar línea: " + ex.Message;
            }

            return RedirectToPage();
        }


        public async Task<IActionResult> OnPostNueva()
        {
            if (string.IsNullOrWhiteSpace(NumeroTelefono) ||
                string.IsNullOrWhiteSpace(IdTelefono) ||
                string.IsNullOrWhiteSpace(IdTarjeta) ||
                string.IsNullOrWhiteSpace(Tipo))
            {
                TempData["Error"] = "Todos los campos son obligatorios.";
                await OnGet();
                return Page();
            }

            if (IdTelefono.Length != 16 || !IdTelefono.All(char.IsDigit))
            {
                TempData["Error"] = "Identificador teléfono debe tener exactamente 16 dígitos.";
                await OnGet();
                return Page();
            }

            if (IdTarjeta.Length != 19 || !IdTarjeta.All(char.IsDigit))
            {
                TempData["Error"] = "Identificador tarjeta debe tener exactamente 19 dígitos.";
                await OnGet();
                return Page();
            }

            try
            {
                string numEnc = AESEncryptor.Encrypt(NumeroTelefono);
                string idTelEnc = AESEncryptor.Encrypt(IdTelefono);
                string idTarEnc = AESEncryptor.Encrypt(IdTarjeta);

                using var ws = new WSProveedorSoapClient(WSProveedorSoapClient.EndpointConfiguration.WSProveedorSoap);
                var resp = await ws.IngresarNuevoServicioAsync(numEnc, idTelEnc, idTarEnc, Tipo, "disponible");

                if (resp.Body.IngresarNuevoServicioResult.Resultado)
                {
                    TempData["Success"] = "Proceso finalizado de forma exitosa";
                    ModelState.Clear();
                }
                else
                {
                    TempData["Error"] = resp.Body.IngresarNuevoServicioResult.Mensaje;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al agregar línea: " + ex.Message;
            }

            await OnGet();
            return Page();
        }
    }

    public static class AESConfig
    {
        public static string Key = "1234567890123456";
        public static string IV = "6543210987654321";
    }

    public static class AESEncryptor
    {
        public static string Encrypt(string plainText)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Encoding.UTF8.GetBytes(AESConfig.Key);
                aesAlg.IV = Encoding.UTF8.GetBytes(AESConfig.IV);
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;

                using (var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV))
                using (var msEncrypt = new MemoryStream())
                {
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (var swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }
                    return "ENC:" + Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }
    }
}

