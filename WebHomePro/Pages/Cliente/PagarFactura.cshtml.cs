using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using WSProveedorRef;

namespace WebHomePro.Pages.Cliente
{
    public class PagarFacturaModel : PageModel
    {
        private readonly WSProveedorSoapClient _ws;

        public PagarFacturaModel(WSProveedorSoapClient ws)
        {
            _ws = ws;
        }

        // ----------- Estado de la vista -----------
        public List<LineaPostpagoDto> LineasPostpago { get; set; } = new();
        [BindProperty] public PagoInputModel Pago { get; set; } = new();

        public bool MostrarFormulario { get; set; }
        public string? Mensaje { get; set; }          // Pago
        public bool EsExito { get; set; }
        public string? MensajeFacturas { get; set; }  // Estado facturas

        // GET: desde menú (sin teléfono) o desde CLIENTE4 (/{telefono})
        public async Task<IActionResult> OnGetAsync(string? telefono)
        {
            var cedula = HttpContext.Session.GetString("Cedula");
            if (string.IsNullOrEmpty(cedula))
            {
                Mensaje = "Debe iniciar sesión.";
                return RedirectToPage("/Login");
            }

            // ✅ Llamar al WS real
            var resp = await _ws.ObtenerFacturasPostpagoPorClienteAsync(cedula);

            LineasPostpago = resp.Body.ObtenerFacturasPostpagoPorClienteResult
                .Select(x => new LineaPostpagoDto
                {
                    NumeroTelefono = x.NumeroTelefono,
                    IdTarjeta = x.IdentificadorTarjeta,
                    MontoPendiente = x.MontoPendiente2,
                    Estado = x.EstadoPago
                }).ToList();

            // Evaluar si hay facturas
            EvaluarFacturas();

            // Si viene desde CLIENTE4 con teléfono, precargar selección y abrir panel
            if (!string.IsNullOrWhiteSpace(telefono))
            {
                var sel = LineasPostpago.FirstOrDefault(x => x.NumeroTelefono == telefono);
                if (sel != null)
                {
                    Pago.Telefono = sel.NumeroTelefono;
                    Pago.MontoFactura = sel.MontoPendiente <= 0 ? 0 : sel.MontoPendiente;
                    MostrarFormulario = true;
                }
            }

            return Page();
        }

        // POST: Procesar pago
        public async Task<IActionResult> OnPostPagarAsync()
        {
            var cedula = HttpContext.Session.GetString("Cedula");
            if (string.IsNullOrEmpty(cedula))
            {
                Mensaje = "Debe iniciar sesión.";
                return RedirectToPage("/Login");
            }

            // Recargar lista desde WS
            var resp = await _ws.ObtenerFacturasPostpagoPorClienteAsync(cedula);

            LineasPostpago = resp.Body.ObtenerFacturasPostpagoPorClienteResult
                .Select(x => new LineaPostpagoDto
                {
                    NumeroTelefono = x.NumeroTelefono,
                    IdTarjeta = x.IdentificadorTarjeta,
                    MontoPendiente = x.MontoPendiente2,
                    Estado = x.EstadoPago
                }).ToList();

            // Validaciones personalizadas
            ValidarFechaVencimiento(Pago.FechaVencimiento, nameof(Pago.FechaVencimiento));
            ValidarNumeroTarjeta(Pago.NumeroTarjeta, nameof(Pago.NumeroTarjeta));

            if (!ModelState.IsValid)
            {
                MostrarFormulario = true;
                Mensaje = "Por favor corrige los errores del formulario.";
                EsExito = false;
                return Page();
            }

            try
            {
                // ✅ Llamar al WS para pagar
                var pagoResp = await _ws.PagarFacturaPostpagoAsync(cedula, Pago.Telefono);
                var resultado = pagoResp.Body.PagarFacturaPostpagoResult;

                if (resultado.RESULTADO)
                {
                    Mensaje = resultado.Mensaje;
                    EsExito = true;

                    // ✅ Actualizar lista en memoria
                    var linea = LineasPostpago.FirstOrDefault(l => l.NumeroTelefono == Pago.Telefono);
                    if (linea != null)
                    {
                        linea.MontoPendiente = 0;
                        linea.Estado = "Al día";
                    }
                }
                else
                {
                    Mensaje = resultado.Mensaje; // 🔹 Mostrar mensaje de error del WS
                    EsExito = false;
                }
            }
            catch (Exception ex)
            {
                Mensaje = $"Error en el servicio: {ex.Message}";
                EsExito = false;
            }

            // ❌ NO llamar EvaluarFacturas aquí
            MostrarFormulario = true;
            return Page();

        }

