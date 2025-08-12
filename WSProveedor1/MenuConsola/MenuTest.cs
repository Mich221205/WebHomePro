
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WSProveedor1;

namespace MenuConsola
{
    internal class Program
    {
        static void Main(string[] args)
        {
            WSProveedor ws = new WSProveedor();
            bool salir = false;

            while (!salir)
            {
                Console.Clear();
                Console.WriteLine("=== MENU WS PROVEEDOR ===");
                Console.WriteLine("1. Ingresar Nuevo Servicio (WS_PROVEEDOR1)");
                Console.WriteLine("2. Activar/Desactivar Línea (WS_PROVEEDOR2)");
                Console.WriteLine("3. Calcular Cobro Postpago (WS_PROVEEDOR3)");
                Console.WriteLine("4. Salir");
                Console.Write("Seleccione una opción: ");
                string opcion = Console.ReadLine();

                switch (opcion)
                {
                    case "1":
                        Console.WriteLine("\n--- Ingresar Nuevo Servicio ---");
                        Console.Write("Número de Teléfono: ");
                        string numeroTelefono = Console.ReadLine();

                        string idTelefono;
                        do
                        {
                            Console.Write("Identificador Teléfono (16 dígitos): ");
                            idTelefono = Console.ReadLine();
                            if (idTelefono.Length != 16 || !idTelefono.All(char.IsDigit))
                                Console.WriteLine("⚠ Debe contener exactamente 16 dígitos numéricos.");
                        } while (idTelefono.Length != 16 || !idTelefono.All(char.IsDigit));

                        string idTarjeta;
                        do
                        {
                            Console.Write("Identificador Tarjeta (19 dígitos): ");
                            idTarjeta = Console.ReadLine();
                            if (idTarjeta.Length != 19 || !idTarjeta.All(char.IsDigit))
                                Console.WriteLine("⚠ Debe contener exactamente 19 dígitos numéricos.");
                        } while (idTarjeta.Length != 19 || !idTarjeta.All(char.IsDigit));

                        Console.Write("Tipo (prepago/postpago): ");
                        string tipo = Console.ReadLine();
                        Console.Write("Estado (disponible): ");
                        string estado = Console.ReadLine();

                        string numeroEnc = AESEncryptor.Encrypt(numeroTelefono);
                        string idTelEnc = AESEncryptor.Encrypt(idTelefono);
                        string idTarEnc = AESEncryptor.Encrypt(idTarjeta);

                        var resp1 = ws.IngresarNuevoServicio(numeroEnc, idTelEnc, idTarEnc, tipo, estado);
                        Console.WriteLine("Resultado: " + resp1.Resultado);
                        Console.WriteLine("Mensaje: " + resp1.Mensaje);
                        break;

                    case "2":
                        Console.WriteLine("\n--- Activar/Desactivar Línea ---");
                        Console.Write("Número de Teléfono: ");
                        string numeroTel2 = Console.ReadLine();

                        string idTel2;
                        do
                        {
                            Console.Write("Identificador Teléfono (16 dígitos): ");
                            idTel2 = Console.ReadLine();
                            if (idTel2.Length != 16 || !idTel2.All(char.IsDigit))
                                Console.WriteLine("⚠ Debe contener exactamente 16 dígitos numéricos.");
                        } while (idTel2.Length != 16 || !idTel2.All(char.IsDigit));

                        string idTar2;
                        do
                        {
                            Console.Write("Identificador Tarjeta (19 dígitos): ");
                            idTar2 = Console.ReadLine();
                            if (idTar2.Length != 19 || !idTar2.All(char.IsDigit))
                                Console.WriteLine("⚠ Debe contener exactamente 19 dígitos numéricos.");
                        } while (idTar2.Length != 19 || !idTar2.All(char.IsDigit));

                        Console.Write("Tipo (prepago/postpago): ");
                        string tipo2 = Console.ReadLine();
                        Console.Write("Identificación Dueño: ");
                        string duenio = Console.ReadLine();
                        Console.Write("Estado (activar/desactivar): ");
                        string estado2 = Console.ReadLine();

                        /*
                        string numeroEn = AESEncryptor.Encrypt(numeroTel2);
                        string idTelEn = AESEncryptor.Encrypt(idTel2);
                        string idTarEn = AESEncryptor.Encrypt(idTar2);
                        */

                        var resp2 = ws.ActivarDesactivarLinea(numeroTel2, idTel2, idTar2, tipo2, duenio, estado2);
                        Console.WriteLine("Resultado: " + resp2.Resultado);
                        Console.WriteLine("Mensaje: " + resp2.Mensaje);
                        break;


                    case "3":
                        Console.WriteLine("\n--- Calcular Cobro Postpago ---");
                        Console.Write("Fecha de Cálculo (YYYY-MM-DD): ");
                        string fechaCalculo = Console.ReadLine();
                        Console.Write("Fecha Máxima de Pago (YYYY-MM-DD): ");
                        string fechaMaxPago = Console.ReadLine();

                        var resp3 = ws.CalcularCobroPostpago(fechaCalculo, fechaMaxPago);
                        Console.WriteLine("Resultado: " + resp3.Resultado);
                        Console.WriteLine("Mensaje: " + resp3.Mensaje);
                        break;


                    case "4":
                        salir = true;
                        break;

                    default:
                        Console.WriteLine("Opción inválida.");
                        break;
                }

                if (!salir)
                {
                    Console.WriteLine("\nPresione una tecla para continuar...");
                    Console.ReadKey();
                }
            }
        }
    }

    public static class AESConfig
    {
        public static string Key = "1234567890123456"; // 16 chars = 128 bits
        public static string IV = "6543210987654321";  // 16 chars
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
                {
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
    }
}
