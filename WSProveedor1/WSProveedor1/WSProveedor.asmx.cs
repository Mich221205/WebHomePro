using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Services;
using TestServicio;
using static WSProveedor1.WSProveedor;

namespace WSProveedor1
{
    /// <summary>
    /// Summary description for WSProveedor
    /// </summary>
    [WebService(Namespace = "http://wsproveedor1.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    // [System.Web.Script.Services.ScriptService]

    public class WSProveedor : System.Web.Services.WebService
    {

        public static class AESDecryptor
        {
            public static string Decrypt(string cipherText)
            {
                if (string.IsNullOrWhiteSpace(cipherText) || !cipherText.StartsWith("ENC:"))
                    return cipherText; // No está cifrado

                cipherText = cipherText.Substring(4); // quitar "ENC:"
                byte[] buffer = Convert.FromBase64String(cipherText);

                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = Encoding.UTF8.GetBytes(AESConfig.Key);
                    aesAlg.IV = Encoding.UTF8.GetBytes(AESConfig.IV);
                    aesAlg.Mode = CipherMode.CBC;
                    aesAlg.Padding = PaddingMode.PKCS7;

                    using (var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV))
                    using (var msDecrypt = new MemoryStream(buffer))
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (var srDecrypt = new StreamReader(csDecrypt))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
        }

        public static class AESConfig
        {
            public static string Key = "1234567890123456"; // misma clave que en el cliente
            public static string IV = "6543210987654321";  // mismo IV que en el cliente
        }

        public static class AESEncryptor
        {
            public static string Encrypt(string plainText)
            {
                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = Encoding.UTF8.GetBytes(AESConfig.Key);
                    aesAlg.IV = Encoding.UTF8.GetBytes(AESConfig.IV);
                    aesAlg.Mode = CipherMode.CBC;
                    aesAlg.Padding = PaddingMode.PKCS7;

                    using (var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV))
                    using (var msEncrypt = new MemoryStream())
                    {
                        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        using (var swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                        return "ENC:" + Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }
        }

        public class ClientesSQL
        {
            public string CEDULA { get; set; }
            public string NOMBRE_COMPLETO { get; set; }
        }

        public class Resultado
        {
            public bool RESULTADO { get; set; }
            public string Mensaje { get; set; }
        }


        [WebMethod]
        public Resultado InsertarClienteBasico(string cedula, string nombre)
        {
            try
            {
                using (var ctx = new COMPANIA_TELEFONICAEntities1())
                {
                    var nuevoCliente = new CLIENTE
                    {
                        CEDULA = cedula,
                        NOMBRE = nombre
                    };

                    ctx.CLIENTES.Add(nuevoCliente);
                    ctx.SaveChanges();
                }

                return new Resultado
                {
                    RESULTADO = true,
                    Mensaje = "Registro exitoso en SQL Server"
                };
            }
            catch (Exception ex)
            {
                return new Resultado
                {
                    RESULTADO = false,
                    Mensaje = "Error al insertar en SQL Server: " + ex.Message
                };
            }
        }

        [WebMethod]
        public RespuestaIngresarServicio EliminarLinea(string numeroTelefono)
        {
            if (string.IsNullOrWhiteSpace(numeroTelefono))
            {
                return new RespuestaIngresarServicio { Resultado = false, Mensaje = "Número no válido" };
            }

            try
            {
                // Encriptar igual que en OnPostNueva
                string numEnc = AESEncryptor.Encrypt(numeroTelefono);

                using (var db = new COMPANIA_TELEFONICAEntities1())
                {
                    var linea = db.TELEFONOS.FirstOrDefault(t => t.NUM_TELEFONO == numEnc);
                    if (linea == null)
                        return new RespuestaIngresarServicio { Resultado = false, Mensaje = "No existe la línea" };

                    db.TELEFONOS.Remove(linea);
                    db.SaveChanges();

                    return new RespuestaIngresarServicio { Resultado = true, Mensaje = "Línea eliminada correctamente" };
                }
            }
            catch (Exception ex)
            {
                return new RespuestaIngresarServicio { Resultado = false, Mensaje = "Error: " + ex.Message };
            }
        }

        public class RespuestaEliminarLinea
        {
            public bool Resultado { get; set; }
            public string Mensaje { get; set; }
        }
        

        [WebMethod]
        public RespuestaIngresarServicio IngresarNuevoServicio(string numeroTelefono, string idTelefono, string idTarjeta, string tipo, string estado)
        {
            if (string.IsNullOrEmpty(numeroTelefono) || string.IsNullOrEmpty(idTelefono)
                || string.IsNullOrEmpty(idTarjeta) || string.IsNullOrEmpty(tipo) || string.IsNullOrEmpty(estado))
            {
                return new RespuestaIngresarServicio { Resultado = false, Mensaje = "Datos incompletos" };
            }

            try
            {
                var tramaJson = new
                {
                    tipo_transaccion = "3",
                    telefono = numeroTelefono,
                    identificadorTel = idTelefono,
                    identificador_tarjeta = idTarjeta,
                    tipo = tipo.ToLower(),
                    estado = estado.ToLower()
                };

                string respuesta = EnviarAlProveedor(JsonConvert.SerializeObject(tramaJson));

                Console.WriteLine("DEBUG RESPUESTA WS: [" + respuesta + "]");

                if (respuesta.Contains("\"status\":\"OK\"") || respuesta.Trim() == "OK")
                {
                    return new RespuestaIngresarServicio { Resultado = true, Mensaje = "Exitoso" };
                }
                else
                {
                    dynamic respObj = JsonConvert.DeserializeObject(respuesta);
                    return new RespuestaIngresarServicio
                    {
                        Resultado = false,
                        Mensaje = respObj.mensaje != null ? (string)respObj.mensaje : "Problemas al incluir la información"
                    };
                }
            }
            catch (Exception ex)
            {
                return new RespuestaIngresarServicio { Resultado = false, Mensaje = $"Error: {ex.Message}" };
            }
        }


        

        [WebMethod]
        public List<LineaDisponible> ObtenerLineasDisponibles()
        {
            using (var db = new COMPANIA_TELEFONICAEntities1())
            {
                var datos = db.TELEFONOS
                    .Where(t => t.ID_ESTADO == 3) // 'disponibles/sin vender'
                    .Select(t => new
                    {
                        t.ID_TELEFONO,           
                        t.NUM_TELEFONO,
                        t.IDENTIFICADOR_TARJETA,
                        Tipo = t.TIPO_TELEFONO1.DESCRIPCION
                    })
                    .ToList();

                var lineas = datos.Select(t => new LineaDisponible
                {
                    IdTelefono = t.ID_TELEFONO.ToString(),                    
                    Numero = AESDecryptor.Decrypt(t.NUM_TELEFONO),
                    IdTarjeta = AESDecryptor.Decrypt(t.IDENTIFICADOR_TARJETA),
                    Tipo = t.Tipo
                }).ToList();

                return lineas;
            }
        }



        [WebMethod]
        public RespuestaActivarDesactivar ActivarDesactivarLinea(
        string numeroTelefono, string idTelefono, string idTarjeta,
        string tipo, string identificacionDuenio, string estado)
        {
            if (string.IsNullOrWhiteSpace(numeroTelefono) ||
                string.IsNullOrWhiteSpace(idTelefono) ||
                string.IsNullOrWhiteSpace(idTarjeta) ||
                string.IsNullOrWhiteSpace(tipo) ||
                string.IsNullOrWhiteSpace(identificacionDuenio) ||
                string.IsNullOrWhiteSpace(estado))
                return new RespuestaActivarDesactivar { Resultado = false, Mensaje = "Datos incompletos" };

            try
            {
                // 1) Normalizar estado -> activar / desactivar
                string e = (estado ?? "").Trim().ToLowerInvariant();
                if (e == "activo" || e == "activado" || e == "1" || e == "true" || e == "on") e = "activar";
                else if (e == "inactivo" || e == "desactivado" || e == "2" || e == "false" || e == "off") e = "desactivar";
                if (e != "activar" && e != "desactivar")
                    return new RespuestaActivarDesactivar { Resultado = false, Mensaje = "Estado inválido. Use 'activar' o 'desactivar'." };

                // 2) Normalizar tipo -> prepago / postpago
                string t = (tipo ?? "").Trim().ToLowerInvariant();
                if (int.TryParse(t, out var n)) t = (n == 1 ? "prepago" : (n == 2 ? "postpago" : t));
                if (t == "pago" || t == "post-pago" || t == "pospago") t = "postpago";

                // 3) Resolver 'duenio' a CÉDULA (si te llega ID_CLIENTE u otro formato)
                var duenioRaw = (identificacionDuenio ?? "").Trim();
                var duenioDigits = System.Text.RegularExpressions.Regex.Replace(duenioRaw, "[^0-9]", "");
                string duenioProv = duenioRaw; // por defecto, lo que llegó

                if (!string.IsNullOrEmpty(duenioDigits))
                {

                    var ced = ObtenerCedulaPorIdCliente(duenioDigits);

                    duenioProv = !string.IsNullOrWhiteSpace(ced)
                        ? System.Text.RegularExpressions.Regex.Replace(ced, "[^0-9]", "")
                        : duenioDigits;
                }

                int estadoCode = (e == "activar") ? 1 : 2;


                var trama = new
                {
                    tipo_transaccion = "6",
                    telefono = numeroTelefono?.Trim(),
                    identificadorTel = idTelefono?.Trim(),
                    identificador_tarjeta = idTarjeta?.Trim(),
                    tipo = t,
                    duenio = duenioProv,
                    estado = estadoCode
                };

                string json = JsonConvert.SerializeObject(trama);
                System.Diagnostics.Debug.WriteLine($"WSProveedor -> JSON: {json}");

                string resp = EnviarAlProveedor(json);
                System.Diagnostics.Debug.WriteLine("WSProveedor <- RESP: " + resp);

                if (resp.Contains("\"status\":\"OK\"") || resp.Trim().Equals("OK", StringComparison.OrdinalIgnoreCase))
                    return new RespuestaActivarDesactivar { Resultado = true, Mensaje = "Exitoso" };

                dynamic obj = JsonConvert.DeserializeObject(resp);
                return new RespuestaActivarDesactivar
                {
                    Resultado = false,
                    Mensaje = obj?.mensaje != null ? (string)obj.mensaje : "Problemas al activar/desactivar línea"
                };
            }
            catch (Exception ex)
            {
                return new RespuestaActivarDesactivar { Resultado = false, Mensaje = "Error: " + ex.Message };
            }
        }

        private static string ObtenerCedulaPorIdCliente(string idCliente)
        {
            if (string.IsNullOrWhiteSpace(idCliente)) return null;
            string cs = "Server=EINSTEIN\\SQLINST1;Database=COMPANIA_TELEFONICA;User Id=sa;Password=2112;";
            using (var cn = new SqlConnection(cs))
            {
                cn.Open();
                using (var cmd = new SqlCommand("SELECT CEDULA FROM dbo.CLIENTES WHERE ID_CLIENTE=@id", cn))
                {
                    cmd.Parameters.AddWithValue("@id", idCliente);
                    var o = cmd.ExecuteScalar();
                    return o?.ToString();
                }
            }
        }

        [WebMethod]
        public List<LineaEnUso> ListarLineasEnUso()
        {
            var lista = new List<LineaEnUso>();
            try
            {
                string cs = "Server=EINSTEIN\\SQLINST1;Database=COMPANIA_TELEFONICA;User Id=sa;Password=2112;";
                using (var conn = new SqlConnection(cs))
                {
                    conn.Open();
                    string sql = @"
                        SELECT
                            t.NUM_TELEFONO,
                            t.IDENTIFICADOR_TARJETA,
                            t.IDENTIFICADOR_TELEFONO,
                            c.CEDULA,
                            c.NOMBRE,
                            TIPO_TELEFONO.DESCRIPCION
                        FROM dbo.TELEFONOS t
                        INNER JOIN dbo.CLIENTES c ON c.ID_CLIENTE = t.ID_CLIENTE
                        inner join TIPO_TELEFONO on t.TIPO_TELEFONO = TIPO_TELEFONO.ID_T_TELEFONO
                        WHERE t.ID_CLIENTE IS NOT NULL
                          AND t.ID_ESTADO = 1;";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            lista.Add(new LineaEnUso
                            {
                                NUM_TELEFONO = dr["NUM_TELEFONO"].ToString(),
                                IDENTIFICADOR_TARJETA = dr["IDENTIFICADOR_TARJETA"].ToString(),
                                IDENTIFICADOR_TELEFONO = dr["IDENTIFICADOR_TELEFONO"].ToString(),
                                CEDULA = dr["CEDULA"].ToString(),
                                NOMBRE = dr["NOMBRE"].ToString(),
                                TIPO_TELEFONO = dr["DESCRIPCION"].ToString()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener líneas en uso: " + ex.Message);
            }
            return lista;
        }

        private static bool ColumnExists(SqlConnection conn, string table, string column)
        {
            using (var cmd = new SqlCommand(@"
             SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
             WHERE TABLE_NAME=@t AND COLUMN_NAME=@c;", conn))
            {
                cmd.Parameters.AddWithValue("@t", table);
                cmd.Parameters.AddWithValue("@c", column);
                return cmd.ExecuteScalar() != null;
            }
        }
        public class LineaEnUso
        {
            public string NUM_TELEFONO { get; set; }
            public string IDENTIFICADOR_TARJETA { get; set; }
            public string IDENTIFICADOR_TELEFONO { get; set; }
            public string CEDULA { get; set; }
            public string NOMBRE { get; set; }
            public string TIPO_TELEFONO { get; set; }
        }

        [WebMethod]
        public RespuestaCalculoPostpago CalcularCobroPostpago(string fechaCalculo, string fechaMaxPago)
        {
            if (string.IsNullOrWhiteSpace(fechaCalculo) || string.IsNullOrWhiteSpace(fechaMaxPago))
            {
                return new RespuestaCalculoPostpago { Resultado = false, Mensaje = "Datos incompletos" };
            }

            try
            {
                var tramaJson = new
                {
                    tipo_transaccion = "7",
                    fecha_calculo = fechaCalculo,
                    fecha_max_pago = fechaMaxPago
                };

                string respuesta = EnviarAlProveedor(JsonConvert.SerializeObject(tramaJson));

                if (respuesta.Contains("\"status\":\"OK\""))
                {
                    return new RespuestaCalculoPostpago { Resultado = true, Mensaje = "Exitoso" };
                }
                else
                {
                    dynamic obj = JsonConvert.DeserializeObject(respuesta);
                    return new RespuestaCalculoPostpago
                    {
                        Resultado = false,
                        Mensaje = obj.mensaje != null ? (string)obj.mensaje : "Problemas al calcular postpago"
                    };
                }
            }
            catch (Exception ex)
            {
                return new RespuestaCalculoPostpago { Resultado = false, Mensaje = $"Error: {ex.Message}" };
            }
        }
        /// <summary>
        /// ///CalculoFacturacionResponse
        /// </summary>
        public class UltimaFacturacionResponse
        {
            public bool Resultado { get; set; }
            public string Mensaje { get; set; }
            public DateTime? FechaCalculo { get; set; }
            public DateTime? FechaMaxPago { get; set; } // FECHA_M_PAGO
            public DateTime? FechaCobro { get; set; }
            public decimal Total { get; set; }
        }


        [WebMethod]
        public UltimaFacturacionResponse ObtenerUltimaFacturacion()
        {
            try
            {
                using (var db = new COMPANIA_TELEFONICAEntities1())
                {
                    var ultima = db.COBROS
                        .OrderByDescending(c => c.FECHA_COBRO)
                        .FirstOrDefault();

                    if (ultima == null)
                    {
                        return new UltimaFacturacionResponse
                        {
                            Resultado = true,
                            Mensaje = "No hay facturaciones previas.",
                            FechaCalculo = null,
                            FechaMaxPago = null,
                            FechaCobro = null,
                            Total = 0m
                        };
                    }

                    return new UltimaFacturacionResponse
                    {
                        Resultado = true,
                        Mensaje = "OK",
                        FechaCalculo = ultima.FECHA_CALCULO,
                        FechaMaxPago = ultima.FECHA_M_PAGO,
                        FechaCobro = ultima.FECHA_COBRO,
                        Total = ultima.COBRO_TOTAL
                    };
                }
            }
            catch (Exception ex)
            {
                return new UltimaFacturacionResponse
                {
                    Resultado = false,
                    Mensaje = "Error al consultar la última facturación: " + ex.Message
                };
            }
        }
        //sdfsfs

        public class LineaPrepagoVM
        {
            public string Telefono { get; set; }
            public decimal Saldo { get; set; }
        }

        public class LineasPrepagoResponse
        {
            public bool Resultado { get; set; }
            public string Mensaje { get; set; }
            public List<LineaPrepagoVM> Lineas { get; set; }
        }

        public class RecargaResponse
        {
            public bool Resultado { get; set; }
            public string Mensaje { get; set; }
            public decimal? NuevoSaldo { get; set; }
        }
        

        [WebMethod]
        public LineasPrepagoResponse ObtenerLineasPrepago(string cedula = null)
        {
            try
            {
                // Armar JSON para el proveedor
                var payload = new Dictionary<string, string>
                {
                    ["tipo_transaccion"] = "8"
                };
                if (!string.IsNullOrWhiteSpace(cedula))
                    payload["cedula"] = cedula.Trim();

                string json = JsonConvert.SerializeObject(payload);
                string resp = EnviarAlProveedor(json);   // usa tu socket a 127.0.0.1:6000

                // Se espera algo como:
                // {"status":"OK","lineas":[{"telefono":"70001234","saldo":1234.56}, ...]}
                dynamic obj = JsonConvert.DeserializeObject(resp);
                if (obj == null)
                    return new LineasPrepagoResponse { Resultado = false, Mensaje = "Respuesta inválida del proveedor" };

                string status = (string)obj.status;
                if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
                {
                    string mensaje = obj.mensaje != null ? (string)obj.mensaje : "Error consultando líneas prepago";
                    return new LineasPrepagoResponse { Resultado = false, Mensaje = mensaje };
                }

                var lista = new List<LineaPrepagoVM>();
                if (obj.lineas != null)
                {
                    foreach (var it in obj.lineas)
                    {
                        // it.telefono y it.saldo provienen del proveedor
                        string tel = (string)it.telefono;
                        decimal saldo = 0m;
                        try
                        {
                            // it.saldo puede venir como number o string; se cubren ambos casos
                            saldo = it.saldo is string
                                ? Convert.ToDecimal((string)it.saldo)
                                : Convert.ToDecimal((double)it.saldo);
                        }
                        catch { /* dejar 0 si falla */ }

                        lista.Add(new LineaPrepagoVM
                        {
                            Telefono = tel,
                            Saldo = saldo
                        });
                    }
                }

                return new LineasPrepagoResponse
                {
                    Resultado = true,
                    Mensaje = "OK",
                    Lineas = lista
                };
            }
            catch (Exception ex)
            {
                return new LineasPrepagoResponse
                {
                    Resultado = false,
                    Mensaje = "Error: " + ex.Message,
                    Lineas = new List<LineaPrepagoVM>()
                };
            }
        }

        [WebMethod]
        public RecargaResponse RecargarSaldoPrepago(string telefono, string monto, string tarjeta = null, string titular = null, string exp = null, string cvv = null)
        {
            if (string.IsNullOrWhiteSpace(telefono) || string.IsNullOrWhiteSpace(monto))
                return new RecargaResponse { Resultado = false, Mensaje = "Datos incompletos (telefono/monto)" };

            decimal montoDec;
            if (!decimal.TryParse(monto, out montoDec) || montoDec <= 0)
                return new RecargaResponse { Resultado = false, Mensaje = "Monto inválido" };

            try
            {
                var payload = new Dictionary<string, string>
                {
                    ["tipo_transaccion"] = "9",
                    ["telefono"] = telefono.Trim(),
                    ["monto"] = montoDec.ToString("0.00")
                };

                // Estos campos son opcionales, NO se guardan (solo por cumplimiento UI)
                if (!string.IsNullOrWhiteSpace(tarjeta)) payload["tarjeta"] = tarjeta.Trim();
                if (!string.IsNullOrWhiteSpace(titular)) payload["titular"] = titular.Trim();
                if (!string.IsNullOrWhiteSpace(exp)) payload["exp"] = exp.Trim();
                if (!string.IsNullOrWhiteSpace(cvv)) payload["cvv"] = cvv.Trim();

                string json = JsonConvert.SerializeObject(payload);
                string resp = EnviarAlProveedor(json);

                // Se espera algo como:
                // {"status":"OK","mensaje":"Recarga exitosa","nuevo_saldo":"1234.56"}
                dynamic obj = JsonConvert.DeserializeObject(resp);
                if (obj == null)
                    return new RecargaResponse { Resultado = false, Mensaje = "Respuesta inválida del proveedor" };

                string status = (string)obj.status;
                if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
                {
                    string mensaje = obj.mensaje != null ? (string)obj.mensaje : "No se pudo recargar";
                    return new RecargaResponse { Resultado = false, Mensaje = mensaje };
                }

                decimal? nuevoSaldo = null;
                try
                {
                    if (obj.nuevo_saldo != null)
                    {
                        nuevoSaldo = obj.nuevo_saldo is string
                            ? Convert.ToDecimal((string)obj.nuevo_saldo)
                            : Convert.ToDecimal((double)obj.nuevo_saldo);
                    }
                }
                catch { /* ignorar error de conversión */ }

                return new RecargaResponse
                {
                    Resultado = true,
                    Mensaje = obj.mensaje != null ? (string)obj.mensaje : "Recarga exitosa",
                    NuevoSaldo = nuevoSaldo
                };
            }
            catch (Exception ex)
            {
                return new RecargaResponse { Resultado = false, Mensaje = "Error: " + ex.Message };
            }
        }

         //


        public static string EnviarAlProveedor(string trama)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 5000;
                    client.Connect("127.0.0.1", 6000);

                    using (NetworkStream stream = client.GetStream())
                    {
                        // Enviar la trama
                        byte[] data = Encoding.UTF8.GetBytes(trama + "\n");
                        stream.Write(data, 0, data.Length);

                        // Leer la respuesta completa
                        using (var ms = new MemoryStream())
                        {
                            byte[] buffer = new byte[256];
                            int bytesRead;
                            do
                            {
                                bytesRead = stream.Read(buffer, 0, buffer.Length);
                                if (bytesRead > 0)
                                {
                                    ms.Write(buffer, 0, bytesRead);
                                }
                            } while (stream.DataAvailable);

                            string respuesta = Encoding.UTF8.GetString(ms.ToArray());
                            return respuesta.Trim('\0', '\r', '\n', ' ');
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return $"{{\"status\":\"ERROR\",\"mensaje\":\"Error al conectar: {ex.Message}\"}}";
            }
        }
    }
}