using System;
using MenuConsola.WSProveedorRef;

namespace MenuConsola
{
    public class Program
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
                        Console.Write("Identificador Teléfono (16 dígitos): ");
                        string idTelefono = Console.ReadLine();
                        Console.Write("Identificador Tarjeta (19 dígitos): ");
                        string idTarjeta = Console.ReadLine();
                        Console.Write("Tipo (prepago/postpago): ");
                        string tipo = Console.ReadLine();
                        Console.Write("Estado (disponible): ");
                        string estado = Console.ReadLine();

                        var resp1 = ws.IngresarNuevoServicio(numeroTelefono, idTelefono, idTarjeta, tipo, estado);
                        Console.WriteLine("Resultado: " + resp1.Resultado);
                        Console.WriteLine("Mensaje: " + resp1.Mensaje);
                        break;

                    case "2":
                        Console.WriteLine("\n--- Activar/Desactivar Línea ---");
                        Console.Write("Número de Teléfono: ");
                        string numeroTel2 = Console.ReadLine();
                        Console.Write("Identificador Teléfono (16 dígitos): ");
                        string idTel2 = Console.ReadLine();
                        Console.Write("Identificador Tarjeta (19 dígitos): ");
                        string idTar2 = Console.ReadLine();
                        Console.Write("Tipo (prepago/postpago): ");
                        string tipo2 = Console.ReadLine();
                        Console.Write("Identificación Dueño: ");
                        string duenio = Console.ReadLine();
                        Console.Write("Estado (activar/desactivar): ");
                        string estado2 = Console.ReadLine();

                        var resp2 = ws.ActivarDesactivarLinea(numeroTel2, idTel2, idTar2, tipo2, duenio, estado2);
                        Console.WriteLine("Resultado: " + resp2.Resultado);
                        Console.WriteLine("Mensaje: " + resp2.Mensaje);
                        break;

                    case "3":
                        Console.WriteLine("\n--- Calcular Cobro Postpago ---");
                        Console.Write("Fecha de Cálculo (YYYYMMDD): ");
                        string fechaCalculo = Console.ReadLine();
                        Console.Write("Fecha Máxima de Pago (YYYYMMDD): ");
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
}
