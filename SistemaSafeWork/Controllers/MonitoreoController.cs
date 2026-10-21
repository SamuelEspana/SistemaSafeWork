using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using System.Web.SessionState;
using SistemaSafeWork.Models;
using SistemaSafeWork.Services;
namespace SistemaSafeWork.Controllers
{
 [SessionState(SessionStateBehavior.Disabled),Permiso("seguridad.monitoreo.ver")]
 public sealed class MonitoreoController:BaseController
 {
  private sealed class MonitorSession
  {
   public int User;public volatile bool Active=true;public long Sequence;public readonly SemaphoreSlim Gate=new SemaphoreSlim(1);
   public readonly CancellationTokenSource Lifetime=new CancellationTokenSource();public readonly SeguimientoMonitoreo Tracker=new SeguimientoMonitoreo();
  }
  private static string Key(Guid id){return "SafeWork.Monitor."+id.ToString("N");}
  private JsonResult Error(string message,int status=400){Response.StatusCode=status;Response.TrySkipIisCustomErrors=true;Response.SuppressFormsAuthenticationRedirect=true;return Json(new{ok=false,message});}
  public ActionResult Index()
  {
   Response.Cache.SetCacheability(HttpCacheability.NoCache);Response.Cache.SetNoStore();var settings=VisionSettings.Load();
   ViewBag.Ready=settings.Ready&&settings.IsLocal;ViewBag.CanRun=Access.Has(HttpContext,"seguridad.monitoreo.ejecutar");return View();
  }
  [HttpPost,ValidateAntiForgeryToken,Permiso("seguridad.monitoreo.ejecutar")]
  public ActionResult Iniciar()
  {
   var settings=VisionSettings.Load();if(!settings.Ready||!settings.IsLocal)return Error("Monitoreo requiere el motor local de SafeWork configurado.",503);
   var id=Guid.NewGuid();var session=new MonitorSession{User=Access.UserId(HttpContext)};
   HttpRuntime.Cache.Insert(Key(id),session,null,Cache.NoAbsoluteExpiration,TimeSpan.FromMinutes(20),CacheItemPriority.Default,(k,v,reason)=>{var s=(MonitorSession)v;s.Active=false;s.Lifetime.Cancel();});
   return Json(new{ok=true,sesion=id.ToString("N")});
  }
  [HttpPost,ValidateAntiForgeryToken]
  public ActionResult Detener(string sesion)
  {
   Guid id;if(!Guid.TryParse(sesion,out id))return Error("Sesión inválida.");var session=HttpRuntime.Cache.Get(Key(id)) as MonitorSession;
   if(session==null)return Json(new{ok=true});if(session.User!=Access.UserId(HttpContext))return Error("Esta sesión pertenece a otro usuario.",403);
   session.Active=false;session.Lifetime.Cancel();HttpRuntime.Cache.Remove(Key(id));return Json(new{ok=true});
  }
  [HttpPost,ValidateAntiForgeryToken,Permiso("seguridad.monitoreo.ejecutar")]
  public async Task<ActionResult> Analizar(string sesion,long secuencia,string captura,bool reiniciar=false)
  {
   Guid id;if(!Guid.TryParse(sesion,out id))return Error("Sesión inválida.");var session=HttpRuntime.Cache.Get(Key(id)) as MonitorSession;
   if(session==null||!session.Active)return Error("El monitoreo venció o se detuvo. Inícialo nuevamente.",409);
   if(session.User!=Access.UserId(HttpContext))return Error("Esta sesión pertenece a otro usuario.",403);
   if(!await session.Gate.WaitAsync(0))return Error("Ya hay una captura en análisis.",429);
   try
   {
    if(secuencia<=session.Sequence||secuencia<1)return Error("Esta captura ya se procesó o está fuera de orden.",409);
    var image=ReadPhoto(null,captura);if(image==null||!ModelState.IsValid){session.Tracker.Reset();return Error("Se necesita una captura JPG válida de hasta 5 MB.");}
    var when=DateTime.UtcNow;var bytes=Resize(image);if(reiniciar)session.Tracker.Reset();
    var people=await new DetectorMonitoreo().Analyze(bytes,VisionSettings.Load(),session.Lifetime.Token);
    if(!session.Active)return Error("El monitoreo se detuvo.",409);
    session.Sequence=secuencia;var events=session.Tracker.Update(people,when);var saved=new System.Collections.Generic.List<object>();
    foreach(var alert in events)
    {
     if(!session.Active)break;var captureId=SaveEvent(alert,id,session.User,bytes);session.Tracker.Confirmar(alert.Id);
     saved.Add(new{id=captureId,fechaUtc=alert.FechaUtc.ToString("o"),observacion=alert.Observacion});
    }
    return Json(new{ok=true,personas=people,eventos=saved,fechaUtc=when.ToString("o"),secuencia});
   }
   catch(VisionException ex){session.Tracker.Reset();return Error(ex.Message,502);}
   catch(UnauthorizedAccessException){session.Tracker.Reset();return Error("No hay permiso para guardar capturas en App_Data/Monitoreo.",503);}
   catch(IOException){session.Tracker.Reset();return Error("No se pudo guardar la captura. Revisa el acceso a App_Data/Monitoreo.",503);}
   catch(System.Data.SqlClient.SqlException){return Error("No se pudo registrar el evento. Revisa la conexión a la base de datos.",503);}
   finally{session.Gate.Release();}
  }
  private int SaveEvent(EventoMonitoreo alert,Guid session,int user,byte[] image)
  {
   var directory=Server.MapPath("~/App_Data/Monitoreo");Directory.CreateDirectory(directory);var name=alert.Id.ToString("N")+".jpg";
   // Nombre estable por evento: un reintento no crea otra captura ni otro registro.
   var path=Path.Combine(directory,name);if(!System.IO.File.Exists(path)){var temporary=path+".tmp";System.IO.File.WriteAllBytes(temporary,image);System.IO.File.Move(temporary,path);}
   var rows=Db.Query("IF NOT EXISTS(SELECT 1 FROM Monitoreo_Detalle WHERE EventoId=@evento) INSERT Monitoreo_Detalle(EventoId,SesionId,PistaId,UsuarioId,FechaUtc,Fotografia,Observacion) VALUES(@evento,@sesion,@pista,@usuario,@fecha,@foto,@nota); SELECT Id FROM Monitoreo_Detalle WHERE EventoId=@evento",Db.P("@evento",alert.Id),Db.P("@sesion",session),Db.P("@pista",alert.Pista),Db.P("@usuario",user),Db.P("@fecha",alert.FechaUtc),Db.P("@foto",name),Db.P("@nota",alert.Observacion));return (int)rows.Rows[0]["Id"];
  }
  private static byte[] Resize(byte[] input)
  {
   using(var stream=new MemoryStream(input))using(var source=Image.FromStream(stream))
   {
    var scale=Math.Min(1,640.0/Math.Max(source.Width,source.Height));
    using(var bitmap=new Bitmap(Math.Max(1,(int)(source.Width*scale)),Math.Max(1,(int)(source.Height*scale))))using(var g=Graphics.FromImage(bitmap))using(var output=new MemoryStream())
    {g.DrawImage(source,0,0,bitmap.Width,bitmap.Height);using(var quality=new EncoderParameters(1)){quality.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,90L);bitmap.Save(output,Array.Find(ImageCodecInfo.GetImageEncoders(),c=>c.MimeType=="image/jpeg"),quality);}return output.ToArray();}
   }
  }
 }
}
