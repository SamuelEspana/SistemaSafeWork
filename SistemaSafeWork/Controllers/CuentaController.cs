using System;
using System.Web.Mvc;
using System.Web.Security;
using SistemaSafeWork.Models;
namespace SistemaSafeWork.Controllers
{
    public class CuentaController : Controller
    {
        [AllowAnonymous] public ActionResult Login(string returnUrl)
        {
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            if (User.Identity.IsAuthenticated)
            {
                if (Access.Current(HttpContext) != null) return RedirectToAction("Index", "Home");
                FormsAuthentication.SignOut();
                // La cookie se elimina en la respuesta. El nuevo GET debe generar el token
                // con identidad anónima, sin reutilizar la identidad de esta solicitud.
                return RedirectToAction("Login", new { returnUrl });
            }
            ViewBag.ReturnUrl = returnUrl; return View(new LoginModel());
        }
        protected override void OnException(ExceptionContext context)
        {
            if (context.Exception is System.Web.Mvc.HttpAntiForgeryException &&
                string.Equals(Convert.ToString(context.RouteData.Values["action"]), "Login", StringComparison.OrdinalIgnoreCase))
            {
                TempData["LoginError"] = "El formulario de acceso venció o la sesión cambió. Ingresa nuevamente tus credenciales.";
                context.ExceptionHandled = true;
                context.Result = RedirectToAction("Login");
                return;
            }
            base.OnException(context);
        }
        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken] public ActionResult Login(LoginModel model, string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            if (!ModelState.IsValid) return View(model);
            var rows = Db.Query("SELECT Id,ContrasenaHash,Activo,BloqueadoHasta FROM Usuario WHERE NombreUsuario=@usuario", Db.P("@usuario", model.Usuario.Trim()));
            if (rows.Rows.Count > 0)
            {
                var row = rows.Rows[0]; bool locked = row["BloqueadoHasta"] != DBNull.Value && (DateTime)row["BloqueadoHasta"] > DateTime.UtcNow;
                if (!locked && (bool)row["Activo"] && Passwords.Verify(model.Contrasena, Db.Text(row, "ContrasenaHash")))
                {
                    Db.Execute("UPDATE Usuario SET Intentos=0,BloqueadoHasta=NULL WHERE Id=@id", Db.P("@id", row["Id"]));
                    Session.Clear(); Session.Abandon(); FormsAuthentication.SetAuthCookie(row["Id"].ToString(), false);
                    return Url.IsLocalUrl(returnUrl) ? (ActionResult)Redirect(returnUrl) : RedirectToAction("Index", "Home");
                }
                if (!locked) Db.Execute("UPDATE Usuario SET Intentos=CASE WHEN BloqueadoHasta<=SYSUTCDATETIME() THEN 1 ELSE Intentos+1 END, BloqueadoHasta=CASE WHEN (CASE WHEN BloqueadoHasta<=SYSUTCDATETIME() THEN 1 ELSE Intentos+1 END)>=5 THEN DATEADD(minute,15,SYSUTCDATETIME()) ELSE NULL END WHERE Id=@id", Db.P("@id", row["Id"]));
            }
            ModelState.AddModelError("", "Usuario o contraseña incorrectos. Tras 5 intentos fallidos, espera 15 minutos."); return View(model);
        }
        [HttpPost, ValidateAntiForgeryToken] public ActionResult Salir() { FormsAuthentication.SignOut(); Session.Clear(); Session.Abandon(); return RedirectToAction("Login"); }
        [Permiso("cuenta.contrasena")] public ActionResult CambiarContrasena() { return View(new CambioContrasena()); }
        [Permiso("cuenta.contrasena"), HttpPost, ValidateAntiForgeryToken] public ActionResult CambiarContrasena(CambioContrasena model)
        {
            if (ModelState.IsValid)
            {
                var row = Db.Query("SELECT ContrasenaHash FROM Usuario WHERE Id=@id", Db.P("@id", Access.UserId(HttpContext))).Rows[0];
                if (!Passwords.Verify(model.Actual, Db.Text(row, "ContrasenaHash"))) ModelState.AddModelError("Actual", "La contraseña actual es incorrecta.");
                else { Db.Execute("UPDATE Usuario SET ContrasenaHash=@hash WHERE Id=@id", Db.P("@hash", Passwords.Hash(model.Nueva)), Db.P("@id", Access.UserId(HttpContext))); TempData["Success"] = "Contraseña actualizada."; return RedirectToAction("Index", "Home"); }
            }
            return View(model);
        }
    }
}

