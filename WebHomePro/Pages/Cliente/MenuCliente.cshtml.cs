using Microsoft.AspNetCore.Mvc.RazorPages;
using WebHomePro.Services;
using WebHomePro.Services.IProveedorService;

public class MenuClienteModel : PageModel
{
    private readonly IProveedorService _prov;

    public MenuClienteModel(IProveedorService proveedorService)
    {
        _prov = proveedorService ?? throw new ArgumentNullException(nameof(proveedorService));
    }

    // Propiedades que usa la vista
    public List<LineaPrepagoVm> LineasPrepago { get; set; } = new();
    public List<LineaPostpagoVm> LineasPostpago { get; set; } = new();
    public string Error { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        try
        {
            var cedula = HttpContext.Session.GetString("Cedula");
            if (string.IsNullOrEmpty(cedula))
            {
                Error = "Sesión inválida, por favor inicie sesión nuevamente.";
                return;
            }

            LineasPrepago = await _prov.GetLineasPrepagoAsync(cedula);
            LineasPostpago = await _prov.GetLineasPostpagoAsync(cedula);
        }
        catch (System.Exception ex)
        {
            Error = "No fue posible cargar tus líneas. " + ex.Message;
        }
    }


}
