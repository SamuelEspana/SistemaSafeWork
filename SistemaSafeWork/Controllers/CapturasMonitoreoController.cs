using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using SistemaSafeWork.Models;
namespace SistemaSafeWork.Controllers
{
 [Permiso("seguridad.monitoreo.capturas.ver")]
 public sealed class CapturasMonitoreoController:Controller
 {
  public ActionResult Index(int pagina=1)
  {
   pagina=Math.Max(1,Math.Min(100000,pagina));Response.Cache.SetCacheability(HttpCacheability.NoCache);Response.Cache.SetNoStore();
   var items=Db.Map(Db.Query("SELECT Id,FechaUtc,Observacion FROM Monitoreo_Detalle ORDER BY FechaUtc DESC,Id DESC OFFSET @skip ROWS FETCH NEXT 51 ROWS ONLY",Db.P("@skip",(pagina-1)*50)),r=>new CapturaMonitoreo{Id=(int)r["Id"],FechaUtc=(DateTime)r["FechaUtc"],Observacion=Db.Text(r,"Observacion")});
   return View(new ListaMonitoreo{Capturas=items.Take(50).ToList(),Pagina=pagina,HayMas=items.Count>50});
  }
  public ActionResult Foto(int id)
  {
   var rows=Db.Query("SELECT Fotografia FROM Monitoreo_Detalle WHERE Id=@id",Db.P("@id",id));if(rows.Rows.Count==0)return HttpNotFound();
   var name=Db.Text(rows.Rows[0],"Fotografia");if(name==null||!Regex.IsMatch(name,"^[a-f0-9]{32}\\.jpg$"))return HttpNotFound();
   var path=Server.MapPath("~/App_Data/Monitoreo/"+name);if(!System.IO.File.Exists(path))return HttpNotFound();
   Response.Cache.SetCacheability(HttpCacheability.NoCache);Response.Cache.SetNoStore();return File(path,"image/jpeg");
  }
 }
}
