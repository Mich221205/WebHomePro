using System;
using System.Threading.Tasks;
using WebHomePro.Services.Dto;

namespace WebHomePro.Services.IFacturacionServices
{
    /*
    public interface IFacturacionService
    {
        Task<UltimaFacturacionDto?> GetUltimaFacturacionAsync();
        Task<RespuestaFacturacionDto> EjecutarCalculoFacturacionAsync(DateTime inicio, DateTime fin);
    }*/

    public interface IFacturacionService
    {
        Task<UltimaFacturacionDto?> GetUltimaFacturacionAsync();
        Task<RespuestaFacturacionDto> EjecutarCalculoFacturacionAsync(DateTime inicio, DateTime fin);
    }
}
