using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WSAUTENTICACION;
using Microsoft.AspNetCore.Http;


namespace WebHomePro.Pages
{
    public class LoginModel : PageModel
    {
        [BindProperty] public string Usuario { get; set; } = string.Empty;
        [BindProperty] public string Password { get; set; } = string.Empty;

        public string Mensaje { get; set; } = string.Empty;

        public async Task<IActionResult> OnPostAsync()
        {
            var cliente = new AuthServiceClient();

            try
            {
                var respuesta = await cliente.LoginAsync(Usuario, Password);

                if (!respuesta.Resultado)
                {
                    Mensaje = respuesta.Mensaje;
                    return Page();
                }

                if (respuesta.TipoUsuario == 1)
                {
                    return RedirectToPage("/Admin/MenuAdmin"); 
                }
                else if (respuesta.TipoUsuario == 2)
                {
                    // Guardar el nombre del cliente en sesión
                    HttpContext.Session.SetString("ClienteNombre", $"{respuesta.Nombre} {respuesta.Apellido1}");

                    return RedirectToPage("/Cliente/MenuCliente");
                }

                else
                {
                    Mensaje = "Tipo de usuario no reconocido.";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                Mensaje = "Error al contactar el servicio: " + ex.Message;
                return Page();
            }
        }

    }
}