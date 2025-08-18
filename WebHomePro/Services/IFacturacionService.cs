using System;
using System.Threading.Tasks;
using WebHomePro.Services.Dto;

namespace WebHomePro.Services.IFaturacionService
{
    public interface IFacturacionService
    {
        Task<UltimaFacturacionDto?> GetUltimaFacturacionAsync();
        Task<RespuestaFacturacionDto> EjecutarCalculoFacturacionAsync(DateTime inicio, DateTime fin);
    }
}
