using Microsoft.AspNetCore.Http;

namespace WebHomePro.Helpers
{
    public static class SessionHelper
    {
        public static string? GetClienteCedula(HttpContext httpContext)
        {
            return httpContext.Session.GetString("Cedula");
        }
    }
}

