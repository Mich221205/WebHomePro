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
        private readonly WSProveedorSoapClient _soap;

        public ProveedorService(IConfiguration cfg, WSProveedorSoapClient soap)
        {
            _cfg = cfg;
            _soap = soap;
        }

        // (Opcional) Factory si en algún momento quieres crear cliente por llamada
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

        // ===================== CLIENTE4: prepago y postpago =====================

        public async Task<List<LineaPrepagoVm>> GetLineasPrepagoAsync(string cedula)
        {
            var resp = await _soap.ListarLineasPrepagoPorClienteAsync(cedula);
            var dto = resp.Body.ListarLineasPrepagoPorClienteResult;

            var list = new List<LineaPrepagoVm>();
            if (dto != null)
            {
                foreach (var l in dto)
                {
                    list.Add(new LineaPrepagoVm
                    {
                        Telefono = l.Telefono,
                        SaldoDisponible = l.SaldoDisponible   
                    });
                }
            }
            return list;
        }


        public async Task<List<LineaPostpagoVm>> GetLineasPostpagoAsync(string cedula)
        {
            var resp = await _soap.ObtenerLineasPostpagoPorCedulaAsync(cedula);
            var dto = resp.Body.ObtenerLineasPostpagoPorCedulaResult;

            var list = new List<LineaPostpagoVm>();
            if (dto != null)
            {
                foreach (var l in dto)
                {
                    list.Add(new LineaPostpagoVm
                    {
                        Telefono = l.Telefono,
                        MontoPendiente = l.MontoPendiente
                    });
                }
            }
            return list;
        }



        // ===================== ADM: disponibles y activar =====================

        public async Task<List<LineaDisponibleDto>> ObtenerLineasDisponiblesAsync()
        {
            // Uso CrearCliente() aquí porque ya lo tenías así
            WSProveedorSoapClient? client = null;
            try
            {
                client = CrearCliente();
                var resp = await client.ObtenerLineasDisponiblesAsync();
                var items = resp.Body.ObtenerLineasDisponiblesResult;

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
                var estado = "ACTIVO"; // activar

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

        public async Task<List<LineaNuevaVm>> GetLineasNuevasDisponiblesAsync(CancellationToken ct = default)
        {
            var lista = await ObtenerLineasDisponiblesAsync();
            return lista.Select(x => new LineaNuevaVm
            {
                NumeroTelefono = x.NumeroTelefono,
                IdentificadorTelefono = x.IdTelefono,
                IdentificadorTarjeta = x.IdTarjeta,
                TipoServicio = x.Tipo
            }).ToList();
        }

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

        // ===================== CLIENTE5: prepago (listar/recargar/saldo) =====================

        public async Task<List<LineaPrepagoVm>> ObtenerLineasPrepagoAsync(string? cedula)
        {
            var resp = await _soap.ObtenerLineasPrepagoAsync(cedula);
            var dto = resp.Body.ObtenerLineasPrepagoResult;

            var list = new List<LineaPrepagoVm>();
            if (dto?.Resultado == true && dto.Lineas != null)
            {
                foreach (var l in dto.Lineas)
                {
                    list.Add(new LineaPrepagoVm
                    {
                        Telefono = l.Telefono,
                        SaldoDisponible = l.Saldo
                    });
                }
            }
            return list;
        }


        // Fallback con cédula: lista y filtra por teléfono
        public async Task<decimal> ObtenerSaldoPrepagoAsync(string telefono, string? cedula)
        {
            var telNorm = SoloDigitos(telefono);

            var resp = await _soap.ObtenerLineasPrepagoAsync(cedula);
            var result = resp.Body.ObtenerLineasPrepagoResult;

            if (result?.Resultado == true && result.Lineas != null)
            {
                var linea = result.Lineas.FirstOrDefault(l => SoloDigitos(l.Telefono) == telNorm);
                if (linea != null)
                    return linea.Saldo;
            }
            return 0m;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="telefono"></param>
        /// <param name="monto"></param>
        /// <returns></returns>
        public async Task<(bool ok, string? mensaje)> RecargarSaldoPrepagoAsync(string telefono, int monto)
        {
            // El WS espera monto como string (entero, sin decimales)
            var resp = await _soap.RecargarSaldoPrepagoAsync(
                telefono,
                monto.ToString(),
                tarjeta: null,
                titular: null,
                exp: null,
                cvv: null
            );

            var dto = resp.Body.RecargarSaldoPrepagoResult;
            return (dto?.Resultado == true, dto?.Mensaje ?? "Error desconocido");
        }

        // ===================== helpers =====================

        private static string SoloDigitos(string? s)
            => new string((s ?? string.Empty).Where(char.IsDigit).ToArray());


        // Compat: sin cédula (la interfaz lo exige)
        public Task<decimal> ObtenerSaldoPrepagoAsync(string telefono)
            => ObtenerSaldoPrepagoAsync(telefono, null);

        // Devuelve una línea prepago específica buscando por teléfono (útil en UI)
        public async Task<LineaPrepagoVm?> ObtenerLineaPrepagoAsync(string telefono)
        {
            var resp = await _soap.ObtenerLineasPrepagoAsync(string.Empty);
            var dto = resp.Body.ObtenerLineasPrepagoResult;

            if (dto?.Resultado == true && dto.Lineas != null)
            {
                var telNorm = SoloDigitos(telefono);
                var match = dto.Lineas.FirstOrDefault(l => SoloDigitos(l.Telefono) == telNorm);
                if (match != null)
                {
                    return new LineaPrepagoVm
                    {
                        Telefono = match.Telefono,
                        SaldoDisponible = match.Saldo
                    };
                }
            }
            return null;
        }
    }

    // ==============================
    //  VM/DTO usados por el PageModel
    // ==============================
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