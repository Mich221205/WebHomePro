using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using WSAUTENTICACION;


namespace WebHomePro.Pages.Admin
{
    // Clase separada para el modelo de Administrador
    public class Administrador
    {
        public string Identificacion { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string PrimerApellido { get; set; } = string.Empty;
        public string SegundoApellido { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string Usuario { get; set; } = string.Empty;
        public string Contrasena { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
    }

    public class Nuevos_AdminModel : PageModel
    {
        // Lista para mostrar en la tabla
        public List<Administrador> Administradores { get; set; } = new();

        // Propiedades para el formulario de nuevo administrador
        [BindProperty] public string Identificacion { get; set; } = string.Empty;
        [BindProperty] public string Nombre { get; set; } = string.Empty;
        [BindProperty] public string PrimerApellido { get; set; } = string.Empty;
        [BindProperty] public string SegundoApellido { get; set; } = string.Empty;
        [BindProperty] public string Correo { get; set; } = string.Empty;
        [BindProperty] public string Usuario { get; set; } = string.Empty;
        [BindProperty] public string Contrasena { get; set; } = string.Empty;

        public async Task OnGet()
        {
            try
            {
                var ws = new AuthServiceClient();
                var lista = await ws.ObtenerAdministradoresAsync();

                Administradores = lista.Select(u => new Administrador
                {
                    Identificacion = u.identificacion,
                    Nombre = u.nombre,
                    PrimerApellido = u.apellido1,
                    SegundoApellido = u.apellido2,
                    Correo = u.correo,
                    Usuario = "[CIFRADO]", 
                    Contrasena = "**************", 
                    Estado = u.estado
                }).ToList();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al obtener administradores: " + ex.Message;
            }
        }


        public async Task<IActionResult> OnPostNuevo()
        {
            // Validar campos requeridos
            if (string.IsNullOrWhiteSpace(Identificacion) ||
                string.IsNullOrWhiteSpace(Nombre) ||
                string.IsNullOrWhiteSpace(PrimerApellido) ||
                string.IsNullOrWhiteSpace(SegundoApellido) ||
                string.IsNullOrWhiteSpace(Correo) ||
                string.IsNullOrWhiteSpace(Usuario) ||
                string.IsNullOrWhiteSpace(Contrasena))
            {
                TempData["Error"] = "Todos los campos son obligatorios.";
                return Page();
            }

            // Validar formato de contraseña (14 caracteres y requisitos)
            var regexPass = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{14}$");
            if (!regexPass.IsMatch(Contrasena))
            {
                TempData["Error"] = "La contraseña debe tener exactamente 14 caracteres, incluyendo mayúsculas, minúsculas, números y un carácter especial.";
                return Page();
            }

            try
            {
                var ws = new AuthServiceClient(); // Cliente igual que en Login

                var nuevo = new Usuario
                {
                    identificacion = Identificacion,
                    nombre = Nombre,
                    apellido1 = PrimerApellido,
                    apellido2 = SegundoApellido,
                    correo = Correo,
                    usuario = Usuario,
                    contrasenna = Contrasena,
                    tipo = 1,       // administrador
                    estado = "activo"
                };

                var resultado = await ws.RegistrarUsuarioAsync(nuevo);

                if (resultado.Resultado)
                {
                    TempData["Success"] = "Registro exitoso";
                    return RedirectToPage();
                }
                else
                {
                    TempData["Error"] = resultado.Mensaje;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al registrar administrador: " + ex.Message;
            }


            return Page();
        }
    }
}


