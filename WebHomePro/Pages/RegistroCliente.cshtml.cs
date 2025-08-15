using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using WSAUTENTICACION;

namespace WebHomePro.Pages
{
    public class RegistroClienteModel : PageModel
    {
        [BindProperty, Required] public string Identificacion { get; set; } = string.Empty;
        [BindProperty, Required] public string Nombre { get; set; } = string.Empty;
        [BindProperty, Required] public string PrimerApellido { get; set; } = string.Empty;
        [BindProperty, Required] public string SegundoApellido { get; set; } = string.Empty;
        [BindProperty, Required, EmailAddress] public string Correo { get; set; } = string.Empty;
        [BindProperty, Required] public string Usuario { get; set; } = string.Empty;
        [BindProperty, Required, MinLength(14)]
        public string Contrasena { get; set; } = string.Empty;

        public string Mensaje { get; set; } = string.Empty;
        public bool EsExitoso { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostNuevoAsync()
        {
            // Validar campos requeridos
            if (!ModelState.IsValid)
            {
                Mensaje = "Todos los campos son obligatorios y deben ser válidos.";
                EsExitoso = false;
                return Page();
            }

            // Validar formato de contraseña
            var regexPass = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{14,}$");
            if (!regexPass.IsMatch(Contrasena))
            {
                Mensaje = "La contraseña debe tener al menos 14 caracteres, incluyendo mayúsculas, minúsculas, números y un carácter especial.";
                EsExitoso = false;
                return Page();
            }

            try
            {
                // 1️⃣ Registrar en WS_AUTENTICACION
                var wsAuth = new AuthServiceClient();

                var nuevo = new Usuario
                {
                    identificacion = Identificacion,
                    nombre = Nombre,
                    apellido1 = PrimerApellido,
                    apellido2 = SegundoApellido,
                    correo = Correo,
                    usuario = Usuario,
                    contrasenna = Contrasena,
                    tipo = 2,       // cliente
                    estado = "activo"
                };

                var resultado = await wsAuth.RegistrarUsuarioAsync(nuevo);

                if (resultado.Resultado)
                {
                    // 2️⃣ Registrar también en SQL Server por medio de WSProveedorRef
                    var wsProveedor = new WSProveedorRef.WSProveedorSoapClient(
                    WSProveedorRef.WSProveedorSoapClient.EndpointConfiguration.WSProveedorSoap
                );

                    string nombreCompleto = $"{Nombre} {PrimerApellido} {SegundoApellido}";

                    var respProveedor = await wsProveedor.InsertarClienteBasicoAsync(Identificacion, nombreCompleto);

                    var resultadoWS = respProveedor.Body.InsertarClienteBasicoResult;

                    if (resultadoWS.RESULTADO)
                    {
                        Mensaje = "Registro exitoso";
                        EsExitoso = true;
                    }
                    else
                    {
                        Mensaje = "Usuario creado, pero error en SQL Server: " + resultadoWS.Mensaje;
                        EsExitoso = false;
                    }

                }
                else
                {
                    Mensaje = resultado.Mensaje;
                    EsExitoso = false;
                }
            }
            catch (Exception ex)
            {
                Mensaje = "Error al registrar cliente: " + ex.Message;
                EsExitoso = false;
            }

            return Page();
        }

    }
}

