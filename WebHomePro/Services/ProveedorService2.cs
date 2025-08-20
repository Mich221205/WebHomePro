using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WSProveedorRef;

namespace WebHomePro.Services
{
    public interface ProveedorService2
    {
        Task<List<LineaPrepagoVm>> GetLineasPrepagoAsync();
        Task<List<LineaPostpagoVm>> GetLineasPostpagoAsync();
    }

    public class LineaPrepagoVm
    {
        public string Telefono { get; set; } = "";
        public decimal SaldoDisponible { get; set; }
    }

    public class LineaPostpagoVm
    {
        public string Telefono { get; set; } = "";
        public decimal MontoPendiente { get; set; }
    }

    public class ProveedorService : ProveedorService2
    {
        private readonly WSProveedorSoapClient _ws;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ProveedorService(WSProveedorSoapClient ws, IHttpContextAccessor httpContextAccessor)
        {
            _ws = ws;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<List<LineaPrepagoVm>> GetLineasPrepagoAsync()
        {
            var resp = await _ws.ListarLineasPrepagoPorClienteAsync();
            return resp.Body.ListarLineasPrepagoPorClienteResult
                .Select(x => new LineaPrepagoVm
                {
                    Telefono = x.Telefono,
                    SaldoDisponible = x.SaldoDisponible
                })
                .ToList();
        }

        public async Task<List<LineaPostpagoVm>> GetLineasPostpagoAsync()
        {
            // ✅ Recuperar IdCliente desde Session mediante HttpContextAccessor
            var idCliente = _httpContextAccessor.HttpContext?.Session.GetInt32("IdCliente");
            if (idCliente == null)
                return new List<LineaPostpagoVm>();

            // ✅ Llamada al WS con el idCliente
            var resp = await _ws.ListarLineasPostpagoPorClienteAsync(idCliente.Value);

            return resp.Body.ListarLineasPostpagoPorClienteResult
                .Select(x => new LineaPostpagoVm
                {
                    Telefono = x.Telefono,
                    MontoPendiente = x.MontoPendiente
                })
                .ToList();
        }
    }
}
