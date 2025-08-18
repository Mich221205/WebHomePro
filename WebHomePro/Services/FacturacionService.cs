using Microsoft.Extensions.Configuration;
using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using WebHomePro.Services.Dto;
using WebHomePro.Services.IFaturacionService;

namespace WebHomePro.Services
{
    public class FacturacionService : IFacturacionService
    {
        private readonly string _connStr;

        public FacturacionService(IConfiguration cfg)
        {
            // Ajusta el nombre de la cadena si usas otro
            _connStr = cfg.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection");
        }

        public async Task<UltimaFacturacionDto?> GetUltimaFacturacionAsync()
        {
            const string sql = @"
                SELECT TOP 1
                    FECHA_CALCULO  AS FechaCalculo,
                    FECHA_M_PAGO   AS FechaMPago,
                    COBRO_TOTAL    AS Total,
                    FECHA_COBRO    AS FechaCobro
                FROM dbo.COBROS
                ORDER BY FECHA_COBRO DESC;";

            await using var cn = new SqlConnection(_connStr);
            await cn.OpenAsync();

            await using var cmd = new SqlCommand(sql, cn);
            await using var rd = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);

            if (!await rd.ReadAsync()) return null;

            return new UltimaFacturacionDto
            {
                FechaCalculo = rd.GetDateTime(rd.GetOrdinal("FechaCalculo")),
                FechaMPago = rd.GetDateTime(rd.GetOrdinal("FechaMPago")),
                Total = rd.GetDecimal(rd.GetOrdinal("Total")),
                FechaCobro = rd.GetDateTime(rd.GetOrdinal("FechaCobro"))
            };
        }

        public async Task<RespuestaFacturacionDto> EjecutarCalculoFacturacionAsync(DateTime inicio, DateTime fin)
        {
            await using var cn = new SqlConnection(_connStr);
            await cn.OpenAsync();

            try
            {
                // 1) Ejecuta el SP que inserta COBROS y DETALLE_COBROS
                await using (var cmd = new SqlCommand("dbo.SP_COBROS_POSTPAGOS", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add(new SqlParameter("@FECHA_CALCULO", SqlDbType.Date) { Value = inicio.Date });
                    cmd.Parameters.Add(new SqlParameter("@FECHA_M_PAGO", SqlDbType.Date) { Value = fin.Date });
                    await cmd.ExecuteNonQueryAsync();
                }

                // 2) Recupera el total del cobro recién generado para ese rango
                const string sqlTotal = @"
                    SELECT TOP 1 COBRO_TOTAL
                    FROM dbo.COBROS
                    WHERE FECHA_CALCULO = @ini AND FECHA_M_PAGO = @fin
                    ORDER BY ID_COBRO DESC;";

                await using var cmdLast = new SqlCommand(sqlTotal, cn);
                cmdLast.Parameters.Add(new SqlParameter("@ini", SqlDbType.Date) { Value = inicio.Date });
                cmdLast.Parameters.Add(new SqlParameter("@fin", SqlDbType.Date) { Value = fin.Date });

                var obj = await cmdLast.ExecuteScalarAsync();
                var total = (obj == null || obj == DBNull.Value) ? 0m : Convert.ToDecimal(obj);

                return new RespuestaFacturacionDto
                {
                    Exitoso = true,
                    Mensaje = "¡Proceso finalizado de forma exitosa!",
                    Total = total
                };
            }
            catch (Exception ex)
            {
                return new RespuestaFacturacionDto
                {
                    Exitoso = false,
                    Mensaje = $"Error al realizar el proceso: {ex.Message}",
                    Total = 0m
                };
            }
        }
    }
}
