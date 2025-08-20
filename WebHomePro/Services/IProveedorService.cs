using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WebHomePro.Services.IProveedorService
{
    public interface IProveedorService
    {
        // CLIENTE4
        Task<List<LineaPrepagoVm>> GetLineasPrepagoAsync(string cedula);
        Task<List<LineaPostpagoVm>> GetLineasPostpagoAsync(string cedula);

        // ADM
        Task<List<LineaDisponibleDto>> ObtenerLineasDisponiblesAsync();
        Task<RespuestaWs> ActivarLineaAsync(string numero, string idTelefono, string idTarjeta, string tipo, string cedula);
        Task<List<LineaNuevaVm>> GetLineasNuevasDisponiblesAsync(CancellationToken ct = default);
        Task<OperacionResponse> ActivarLineaAsync(ActivarLineaRequestDto req, CancellationToken ct = default);

        // CLIENTE5
        Task<(bool ok, string? mensaje)> RecargarSaldoPrepagoAsync(string telefono, int monto);
        Task<LineaPrepagoVm?> ObtenerLineaPrepagoAsync(string telefono);
    }

    // ---- DTOs auxiliares ----
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
}

