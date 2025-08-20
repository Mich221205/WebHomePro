using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using WSAUTENTICACION;
using WSProveedorRef;

namespace WebHomePro.Pages
{
    public class LoginModel : PageModel
    {
        private readonly AuthServiceClient _auth;
        private readonly WSProveedorSoapClient _prov;

        // Se inyectan los clientes en el constructor
        public LoginModel(AuthServiceClient auth, WSProveedorSoapClient prov)
        {
            _auth = auth;
            _prov = prov;
        }

        [BindProperty] public string Usuario { get; set; } = string.Empty;
        [BindProperty] public string Password { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;

        // -------------------------------
        private string? GetClienteCedula()
        {
            return HttpContext.Session.GetString("Cedula");
        }
        // -------------------------------

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                // 🔹 Usar el cliente inyectado (NO new)
                var respuesta = await _auth.LoginAsync(Usuario, Password);

                if (!respuesta.Resultado)
                {
                    Mensaje = respuesta.Mensaje;
                    return Page();
                }

                if (respuesta.TipoUsuario == 1)
                    return RedirectToPage("/Admin/MenuAdmin");

                if (respuesta.TipoUsuario == 2)
                {
                    HttpContext.Session.SetString("ClienteNombre", $"{respuesta.Nombre} {respuesta.Apellido1}");
                    HttpContext.Session.SetString("Cedula", respuesta.Identificacion);
                    return RedirectToPage("/Cliente/MenuCliente");
                }

                Mensaje = "Tipo de usuario no reconocido.";
                return Page();
            }
            catch (Exception ex)
            {
                Mensaje = "Error al contactar el servicio: " + ex.Message;
                return Page();
            }
        }
    }
}