        // ------------------ Validaciones helper ------------------
        private void ValidarFechaVencimiento(string? mmYY, string field)
        {
            if (string.IsNullOrWhiteSpace(mmYY))
            {
                ModelState.AddModelError(field, "La fecha de vencimiento es requerida.");
                return;
            }

            if (!Regex.IsMatch(mmYY, @"^(0[1-9]|1[0-2])\/\d{2}$"))
            {
                ModelState.AddModelError(field, "Formato inválido. Use MM/YY.");
                return;
            }

            var parts = mmYY.Split('/');
            int mm = int.Parse(parts[0]);
            int yy = 2000 + int.Parse(parts[1]);

            var ahora = DateTime.UtcNow;
            var primerDiaMesActual = new DateTime(ahora.Year, ahora.Month, 1);
            var ultimoDiaMesVenc = new DateTime(yy, mm, 1).AddMonths(1).AddDays(-1);

            if (ultimoDiaMesVenc < primerDiaMesActual)
            {
                ModelState.AddModelError(field, "La tarjeta está vencida.");
            }
        }

        private void ValidarNumeroTarjeta(string? num, string field)
        {
            if (string.IsNullOrWhiteSpace(num))
            {
                ModelState.AddModelError(field, "El número de tarjeta es requerido.");
                return;
            }

            if (!Regex.IsMatch(num, @"^\d{4}\s\d{4}\s\d{4}$"))
            {
                ModelState.AddModelError(field, "Debe tener 12 dígitos en grupos de 4 (#### #### ####).");
            }
        }

        // ------------------ Helper para evaluar facturas ------------------
        private void EvaluarFacturas()
        {
            if (LineasPostpago == null || LineasPostpago.Count == 0)
                MensajeFacturas = "No había facturas postpago registradas a su nombre.";
            else if (LineasPostpago.All(l => l.MontoPendiente <= 0))
                MensajeFacturas = "No había facturas pendientes o ya estaban pagadas.";
            else
                MensajeFacturas = null; // ✅ Hay facturas pendientes, no mostrar nada
        }

    }

    // ------------------ DTOs / Inputs ------------------
    public class LineaPostpagoDto
    {
        public string NumeroTelefono { get; set; } = "";
        public string IdTarjeta { get; set; } = "";
        public decimal MontoPendiente { get; set; }
        public string Estado { get; set; } = "";
    }

    public class PagoInputModel
    {
        [Required] public string Telefono { get; set; } = "";

        [Display(Name = "Número de Tarjeta")]
        [Required(ErrorMessage = "El número de tarjeta es requerido.")]
        public string NumeroTarjeta { get; set; } = "";

        [Display(Name = "Nombre del dueño de la tarjeta")]
        [Required, StringLength(80, MinimumLength = 3)]
        public string NombreTarjeta { get; set; } = "";

        [Display(Name = "Fecha de vencimiento")]
        [Required] public string FechaVencimiento { get; set; } = "";

        [Display(Name = "CVV")]
        [Required, RegularExpression(@"^\d{3}$", ErrorMessage = "El CVV debe tener 3 dígitos.")]
        public string CVV { get; set; } = "";

        [Display(Name = "Monto factura a cancelar (CRC)")]
        [Range(0, 999999999)]
        public decimal MontoFactura { get; set; }
    }
}
