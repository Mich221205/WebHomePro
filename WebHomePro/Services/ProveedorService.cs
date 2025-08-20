using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using WebHomePro.Services.Dto;
using WSProveedorRef;

namespace WebHomePro.Services.IProveedorService
{
    public class ProveedorService : IProveedorService
    {
        private readonly IConfiguration _cfg;
        public ProveedorService(IConfiguration cfg) => _cfg = cfg;

        // =========================
        //  Cliente WCF (ASMX/SOAP)
        // =========================
        private WSProveedorSoapClient CrearCliente()
        {
            var url = _cfg["WSProveedor:BaseAddress"];
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("Debe configurar WSProveedor:BaseAddress en appsettings.json (ej: \"https://localhost:44354/WSProveedor1/WSProveedor.asmx\").");

            var isHttps = url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            var binding = new BasicHttpBinding(isHttps ? BasicHttpSecurityMode.Transport : BasicHttpSecurityMode.None)
            {
                MaxReceivedMessageSize = 10 * 1024 * 1024,
                OpenTimeout = TimeSpan.FromSeconds(15),
                CloseTimeout = TimeSpan.FromSeconds(15),
                SendTimeout = TimeSpan.FromSeconds(30),
                ReceiveTimeout = TimeSpan.FromSeconds(30),
                ReaderQuotas =
                {
                    MaxArrayLength = 1024 * 1024,
                    MaxStringContentLength = 1024 * 1024
                }
            };

            var endpoint = new EndpointAddress(url);
            return new WSProveedorSoapClient(binding, endpoint);
        }

        private static async Task CerrarSeguroAsync(WSProveedorSoapClient client)
        {
            try
            {
                if (client.State == CommunicationState.Faulted)
                    client.Abort();
                else
                    await client.CloseAsync();
            }
            catch
            {
                client.Abort();
            }
        }

        // ===========================================
        //  Métodos "clásicos" que ya usas en tu UI
        // ===========================================

        public async Task<List<LineaDisponibleDto>> ObtenerLineasDisponiblesAsync()
        {
            WSProveedorSoapClient? client = null;
            try
            {
                client = CrearCliente();
                var resp = await client.ObtenerLineasDisponiblesAsync(); // Ajusta al nombre real del método en tu Reference.cs
                var items = resp.Body.ObtenerLineasDisponiblesResult;    // Ajusta al shape real de tu proxy

                return (items ?? Array.Empty<WSProveedorRef.LineaDisponible>())
                    .Select(x => new LineaDisponibleDto
                    {
                        NumeroTelefono = x.Numero,
                        IdTarjeta = x.IdTarjeta,
                        IdTelefono = x.IdTelefono,
                        Tipo = x.Tipo
                    })
                    .ToList();
            }
            catch
            {
                // Devuelve vacío para que la UI muestre "sin datos"
                return new List<LineaDisponibleDto>();
            }
            finally
            {
                if (client != null) await CerrarSeguroAsync(client);
            }
        }

        public async Task<RespuestaWs> ActivarLineaAsync(string numero, string idTelefono, string idTarjeta, string tipo, string cedula)
        {
            WSProveedorSoapClient? client = null;
            try
            {
                client = CrearCliente();
                var estado = "ACTIVO"; // ADM4 exige activar con estado Activo

                // Ajusta al nombre real del método y parámetros del proxy
                var r = await client.ActivarDesactivarLineaAsync(numero, idTelefono, idTarjeta, tipo, cedula, estado);
                var res = r.Body.ActivarDesactivarLineaResult;

                return new RespuestaWs
                {
                    Exitoso = res?.Resultado ?? false,
                    Mensaje = res?.Mensaje ?? string.Empty
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
            finally
            {
                if (client != null) await CerrarSeguroAsync(client);
            }
        }

        // =====================================================
        //  Métodos "nuevos" que usa el PageModel propuesto
        //  (quedan como thin adapters sobre los anteriores)
        // =====================================================

        // GET: líneas nuevas disponibles (sin vender) para ADM4
        public async Task<List<LineaNuevaVm>> GetLineasNuevasDisponiblesAsync(CancellationToken ct = default)
        {
            // Reuso tu método existente + mapeo
            // Nota: los métodos del proxy no aceptan CancellationToken, por eso
            // no se propaga; si quieres cortar por CT, podrías usar .WaitAsync(ct).
            var lista = await ObtenerLineasDisponiblesAsync();
            return lista.Select(x => new LineaNuevaVm
            {
                NumeroTelefono = x.NumeroTelefono,
                IdentificadorTelefono = x.IdTelefono,
                IdentificadorTarjeta = x.IdTarjeta,
                TipoServicio = x.Tipo
            }).ToList();
        }

        // POST: activar (DTO completo) — versión usada en el PageModel
        public async Task<OperacionResponse> ActivarLineaAsync(ActivarLineaRequestDto req, CancellationToken ct = default)
        {
            var r = await ActivarLineaAsync(
                numero: req.NumeroTelefono,
                idTelefono: req.IdentificadorTelefono,
                idTarjeta: req.IdentificadorTarjeta,
                tipo: req.TipoServicio,
                cedula: req.CedulaCliente
            );

            return new OperacionResponse
            {
                Exitoso = r.Exitoso,
                Mensaje = string.IsNullOrWhiteSpace(r.Mensaje) && r.Exitoso
                            ? "¡Proceso finalizado de forma exitosa!"
                            : (r.Mensaje ?? string.Empty)
            };
        }
    }

    // ================================
    //  VM/DTO usados por el PageModel
    // ================================
    public class LineaNuevaVm
    {
        public string NumeroTelefono { get; set; } = default!;
        public string IdentificadorTelefono { get; set; } = default!;
        public string IdentificadorTarjeta { get; set; } = default!;
        public string TipoServicio { get; set; } = default!;
    }

    public class ActivarLineaRequestDto
    {
        public string NumeroTelefono { get; set; } = default!;
        public string IdentificadorTelefono { get; set; } = default!;
        public string IdentificadorTarjeta { get; set; } = default!;
        public string TipoServicio { get; set; } = default!;
        public string CedulaCliente { get; set; } = default!;
        public string Estado { get; set; } = "ACTIVO";
    }

    public class OperacionResponse
    {
        public bool Exitoso { get; set; }
        public string? Mensaje { get; set; }
    }
}
