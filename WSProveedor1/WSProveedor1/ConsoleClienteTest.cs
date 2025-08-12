using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WSProveedor1
{
    public class ConsoleClienteTest
    {
        static void Main(string[] args)
        {
            // Crear instancia del Web Service
            WSProveedor ws = new WSProveedor();

            // Ingresar datos de prueba (ya encriptados o listos para encriptar)
            string numeroTelefono = "84561234";
            string idTelefono = "1234567890123456";
            string idTarjeta = "1234567890123456789";
            string tipo = "prepago";
            string estado = "disponible";

            Console.WriteLine("Enviando solicitud al Web Service...");

            var respuesta = ws.IngresarNuevoServicio(numeroTelefono, idTelefono, idTarjeta, tipo, estado);

            Console.WriteLine("Resultado: " + respuesta.Resultado);
            Console.WriteLine("Mensaje: " + respuesta.Mensaje);

            Console.WriteLine("Presione una tecla para salir...");
            Console.ReadKey();
        }
    }

}