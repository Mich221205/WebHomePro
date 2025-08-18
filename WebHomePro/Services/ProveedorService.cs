using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using WebHomePro.Services.Dto;
using WSProveedorRef;

namespace WebHomePro.Services.IProveedorService
{
    public class ProveedorService : IProveedorService
    {
        private readonly IConfiguration _cfg;
        public ProveedorService(IConfiguration cfg) => _cfg = cfg;
        

        /*private WSProveedorSoapClient CrearCliente()
        {
            // Endpoint de tu ASMX
            var url = _cfg["WSProveedor:BaseAddress"] ?? throw new InvalidOperationException("Falta WSProveedor:BaseAddress");

            // ASMX con BasicHttpBinding
            var binding = new BasicHttpBinding(BasicHttpSecurityMode.None);
            binding.MaxReceivedMessageSize = 1024 * 1024 * 10;

            var endpoint = new EndpointAddress(url); // ej: https://tu_host/WSProveedor.asmx
            return new WSProveedorSoapClient(WSProveedorSoapClient.EndpointConfiguration.WSProveedorSoap, url); // Use 'url' directly instead of 'endpoint.Address.ToString()'
        }*/

        private WSProveedorSoapClient CrearCliente()
        {
            var url = _cfg["WSProveedor:BaseAddress"]
                ?? throw new InvalidOperationException("Falta WSProveedor:BaseAddress");

            var binding = new BasicHttpBinding(BasicHttpSecurityMode.None)
            {
                MaxReceivedMessageSize = 10 * 1024 * 1024
            };
            var endpoint = new EndpointAddress(url);

            // Usa el ctor (binding, endpoint) de tu Reference.cs
            return new WSProveedorSoapClient(binding, endpoint);
        }

        public async Task<List<LineaDisponibleDto>> ObtenerLineasDisponiblesAsync()
        {
            try
            {
                using var client = CrearCliente();
                var resp = await client.ObtenerLineasDisponiblesAsync(); // ← nombre 1:1 con tu WS

                // Si devuelve un array/clase con propiedad:
                var items = resp.Body.ObtenerLineasDisponiblesResult; // ajusta según Reference.cs

                return items.Select(x => new LineaDisponibleDto
                {
                    NumeroTelefono = x.Numero,
                    IdTarjeta = x.IdTarjeta,
                    IdTelefono = x.IdTelefono,   // gracias al fix del paso 1
                    Tipo = x.Tipo
                }).ToList();
            }
            catch (Exception ex)
            {
                // Devuelve vacío si falla (la UI puede mostrar “sin datos”)
                return new List<LineaDisponibleDto>();
            }
        }

        public async Task<RespuestaWs> ActivarLineaAsync(string numero, string idTelefono, string idTarjeta, string tipo, string cedula)
        {
            try
            {
                using var client = CrearCliente();
                var estado = "Activo";

                // Tu firma exacta:
                var r = await client.ActivarDesactivarLineaAsync(numero, idTelefono, idTarjeta, tipo, cedula, estado);

                var res = r.Body.ActivarDesactivarLineaResult; // ajusta según Reference.cs
                return new RespuestaWs
                {
                    Exitoso = res.Resultado,
                    Mensaje = res.Mensaje ?? ""
                };
            }
            catch (TimeoutException tex)
            {
                return new RespuestaWs { Exitoso = false, Mensaje = "Timeout: " + tex.Message };
            }
            catch (CommunicationException cex)
            {
                return new RespuestaWs { Exitoso = false, Mensaje = "Error de comunicación: " + cex.Message };
            }
            catch (Exception ex)
            {
                return new RespuestaWs { Exitoso = false, Mensaje = "Error: " + ex.Message };
            }
        }


        
    }
}
