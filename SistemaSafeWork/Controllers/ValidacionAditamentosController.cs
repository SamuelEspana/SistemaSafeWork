using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using SistemaSafeWork.Models;
using SistemaSafeWork.Services;
namespace SistemaSafeWork.Controllers
{
 [Permiso("seguridad.validacion.ver")]
 public class ValidacionAditamentosController:BaseController
 {
  public ActionResult Index()
  {
   Response.Cache.SetCacheability(HttpCacheability.NoCache);Response.Cache.SetNoStore();
   var vision=VisionSettings.Load();ViewBag.VisionReady=vision.Ready;ViewBag.LocalVision=vision.IsLocal;ViewBag.CanAnalyze=Access.Has(HttpContext,"seguridad.validacion.analizar");return View();
  }
  private PersonaValidacion Find(string dpi)
  {
   var rows=Db.Query("SELECT p.Id,p.Nombre,c.Nombre Categoria,p.CategoriaPersonalId FROM Persona p JOIN Categoria_Personal c ON c.Id=p.CategoriaPersonalId WHERE p.DPI=@dpi",Db.P("@dpi",dpi));
   if(rows.Rows.Count==0)return null;var row=rows.Rows[0];
   return new PersonaValidacion{Id=(int)row["Id"],Nombre=Db.Text(row,"Nombre"),Categoria=Db.Text(row,"Categoria"),Aditamentos=Db.Map(Db.Query("SELECT a.* FROM CategoriaAditamento ca JOIN Aditamento a ON a.Id=ca.AditamentoId WHERE ca.CategoriaPersonalId=@id ORDER BY a.ZonaCuerpo,a.Nombre",Db.P("@id",row["CategoriaPersonalId"])),r=>new Aditamento{Id=(int)r["Id"],Nombre=Db.Text(r,"Nombre"),Codigo=Db.Text(r,"Codigo"),Descripcion=Db.Text(r,"Descripcion"),ZonaCuerpo=Db.Text(r,"ZonaCuerpo"),ColorRequerido=Db.Text(r,"ColorRequerido"),Fotografia=Db.Text(r,"Fotografia")})};
  }
  private JsonResult Error(string message,int status=400)
  {Response.StatusCode=status;Response.TrySkipIisCustomErrors=true;Response.SuppressFormsAuthenticationRedirect=true;return Json(new{ok=false,message});}
  [HttpPost,ValidateAntiForgeryToken]
  public ActionResult Buscar(string dpi)
  {
   if(dpi==null||!Regex.IsMatch(dpi,"^[0-9]{13}$"))return Error("Ingresa un DPI de exactamente 13 dígitos.");
   var person=Find(dpi);if(person==null)return Error("No se encontró una persona registrada con ese DPI.",404);
   return Json(new{ok=true,persona=new{person.Nombre,person.Categoria},aditamentos=person.Aditamentos.Select(a=>new{a.Id,a.Nombre,a.Codigo,a.Descripcion,a.ZonaCuerpo,a.ColorRequerido}),puedeDetectar=person.Aditamentos.Count>0&&person.Aditamentos.Count<=40&&person.Aditamentos.All(a=>a.ZonaCuerpo!="Sin definir")});
  }
  [Permiso("seguridad.validacion.analizar"),HttpPost,ValidateAntiForgeryToken]
  public async Task<ActionResult> Analizar(string dpi,string captura)
  {
   if(dpi==null||!Regex.IsMatch(dpi,"^[0-9]{13}$"))return Error("Ingresa un DPI de exactamente 13 dígitos.");
   var person=Find(dpi);if(person==null)return Error("No se encontró una persona registrada con ese DPI.",404);
   if(person.Aditamentos.Count==0)return Error("La categoría no tiene aditamentos requeridos.");
   if(person.Aditamentos.Count>40)return Error("La categoría tiene más de 40 aditamentos. Divide las categorías para realizar esta validación.");
   if(person.Aditamentos.Any(a=>!ZonasCuerpo.Todas.Contains(a.ZonaCuerpo)||a.ZonaCuerpo=="Sin definir"))return Error("Configura la zona del cuerpo de todos los aditamentos requeridos antes de detectar.");
   var settings=VisionSettings.Load();if(!settings.Ready)return Error("El servicio de visión no está configurado. Solicita al administrador revisar la configuración del motor en el servidor.",503);
   var image=ReadPhoto(null,captura);if(image==null||!ModelState.IsValid)return Error("Captura una imagen JPG válida de hasta 5 MB para realizar la detección.");
   string key="SafeWork.Vision.User."+Access.UserId(HttpContext);
   if(System.Web.HttpRuntime.Cache.Add(key,true,null,DateTime.UtcNow.AddSeconds(210),Cache.NoSlidingExpiration,CacheItemPriority.Default,null)!=null)return Error("Hay una detección en curso o recién finalizada. Espera unos segundos.",429);
   try
   {
    var result=await new DetectorAditamentos().Analyze(Resize(image,1600),person.Aditamentos,settings,ReferencePhoto);
    return Json(new{ok=true,persona=new{person.Nombre,person.Categoria},resultado=result,fecha=DateTime.UtcNow.ToString("o")});
   }
   catch(VisionException ex){return Error(ex.Message,502);}
   finally{System.Web.HttpRuntime.Cache.Insert(key,true,null,DateTime.UtcNow.AddSeconds(3),Cache.NoSlidingExpiration);}
  }
  private byte[] ReferencePhoto(Aditamento item)
  {
   if(string.IsNullOrWhiteSpace(item.Fotografia)||!Regex.IsMatch(item.Fotografia,"^[a-f0-9]{32}\\.jpg$"))return null;
   string path=Server.MapPath("~/App_Data/Fotos/"+item.Fotografia);if(!System.IO.File.Exists(path))return null;
   try{return Resize(System.IO.File.ReadAllBytes(path),512);}catch(ArgumentException){return null;}catch(IOException){return null;}
  }
  private static byte[] Resize(byte[] bytes,int max)
  {
   using(var stream=new MemoryStream(bytes))using(var image=Image.FromStream(stream))
   {
    double ratio=Math.Min(1,(double)max/Math.Max(image.Width,image.Height));
    using(var bitmap=new Bitmap(Math.Max(1,(int)(image.Width*ratio)),Math.Max(1,(int)(image.Height*ratio))))
    {using(var graphics=Graphics.FromImage(bitmap)){graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;graphics.DrawImage(image,0,0,bitmap.Width,bitmap.Height);}using(var output=new MemoryStream()){bitmap.Save(output,ImageFormat.Jpeg);return output.ToArray();}}
   }
  }
 }
}
