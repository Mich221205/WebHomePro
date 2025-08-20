using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
using WebHomePro.Services.Dto;
using WebHomePro.Services.IFacturacionServices;
using WSProveedorRef;

namespace WebHomePro.Services
{
    public class FacturacionService : IFacturacionService
    {
        private readonly string _wsUrl;
        private readonly ILogger<FacturacionService> _logger;

        public FacturacionService(IConfiguration cfg, ILogger<FacturacionService> logger)
        {
            _logger = logger;

            // Preferir misma clave que usas en otros servicios: WSProveedor:BaseAddress
            _wsUrl = cfg["WSProveedor:BaseAddress"];

            if (string.IsNullOrWhiteSpace(_wsUrl))
            {
                _wsUrl = "https://localhost:44354/WSProveedor1/WSProveedor.asmx"; // fallback seguro
                _logger.LogWarning("WSProveedor:BaseAddress no está configurada. Usando URL por defecto: {Url}", _wsUrl);
            }
        }

        private WSProveedorSoapClient CrearCliente()
        {
            var isHttps = _wsUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            var binding = new BasicHttpBinding(isHttps ? BasicHttpSecurityMode.Transport : BasicHttpSecurityMode.None)
            {
                MaxReceivedMessageSize = 10_000_000,
                OpenTimeout = TimeSpan.FromSeconds(30),
                SendTimeout = TimeSpan.FromMinutes(2),
                ReceiveTimeout = TimeSpan.FromMinutes(2)
            };

            var endpoint = new EndpointAddress(_wsUrl);
            return new WSProveedorSoapClient(binding, endpoint);
        }

        private static async Task CerrarSeguroAsync(WSProveedorSoapClient client)
        {
            try
            {
                if (client.State == CommunicationState.Faulted) client.Abort();
                else await client.CloseAsync();
            }
            catch { client.Abort(); }
        }

        public async Task<UltimaFacturacionDto?> GetUltimaFacturacionAsync()
        {
            WSProveedorSoapClient? client = null;
            try
            {
                client = CrearCliente();

                // La mayoría de proxies generan método sin Request manual
                var resp = await client.ObtenerUltimaFacturacionAsync();
                var data = resp?.Body?.ObtenerUltimaFacturacionResult;

                if (data == null)
                    return null;

                // Si el WS dice que no hay facturación previa, devolver null para que la UI muestre vacío
                if (!data.Resultado && (data.Mensaje?.IndexOf("No hay facturaciones", StringComparison.OrdinalIgnoreCase) >= 0))
                    return null;

                if (!data.Resultado)
                    throw new InvalidOperationException(data.Mensaje ?? "Error al consultar la última facturación.");

                return new UltimaFacturacionDto
                {
                    FechaCalculo = data.FechaCalculo,
                    FechaMPago = data.FechaMaxPago,
                    FechaCobro = data.FechaCobro,
                    Total = data.Total
                };
            }
            finally
            {
                if (client != null) await CerrarSeguroAsync(client);
            }
        }

        public async Task<RespuestaFacturacionDto> EjecutarCalculoFacturacionAsync(DateTime inicio, DateTime fin)
        {
            WSProveedorSoapClient? client = null;
            try
            {
                client = CrearCliente();

                // Formato yyyy-MM-dd como suele esperar el WS
                var fIni = inicio.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                var fFin = fin.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

                var resp = await client.CalcularCobroPostpagoAsync(fIni, fFin);
                var r = resp?.Body?.CalcularCobroPostpagoResult;

                return new RespuestaFacturacionDto
                {
                    Exitoso = r?.Resultado == true,
                    Mensaje = r?.Resultado == true
                        ? "¡Proceso finalizado de forma exitosa!"
                        : (string.IsNullOrWhiteSpace(r?.Mensaje) ? "Error al realizar el proceso" : r.Mensaje),
                    Total = 0m // Si el WS devuelve total, mapea aquí
                };
            }
            catch (Exception ex)
            {
                return new RespuestaFacturacionDto { Exitoso = false, Mensaje = $"Error al realizar el proceso: {ex.Message}" };
            }
            finally
            {
                if (client != null) await CerrarSeguroAsync(client);
            }
        }
    }
}
