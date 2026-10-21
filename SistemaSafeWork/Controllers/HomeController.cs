using System.Web.Mvc;
using SistemaSafeWork.Models;
namespace SistemaSafeWork.Controllers
{
 public class HomeController : Controller
 {
  public ActionResult Index()
  {
   if (Access.Current(HttpContext) == null) return RedirectToAction("Login", "Cuenta");
   if (!Access.Has(HttpContext, "dashboard.ver")) return View("Welcome");
   ViewBag.Personas = Access.Has(HttpContext,"personas.ver") ? (int?)Db.Query("SELECT COUNT(*) Total FROM Persona").Rows[0]["Total"] : null;
   ViewBag.Categorias = Access.Has(HttpContext,"categorias.ver") ? (int?)Db.Query("SELECT COUNT(*) Total FROM Categoria_Personal").Rows[0]["Total"] : null;
   ViewBag.Aditamentos = Access.Has(HttpContext,"aditamentos.ver") ? (int?)Db.Query("SELECT COUNT(*) Total FROM Aditamento").Rows[0]["Total"] : null;
   return View();
  }
 }
}
