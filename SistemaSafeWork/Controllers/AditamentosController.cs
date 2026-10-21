using System.Linq;
using System.Web;
using System.Web.Mvc;
using SistemaSafeWork.Models;
namespace SistemaSafeWork.Controllers
{
 public class AditamentosController : BaseController
 {
  private Aditamento Find(int id) { var items=Db.Map(Db.Query("SELECT * FROM Aditamento WHERE Id=@id",Db.P("@id",id)),r=>new Aditamento {Id=(int)r["Id"],Codigo=Db.Text(r,"Codigo"),Nombre=Db.Text(r,"Nombre"),Descripcion=Db.Text(r,"Descripcion"),ZonaCuerpo=Db.Text(r,"ZonaCuerpo"),ColorRequerido=Db.Text(r,"ColorRequerido"),Fotografia=Db.Text(r,"Fotografia")}); return items.Count==0?null:items[0]; }
  [Permiso("aditamentos.ver")] public ActionResult Index(string q) { ViewBag.Q=q; return View(Db.Query("SELECT * FROM Aditamento WHERE @q='' OR Nombre LIKE @search OR Codigo LIKE @search ORDER BY Nombre",Db.P("@q",q??""),Db.P("@search","%"+(q??"")+"%"))); }
  [Permiso("aditamentos.crear")] public ActionResult Crear() { return View("Form",new Aditamento()); }
  [Permiso("aditamentos.editar")] public ActionResult Editar(int id) { var model=Find(id); return model==null ? (ActionResult)HttpNotFound() : View("Form",model); }
  [Permiso("aditamentos.crear"),HttpPost,ValidateAntiForgeryToken] public ActionResult Crear([Bind(Include="Codigo,Nombre,Descripcion,ZonaCuerpo,ColorRequerido")] Aditamento model,HttpPostedFileBase foto) { return Persist(model,foto,null); }
  [Permiso("aditamentos.editar"),HttpPost,ValidateAntiForgeryToken] public ActionResult Editar(int id,[Bind(Include="Codigo,Nombre,Descripcion,ZonaCuerpo,ColorRequerido")] Aditamento model,HttpPostedFileBase foto) { var old=Find(id); if(old==null)return HttpNotFound(); model.Id=id; return Persist(model,foto,old); }
  private ActionResult Persist(Aditamento model,HttpPostedFileBase foto,Aditamento old)
  {
   if(!ZonasCuerpo.Todas.Contains(model.ZonaCuerpo))ModelState.AddModelError("ZonaCuerpo","Selecciona una zona válida.");
   model.ColorRequerido=string.IsNullOrWhiteSpace(model.ColorRequerido)?null:model.ColorRequerido.Trim();
   if(model.ColorRequerido!=null&&!ColoresAditamento.Valores.Contains(model.ColorRequerido))ModelState.AddModelError("ColorRequerido","Selecciona un color válido.");
   var bytes=ReadPhoto(foto); model.Fotografia=old?.Fotografia;
   if(ModelState.IsValid)
   {
    string previous=model.Fotografia; model.Fotografia=StorePhoto(bytes,previous);
    bool saved=Save(()=>Db.Execute(old==null ? "INSERT Aditamento(Codigo,Nombre,Descripcion,ZonaCuerpo,ColorRequerido,Fotografia) VALUES(@codigo,@nombre,@descripcion,@zona,@color,@foto)" : "UPDATE Aditamento SET Codigo=@codigo,Nombre=@nombre,Descripcion=@descripcion,ZonaCuerpo=@zona,ColorRequerido=@color,Fotografia=@foto WHERE Id=@id",Db.P("@codigo",model.Codigo.Trim()),Db.P("@nombre",model.Nombre.Trim()),Db.P("@descripcion",model.Descripcion),Db.P("@zona",model.ZonaCuerpo),Db.P("@color",model.ColorRequerido),Db.P("@foto",model.Fotografia),Db.P("@id",model.Id)));
    if(saved) { if(bytes!=null)DeletePhoto(previous); return RedirectToAction("Index"); } if(bytes!=null)DeletePhoto(model.Fotografia); model.Fotografia=previous;
   }
   return View("Form",model);
  }
  [Permiso("aditamentos.eliminar")] public ActionResult Eliminar(int id) { var model=Find(id); if(model==null)return HttpNotFound(); ViewBag.Nombre=model.Nombre; return View("~/Views/Shared/Delete.cshtml"); }
  [Permiso("aditamentos.eliminar"),HttpPost,ValidateAntiForgeryToken,ActionName("Eliminar")] public ActionResult ConfirmarEliminar(int id) { var model=Find(id); if(model==null)return HttpNotFound(); if(Save(()=>Db.Execute("DELETE Aditamento WHERE Id=@id",Db.P("@id",id)))) { DeletePhoto(model.Fotografia); return RedirectToAction("Index"); } ViewBag.Nombre=model.Nombre; return View("~/Views/Shared/Delete.cshtml"); }
 }
}

