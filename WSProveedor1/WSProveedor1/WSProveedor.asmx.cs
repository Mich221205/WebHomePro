using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
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
        public List<LineaPrepago> ListarLineasPrepagoPorCliente(string cedula)
        {
            var lista = new List<LineaPrepago>();

            int idCliente = ObtenerIdClientePorCedula(cedula);

            using (var cn = new SqlConnection("Server=Laptop-Michel;Database=COMPANIA_TELEFONICA;User Id=sa;Password=mich22;TrustServerCertificate=True;"))
            using (var cmd = new SqlCommand(@"
                SELECT t.NUM_TELEFONO AS Telefono,
                       COALESCE(t.SALDO,0) AS SaldoDisponible
                FROM TELEFONOS t
                JOIN TIPO_TELEFONO tt ON tt.ID_T_TELEFONO = t.TIPO_TELEFONO
                WHERE t.ID_CLIENTE = @IdCliente
                  AND t.ID_ESTADO = 1
                  AND UPPER(tt.DESCRIPCION) = 'PREPAGO'
                ORDER BY t.NUM_TELEFONO;", cn))
            {
                cmd.Parameters.AddWithValue("@IdCliente", idCliente);
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var tel = rd["Telefono"]?.ToString() ?? "";
                        tel = AESDecryptor.Decrypt(tel);
                        lista.Add(new LineaPrepago
                        {
                            Telefono = tel,
                            SaldoDisponible = Convert.ToDecimal(rd["SaldoDisponible"])
                        });
                    }
                }
            }

            return lista;
        }

        [WebMethod]
        public List<LineaPostpago> ObtenerLineasPostpagoPorCedula(string cedula)
        {
            var lista = new List<LineaPostpago>();

            if (string.IsNullOrWhiteSpace(cedula))
                return lista;

            // Primero obtener el ID_CLIENTE
            int idCliente = ObtenerIdClientePorCedula(cedula);
            if (idCliente == 0)
                return lista;

            using (var cn = new SqlConnection("Server=Laptop-Michel;Database=COMPANIA_TELEFONICA;User Id=sa;Password=mich22;TrustServerCertificate=True;"))
            using (var cmd = new SqlCommand(@"
        WITH postpago AS (
            SELECT t.NUM_TELEFONO AS Telefono, t.ID_CLIENTE
            FROM TELEFONOS t
            JOIN TIPO_TELEFONO tt ON tt.ID_T_TELEFONO = t.TIPO_TELEFONO
            WHERE t.ID_CLIENTE = @IdCliente
              AND t.ID_ESTADO = 1
              AND UPPER(tt.DESCRIPCION) = 'POSTPAGO'
        ),
        pendiente_cliente AS (
            SELECT dc.ID_CLIENTE,
                   COALESCE(SUM(CASE WHEN UPPER(dc.ESTADO_PAGO)='PENDIENTE'
                                     THEN COALESCE(dc.MONTO_TOTAL,0) END),0) AS MontoPendiente
            FROM DETALLE_COBROS dc
            WHERE dc.ID_CLIENTE = @IdCliente
            GROUP BY dc.ID_CLIENTE
        )
        SELECT p.Telefono,
               COALESCE(pc.MontoPendiente,0) AS MontoPendiente
        FROM postpago p
        LEFT JOIN pendiente_cliente pc ON pc.ID_CLIENTE = p.ID_CLIENTE
        ORDER BY p.Telefono;", cn))
            {
                cmd.Parameters.AddWithValue("@IdCliente", idCliente);

                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var tel = rd["Telefono"]?.ToString() ?? "";
                        tel = AESDecryptor.Decrypt(tel);

                        lista.Add(new LineaPostpago
                        {
                            Telefono = tel,
                            MontoPendiente = Convert.ToDecimal(rd["MontoPendiente"])
                        });
                    }
                }
            }

            return lista;
        }



        [WebMethod]
        public int ObtenerIdClientePorCedula(string cedula)
        {
            using(var cn = new SqlConnection("Server=Laptop-Michel;Database=COMPANIA_TELEFONICA;User Id=sa;Password=mich22;TrustServerCertificate=True;"))
            using (var cmd = new SqlCommand("SELECT ID_CLIENTE FROM CLIENTES WHERE CEDULA = @Cedula", cn))
            {
                cmd.Parameters.AddWithValue("@Cedula", cedula);

                cn.Open();
                var result = cmd.ExecuteScalar();

                return result != null ? Convert.ToInt32(result) : 0;
            }
        }

    public class LineaPrepago { public string Telefono; public decimal SaldoDisponible; }
        public class LineaPostpago { public string Telefono; public decimal MontoPendiente; }

        [WebMethod]
        public List<LineaPostpago> ListarLineasPostpagoPorCliente(int idCliente)
        {
            var lista = new List<LineaPostpago>();

            using (var cn = new SqlConnection(ConfigurationManager.ConnectionStrings["SQLServer"].ConnectionString))
            using (var cmd = new SqlCommand(@"
                WITH postpago AS (
                    SELECT t.NUM_TELEFONO AS Telefono, t.ID_CLIENTE
                    FROM TELEFONOS t
                    JOIN TIPO_TELEFONO tt ON tt.ID_T_TELEFONO = t.TIPO_TELEFONO
                    WHERE t.ID_CLIENTE = @IdCliente
                      AND t.ID_ESTADO = 1
                      AND UPPER(tt.DESCRIPCION) = 'POSTPAGO'
                ),
                pendiente_cliente AS (
                    SELECT dc.ID_CLIENTE,
                           COALESCE(SUM(CASE WHEN UPPER(dc.ESTADO_PAGO)='PENDIENTE'
                                             THEN COALESCE(dc.MONTO_TOTAL,0) END),0) AS MontoPendiente
                    FROM DETALLE_COBROS dc
                    WHERE dc.ID_CLIENTE = @IdCliente
                    GROUP BY dc.ID_CLIENTE
                )
                SELECT p.Telefono,
                       COALESCE(pc.MontoPendiente,0) AS MontoPendiente
                FROM postpago p
                LEFT JOIN pendiente_cliente pc ON pc.ID_CLIENTE = p.ID_CLIENTE
                ORDER BY p.Telefono;", cn))
            {
                // ✅ Parámetro seguro (sin inyección SQL)
                cmd.Parameters.AddWithValue("@IdCliente", idCliente);

                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var tel = rd["Telefono"]?.ToString() ?? "";
                        // 🔓 Desencriptar si está en formato ENC:
                        tel = AESDecryptor.Decrypt(tel);

                        lista.Add(new LineaPostpago
                        {
                            Telefono = tel,
                            MontoPendiente = Convert.ToDecimal(rd["MontoPendiente"])
                        });
                    }
                }
            }

            return lista;
        }

        /*
        [WebMethod]
        public List<LineaPrepago> ListarLineasPrepagoPorCliente()
        {
            var lista = new List<LineaPrepago>();

            using (var cn = new SqlConnection(ConfigurationManager.ConnectionStrings["SQLServer"].ConnectionString))
            using (var cmd = new SqlCommand(@"
        SELECT 
            t.NUM_TELEFONO       AS Telefono,
            COALESCE(t.SALDO, 0) AS SaldoDisponible
        FROM TELEFONOS t
        JOIN TIPO_TELEFONO tt ON tt.ID_T_TELEFONO = t.TIPO_TELEFONO
        WHERE t.ID_CLIENTE = 25
          AND t.ID_ESTADO = 1
          AND UPPER(tt.DESCRIPCION) = 'PREPAGO'
        ORDER BY t.NUM_TELEFONO;", cn))
            {
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var tel = rd["Telefono"]?.ToString() ?? "";
                        tel = AESDecryptor.Decrypt(tel);

                        lista.Add(new LineaPrepago
                        {
                            Telefono = tel,
                            SaldoDisponible = Convert.ToDecimal(rd["SaldoDisponible"])
                        });
                    }
                }
            }

            return lista;
        }
        */

        [WebMethod]
        public Resultado InsertarClienteBasico(string cedula, string nombre)
        {
            try
            {
                using (var ctx = new COMPANIA_TELEFONICAEntities())
                {
                    var nuevoCliente = new CLIENTES
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

                using (var db = new COMPANIA_TELEFONICAEntities())
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
            using (var db = new COMPANIA_TELEFONICAEntities())
            {
                var datos = db.TELEFONOS
                    .Where(t => t.ID_ESTADO == 3) // 'disponibles/sin vender'
                    .Select(t => new
                    {
                        t.IDENTIFICADOR_TELEFONO,           
                        t.NUM_TELEFONO,
                        t.IDENTIFICADOR_TARJETA,
                        Tipo = t.TIPO_TELEFONO1.DESCRIPCION
                    })
                    .ToList();

                var lineas = datos.Select(t => new LineaDisponible
                {
                    IdTelefono = AESDecryptor.Decrypt(t.IDENTIFICADOR_TELEFONO),                    
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


                string telefonoEnc = AESEncryptor.Encrypt(numeroTelefono.Trim());
                string idChipEnc = AESEncryptor.Encrypt(idTarjeta.Trim());
                string idTelEnc = AESEncryptor.Encrypt(idTelefono.Trim());

                var trama = new
                {
                    tipo_transaccion = "6",
                    telefono = telefonoEnc,
                    identificadorTel = idTelEnc,
                    identificador_tarjeta = idChipEnc,
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
            string cs = "Server=Laptop-Michel;Database=COMPANIA_TELEFONICA;User Id=sa;Password=mich22;";
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
                string cs = "Server=Laptop-Michel;Database=COMPANIA_TELEFONICA;User Id=sa;Password=mich22;";
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
                                NUM_TELEFONO = AESDecryptor.Decrypt(dr["NUM_TELEFONO"].ToString()),
                                IDENTIFICADOR_TARJETA = AESDecryptor.Decrypt(dr["IDENTIFICADOR_TARJETA"].ToString()),
                                IDENTIFICADOR_TELEFONO = AESDecryptor.Decrypt(dr["IDENTIFICADOR_TELEFONO"].ToString()),
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
                using (var db = new COMPANIA_TELEFONICAEntities())
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

            if (!decimal.TryParse(monto, out var montoDec) || montoDec <= 0)
                return new RecargaResponse { Resultado = false, Mensaje = "Monto inválido" };

            try
            {
                using (var cn = new SqlConnection("Server=localhost;Database=COMPANIA_TELEFONICA;User Id=sa;Password=mich22;TrustServerCertificate=true;"))
                {
                    cn.Open();

                    string telefonoCifrado = AESEncryptor.Encrypt(telefono);

                    // 1. Actualizar el saldo
                    var sqlUpdate = @"UPDATE TELEFONOS
                              SET SALDO = ISNULL(SALDO,0) + @Monto 
                              WHERE NUM_TELEFONO = @Telefono";

                    using (var cmd = new SqlCommand(sqlUpdate, cn))
                    {
                        cmd.Parameters.AddWithValue("@Monto", montoDec);
                        cmd.Parameters.AddWithValue("@Telefono", telefonoCifrado);
                        int rows = cmd.ExecuteNonQuery();

                        if (rows == 0)
                            return new RecargaResponse { Resultado = false, Mensaje = "Número no encontrado" };
                    }

                    // 2. Obtener el nuevo saldo
                    decimal nuevoSaldo = 0;
                    var sqlSelect = "SELECT SALDO FROM TELEFONOS WHERE NUM_TELEFONO = @Telefono";
                    using (var cmd2 = new SqlCommand(sqlSelect, cn))
                    {
                        cmd2.Parameters.AddWithValue("@Telefono", telefonoCifrado);
                        var result = cmd2.ExecuteScalar();
                        if (result != null) nuevoSaldo = Convert.ToDecimal(result);
                    }

                    return new RecargaResponse
                    {
                        Resultado = true,
                        Mensaje = "Recarga exitosa",
                        NuevoSaldo = nuevoSaldo
                    };
                }
            }
            catch (Exception ex)
            {
                return new RecargaResponse { Resultado = false, Mensaje = "Error en SQL: " + ex.Message };
            }
        }


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