using System.Net;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;

namespace CompVis_StableDiffusion_Api
{
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();
            var isLocal = httpContext?.Connection?.RemoteIpAddress != null &&
                IPAddress.IsLoopback(IPAddress.Parse(httpContext.Connection.RemoteIpAddress.ToString()));

            // Allow all authenticated users to see the Dashboard (potentially dangerous).
            return isLocal || httpContext.User.Identity.IsAuthenticated;
        }
    }
}
