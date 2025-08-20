using System.Collections.Generic;
using System.Threading.Tasks;
using WebHomePro.Pages.Admin;
using WebHomePro.Services;


namespace WebHomePro.Services.IProveedorService
{
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

    public interface IProveedorService
    {  /*
        Task<List<LineaDisponibleDto>> ObtenerLineasDisponiblesAsync(); // ws proveedor 1
        Task<RespuestaWs> ActivarLineaAsync(string numero, string idTelefono, string idTarjeta, string tipo, string cedula); //ws proveedor 2

        Task<List<LineaNuevaVm>> GetLineasNuevasDisponiblesAsync(CancellationToken ct);
        // -> consume WS_PROVEEDOR, retorna líneas sin vender

        Task<OperacionResponse> ActivarLineaAsync(ActivarLineaRequestDto req, CancellationToken ct);
        // -> */


        Task<List<LineaDisponibleDto>> ObtenerLineasDisponiblesAsync();
        Task<RespuestaWs> ActivarLineaAsync(string numero, string idTelefono, string idTarjeta, string tipo, string cedula);

        Task<List<LineaNuevaVm>> GetLineasNuevasDisponiblesAsync(CancellationToken ct = default);
        Task<OperacionResponse> ActivarLineaAsync(ActivarLineaRequestDto req, CancellationToken ct = default);
    }
   
}
