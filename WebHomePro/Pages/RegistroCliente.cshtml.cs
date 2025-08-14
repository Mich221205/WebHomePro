using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace WebHomePro.Pages
{
    public class RegistroClienteModel : PageModel
    {

        /*variables internas*/
        [BindProperty, Required] public string Identificacion { get; set; } = string.Empty;
        [BindProperty, Required] public string Nombre { get; set; } = string.Empty;
        [BindProperty, Required] public string PrimerApellido { get; set; } = string.Empty;
        [BindProperty, Required] public string SegundoApellido { get; set; } = string.Empty;
        [BindProperty, Required, EmailAddress] public string Correo { get; set; } = string.Empty;
        [BindProperty, Required] public string Usuario { get; set; } = string.Empty;
        [BindProperty, Required, MinLength(14)]
        public string Password { get; set; } = string.Empty;

        public string Mensaje { get; set; } = string.Empty;
        public bool EsExitoso { get; set; }

        /*------------------*/
        public void OnGet()
        {
        }
    }
}
