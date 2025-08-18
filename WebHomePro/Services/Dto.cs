

namespace WebHomePro.Services.Dto
{
    public class UltimaFacturacionDto
    {
        public DateTime? FechaCalculo { get; set; }   // FECHA_CALCULO
        public DateTime? FechaMPago { get; set; }   // FECHA_M_PAGO
        public decimal Total { get; set; }   // COBRO_TOTAL
        public DateTime? FechaCobro { get; set; }   // FECHA_COBRO (ejecución)
    }


    /*
        public class DetalleCobroDto
        {
            public string Numero { get; set; } = "";
            public string Tipo { get; set; } = ""; // "prepago" / "postpago"
            public int Minutos { get; set; }
            public decimal Monto { get; set; }
        }
    */

    public class RespuestaFacturacionDto
    {
        public bool Exitoso { get; set; }
        public string? Mensaje { get; set; }
        public decimal Total { get; set; }
    }
}
