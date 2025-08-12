using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
                //Traer datos sin decrypt
                var datos = db.TELEFONOS
                    .Where(t => t.ID_ESTADO == 3)
                    .Select(t => new
                    {
                        t.NUM_TELEFONO,
                        t.IDENTIFICADOR_TARJETA,
                        Tipo = t.TIPO_TELEFONO1.DESCRIPCION
                    })
                    .ToList(); // Aquí EF ejecuta el SQL y trae los datos en memoria

                // Desencriptar en memoria
                var lineas = datos.Select(t => new LineaDisponible
                {
                    Numero = AESDecryptor.Decrypt(t.NUM_TELEFONO),
                    IdTarjeta = AESDecryptor.Decrypt(t.IDENTIFICADOR_TARJETA),
                    Tipo = t.Tipo
                }).ToList();

                return lineas;
            }
        }



        [WebMethod]
        public RespuestaActivarDesactivar ActivarDesactivarLinea(string numeroTelefono, string idTelefono, string idTarjeta, string tipo, string identificacionDuenio, string estado)
        {
            if (string.IsNullOrWhiteSpace(numeroTelefono) || string.IsNullOrWhiteSpace(idTelefono)
                || string.IsNullOrWhiteSpace(idTarjeta) || string.IsNullOrWhiteSpace(tipo)
                || string.IsNullOrWhiteSpace(identificacionDuenio) || string.IsNullOrWhiteSpace(estado))
            {
                return new RespuestaActivarDesactivar { Resultado = false, Mensaje = "Datos incompletos" };
            }

            try
            {
                var tramaJson = new
                {
                    tipo_transaccion = "6",
                    telefono = numeroTelefono,
                    identificadorTel = idTelefono,
                    identificador_tarjeta = idTarjeta,
                    tipo = tipo.ToLower(),
                    duenio = identificacionDuenio,
                    estado = estado.ToLower()
                };

                string respuesta = EnviarAlProveedor(JsonConvert.SerializeObject(tramaJson));

                if (respuesta.Contains("\"status\":\"OK\""))
                {
                    return new RespuestaActivarDesactivar { Resultado = true, Mensaje = "Exitoso" };
                }
                else
                {
                    dynamic respObj = JsonConvert.DeserializeObject(respuesta);
                    return new RespuestaActivarDesactivar
                    {
                        Resultado = false,
                        Mensaje = respObj.mensaje != null ? (string)respObj.mensaje : "Problemas al activar/desactivar línea"
                    };
                }
            }
            catch (Exception ex)
            {
                return new RespuestaActivarDesactivar { Resultado = false, Mensaje = $"Error: {ex.Message}" };
            }
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