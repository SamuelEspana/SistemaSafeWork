using System;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;

namespace SistemaSafeWork.Models
{
    public static class Passwords
    {
        public static string Hash(string password)
        {
            var salt = new byte[16]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(salt);
            using (var derive = new Rfc2898DeriveBytes(password, salt, 210000, HashAlgorithmName.SHA256))
                return "210000." + Convert.ToBase64String(salt) + "." + Convert.ToBase64String(derive.GetBytes(32));
        }
        public static bool Verify(string password, string hash)
        {
            try
            {
                if (string.IsNullOrEmpty(hash) || password==null) return false; var parts = hash.Split('.'); int iterations; if(parts.Length!=3 || !int.TryParse(parts[0],out iterations) || iterations<10000 || iterations>1000000)return false; var expected = Convert.FromBase64String(parts[2]); if(expected.Length!=32)return false;
                using (var derive = new Rfc2898DeriveBytes(password, Convert.FromBase64String(parts[1]), iterations, HashAlgorithmName.SHA256))
                { var actual = derive.GetBytes(expected.Length); int difference = 0; for (int i = 0; i < actual.Length; i++) difference |= actual[i] ^ expected[i]; return difference == 0; }
            }
            catch (FormatException) { return false; }
        }
    }
    public static class Access
    {
        public static readonly string[] Permisos = { "seguridad.monitoreo.ver", "seguridad.monitoreo.ejecutar", "seguridad.monitoreo.capturas.ver", "seguridad.validacion.ver", "seguridad.validacion.analizar", "dashboard.ver", "personas.ver", "personas.crear", "personas.editar", "personas.eliminar", "aditamentos.ver", "aditamentos.crear", "aditamentos.editar", "aditamentos.eliminar", "categorias.ver", "categorias.crear", "categorias.editar", "categorias.eliminar", "usuarios.ver", "usuarios.crear", "usuarios.editar", "usuarios.eliminar", "roles.ver", "roles.crear", "roles.editar", "roles.eliminar", "cuenta.contrasena" };
        public static int UserId(HttpContextBase context) { int id; var identity = context.User.Identity as FormsIdentity; return identity != null && identity.IsAuthenticated && int.TryParse(identity.Name, out id) && id > 0 ? id : 0; }
        public static Usuario Current(HttpContextBase context)
        {
            const string key = "SafeWork.CurrentUser";
            if (context.Items.Contains(key)) return context.Items[key] as Usuario;
            var rows = Db.Query("SELECT u.Id,u.Nombre,u.NombreUsuario,u.RolId,r.Nombre Rol,u.Activo FROM Usuario u JOIN Rol r ON r.Id=u.RolId WHERE u.Id=@id AND u.Activo=1", Db.P("@id", UserId(context)));
            var user = rows.Rows.Count == 0 ? null : new Usuario { Id = (int)rows.Rows[0]["Id"], Nombre = Db.Text(rows.Rows[0], "Nombre"), NombreUsuario = Db.Text(rows.Rows[0], "NombreUsuario"), RolId = (int)rows.Rows[0]["RolId"], Rol = Db.Text(rows.Rows[0], "Rol") };
            context.Items[key] = user; return user;
        }
        public static bool Has(HttpContextBase context, string permission)
        {
            var user = Current(context); if (user == null) return false;
            if (user.Rol == "Administrador") return true;
            const string key = "SafeWork.Permissions";
            if (!context.Items.Contains(key)) context.Items[key] = Db.Map(Db.Query("SELECT Permiso FROM RolPermiso WHERE RolId=@id", Db.P("@id", user.RolId)), r => Db.Text(r, "Permiso"));
            return ((System.Collections.Generic.List<string>)context.Items[key]).Contains(permission);
        }
    }
    public class PermisoAttribute : AuthorizeAttribute
    {
        private readonly string permission;
        public PermisoAttribute(string permission) { this.permission = permission; }
        protected override bool AuthorizeCore(HttpContextBase context) { return Access.Has(context, permission); }
        protected override void HandleUnauthorizedRequest(AuthorizationContext context)
        {
            if (context.HttpContext.User.Identity.IsAuthenticated) { context.Result = new ViewResult { ViewName = "~/Views/Shared/Forbidden.cshtml" }; context.HttpContext.Response.StatusCode = 403; }
            else base.HandleUnauthorizedRequest(context);
        }
    }
}


