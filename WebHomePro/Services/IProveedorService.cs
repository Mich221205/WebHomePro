using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
// quita estos dos si no los usas en este archivo:
// using WebHomePro.Pages.Admin;
// using WebHomePro.Services;

namespace WebHomePro.Services.IProveedorService
{

    public interface IProveedorService
    {
        Task<List<LineaDisponibleDto>> ObtenerLineasDisponiblesAsync();
        Task<RespuestaWs> ActivarLineaAsync(string numero, string idTelefono, string idTarjeta, string tipo, string cedula);

        Task<List<LineaNuevaVm>> GetLineasNuevasDisponiblesAsync(CancellationToken ct = default);
        Task<OperacionResponse> ActivarLineaAsync(ActivarLineaRequestDto req, CancellationToken ct = default);

        Task<List<LineaPrepagoDto>> ObtenerLineasPrepagoAsync(string? cedula);
        Task<decimal> ObtenerSaldoPrepagoAsync(string telefono);              // queda por compat
        Task<decimal> ObtenerSaldoPrepagoAsync(string telefono, string? cedula); // fallback con cédula
        Task<(bool ok, string? mensaje)> RecargarSaldoPrepagoAsync(string telefono, int monto);
        Task<LineaPrepagoDto?> ObtenerLineaPrepagoAsync(string telefono);
    }

    public class LineaDisponibleDto
    {
        public string NumeroTelefono { get; set; } = "";
        public string IdTelefono { get; set; } = "";
        public string? IdTarjeta { get; set; } = "";
        public string Tipo { get; set; } = ""; // "prepago" | "postpago"
    }

    public class RespuestaWs
    {
        public bool Exitoso { get; set; }
        public string Mensaje { get; set; } = "";
    }


    public class LineaPrepagoDto
    {
        public string NumeroTelefono { get; set; } = default!;
        public decimal Saldo { get; set; }
    }
}
