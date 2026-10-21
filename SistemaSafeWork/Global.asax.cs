using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace SistemaSafeWork
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            System.Web.Helpers.AntiForgeryConfig.CookieName = ".SafeWork.AntiForgery";
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        protected void Application_PostAuthenticateRequest()
        {
            // Solo se admiten tickets Forms firmados de SafeWork. Nunca identidad Windows.
            var identity = Context.User == null ? null : Context.User.Identity as System.Web.Security.FormsIdentity;
            int id;
            if (identity == null || !identity.IsAuthenticated || !int.TryParse(identity.Name, out id) || id <= 0)
            {
                if (identity != null) System.Web.Security.FormsAuthentication.SignOut();
                var anonymous = new System.Security.Principal.GenericPrincipal(new System.Security.Principal.GenericIdentity(string.Empty), new string[0]);
                Context.User = anonymous;
                System.Threading.Thread.CurrentPrincipal = anonymous;
            }
        }
    }
}
