using System.IO;
using System.Web.Mvc;
using SistemaSafeWork.Models;
namespace SistemaSafeWork.Controllers
{
    public class FotosController : Controller
    {
        public ActionResult Ver(string tipo, int id)
        {
            string table, permission;
            if (tipo == "persona") { table = "Persona"; permission = "personas.ver"; }
            else if (tipo == "aditamento") { table = "Aditamento"; permission = "aditamentos.ver"; }
            else return HttpNotFound();
            if (!Access.Has(HttpContext, permission)) return new HttpStatusCodeResult(403);
            var rows = Db.Query("SELECT Fotografia FROM " + table + " WHERE Id=@id", Db.P("@id", id));
            if (rows.Rows.Count == 0) return HttpNotFound(); var name = Db.Text(rows.Rows[0], "Fotografia");
            if (string.IsNullOrEmpty(name) || name != Path.GetFileName(name)) return HttpNotFound();
            var path = Server.MapPath("~/App_Data/Fotos/" + name); if (!System.IO.File.Exists(path)) return HttpNotFound();
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache); return File(path, "image/jpeg");
        }
    }
}
