using System;
using System.Web;
using System.Web.Mvc;
using SistemaSafeWork.Models;
namespace SistemaSafeWork.Controllers
{
 public class PersonasController : BaseController
 {
  private Persona Find(int id) { var items = Db.Map(Db.Query("SELECT p.*,c.Nombre Categoria FROM Persona p JOIN Categoria_Personal c ON c.Id=p.CategoriaPersonalId WHERE p.Id=@id", Db.P("@id",id)), r => new Persona { Id=(int)r["Id"], Nombre=Db.Text(r,"Nombre"), CategoriaPersonalId=(int)r["CategoriaPersonalId"], Categoria=Db.Text(r,"Categoria"), FechaNacimiento=(DateTime)r["FechaNacimiento"], DPI=Db.Text(r,"DPI"), Telefono=Db.Text(r,"Telefono"), Fotografia=Db.Text(r,"Fotografia") }); return items.Count==0 ? null : items[0]; }
  [Permiso("personas.ver")] public ActionResult Index(string q) { ViewBag.Q=q; return View(Db.Query("SELECT p.*,c.Nombre Categoria FROM Persona p JOIN Categoria_Personal c ON c.Id=p.CategoriaPersonalId WHERE @q='' OR p.Nombre LIKE @search OR p.DPI LIKE @search ORDER BY p.Nombre", Db.P("@q",q??""), Db.P("@search","%"+(q??"")+"%"))); }
  [Permiso("personas.crear")] public ActionResult Crear() { Categories(); return View("Form",new Persona()); }
  [Permiso("personas.editar")] public ActionResult Editar(int id) { var model=Find(id); if(model==null)return HttpNotFound(); Categories(model.CategoriaPersonalId); return View("Form",model); }
  [Permiso("personas.crear"), HttpPost, ValidateAntiForgeryToken] public ActionResult Crear([Bind(Include="Nombre,CategoriaPersonalId,FechaNacimiento,DPI,Telefono")] Persona model,HttpPostedFileBase foto,string captura) { return Persist(model,foto,captura,null); }
  [Permiso("personas.editar"), HttpPost, ValidateAntiForgeryToken] public ActionResult Editar(int id,[Bind(Include="Nombre,CategoriaPersonalId,FechaNacimiento,DPI,Telefono")] Persona model,HttpPostedFileBase foto,string captura) { var old=Find(id); if(old==null)return HttpNotFound(); model.Id=id; return Persist(model,foto,captura,old); }
  private ActionResult Persist(Persona model,HttpPostedFileBase foto,string captura,Persona old)
  {
   var bytes=ReadPhoto(foto,captura); model.Fotografia=old?.Fotografia;
   if(model.FechaNacimiento.HasValue && (model.FechaNacimiento.Value.Date>DateTime.Today || model.FechaNacimiento.Value.Year<1900)) ModelState.AddModelError("FechaNacimiento","Ingresa una fecha desde 1900 hasta hoy.");
   if(ModelState.IsValid)
   {
    string previous=model.Fotografia; model.Fotografia=StorePhoto(bytes,previous);
    bool saved=Save(()=>Db.Execute(old==null ? "INSERT Persona(Nombre,CategoriaPersonalId,FechaNacimiento,DPI,Telefono,Fotografia) VALUES(@nombre,@categoria,@fecha,@dpi,@telefono,@foto)" : "UPDATE Persona SET Nombre=@nombre,CategoriaPersonalId=@categoria,FechaNacimiento=@fecha,DPI=@dpi,Telefono=@telefono,Fotografia=@foto WHERE Id=@id", Db.P("@nombre",model.Nombre.Trim()),Db.P("@categoria",model.CategoriaPersonalId),Db.P("@fecha",model.FechaNacimiento),Db.P("@dpi",model.DPI),Db.P("@telefono",model.Telefono),Db.P("@foto",model.Fotografia),Db.P("@id",model.Id)));
    if(saved) { if(bytes!=null)DeletePhoto(previous); return RedirectToAction("Index"); }
    if(bytes!=null)DeletePhoto(model.Fotografia); model.Fotografia=previous;
   }
   Categories(model.CategoriaPersonalId); return View("Form",model);
  }
  [Permiso("personas.eliminar")] public ActionResult Eliminar(int id) { var model=Find(id); if(model==null)return HttpNotFound(); ViewBag.Nombre=model.Nombre; return View("~/Views/Shared/Delete.cshtml"); }
  [Permiso("personas.eliminar"),HttpPost,ValidateAntiForgeryToken,ActionName("Eliminar")] public ActionResult ConfirmarEliminar(int id) { var model=Find(id); if(model==null)return HttpNotFound(); if(Save(()=>Db.Execute("DELETE Persona WHERE Id=@id",Db.P("@id",id)))) { DeletePhoto(model.Fotografia); return RedirectToAction("Index"); } ViewBag.Nombre=model.Nombre; return View("~/Views/Shared/Delete.cshtml"); }
 }
}
