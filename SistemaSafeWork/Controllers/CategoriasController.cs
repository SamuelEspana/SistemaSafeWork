using System;
using System.Linq;
using System.Data.SqlClient;
using System.Web.Mvc;
using SistemaSafeWork.Models;
namespace SistemaSafeWork.Controllers
{
 public class CategoriasController : BaseController
 {
  private Categoria_Personal Find(int id) { var rows=Db.Query("SELECT * FROM Categoria_Personal WHERE Id=@id",Db.P("@id",id)); if(rows.Rows.Count==0)return null; var row=rows.Rows[0]; return new Categoria_Personal {Id=id,Nombre=Db.Text(row,"Nombre"),Descripcion=Db.Text(row,"Descripcion"),AditamentoIds=Db.Map(Db.Query("SELECT AditamentoId FROM CategoriaAditamento WHERE CategoriaPersonalId=@id",Db.P("@id",id)),r=>(int)r["AditamentoId"]).ToArray()}; }
  private void Options() { ViewBag.Aditamentos=Db.Map(Db.Query("SELECT * FROM Aditamento ORDER BY Nombre"),r=>new Aditamento {Id=(int)r["Id"],Codigo=Db.Text(r,"Codigo"),Nombre=Db.Text(r,"Nombre"),Descripcion=Db.Text(r,"Descripcion")}); }
  [Permiso("categorias.ver")] public ActionResult Index(string q) { ViewBag.Q=q; return View(Db.Query("SELECT c.*, (SELECT COUNT(*) FROM CategoriaAditamento d WHERE d.CategoriaPersonalId=c.Id) Equipos FROM Categoria_Personal c WHERE @q='' OR c.Nombre LIKE @search ORDER BY c.Nombre",Db.P("@q",q??""),Db.P("@search","%"+(q??"")+"%"))); }
  [Permiso("categorias.ver")] public ActionResult Detalle(int id) { var model=Find(id); if(model==null)return HttpNotFound(); Options(); return View(model); }
  [Permiso("categorias.crear")] public ActionResult Crear() { Options(); return View("Form",new Categoria_Personal()); }
  [Permiso("categorias.editar")] public ActionResult Editar(int id) { var model=Find(id); if(model==null)return HttpNotFound(); Options(); return View("Form",model); }
  [Permiso("categorias.crear"),HttpPost,ValidateAntiForgeryToken] public ActionResult Crear([Bind(Include="Nombre,Descripcion,AditamentoIds")] Categoria_Personal model) { return Persist(model,false); }
  [Permiso("categorias.editar"),HttpPost,ValidateAntiForgeryToken] public ActionResult Editar(int id,[Bind(Include="Nombre,Descripcion,AditamentoIds")] Categoria_Personal model) { if(Find(id)==null)return HttpNotFound(); model.Id=id; return Persist(model,true); }
  private ActionResult Persist(Categoria_Personal model,bool edit)
  {
   model.AditamentoIds=(model.AditamentoIds??new int[0]).Distinct().ToArray();
   if(model.AditamentoIds.Length==0)ModelState.AddModelError("AditamentoIds","Selecciona al menos un aditamento requerido.");
   if(ModelState.IsValid && Save(()=>
   {
    using(var connection=Db.Open()) using(var tx=connection.BeginTransaction())
    {
     using(var command=new SqlCommand(edit?"UPDATE Categoria_Personal SET Nombre=@nombre,Descripcion=@descripcion WHERE Id=@id; SELECT @id":"INSERT Categoria_Personal(Nombre,Descripcion) VALUES(@nombre,@descripcion); SELECT CAST(SCOPE_IDENTITY() AS int)",connection,tx))
     { command.Parameters.AddRange(new[]{Db.P("@nombre",model.Nombre.Trim()),Db.P("@descripcion",model.Descripcion),Db.P("@id",model.Id)}); model.Id=(int)command.ExecuteScalar(); }
     using(var command=new SqlCommand("DELETE CategoriaAditamento WHERE CategoriaPersonalId=@id",connection,tx)) { command.Parameters.Add(Db.P("@id",model.Id)); command.ExecuteNonQuery(); }
     foreach(var item in model.AditamentoIds) using(var command=new SqlCommand("INSERT CategoriaAditamento(CategoriaPersonalId,AditamentoId) VALUES(@id,@item)",connection,tx)) { command.Parameters.AddRange(new[]{Db.P("@id",model.Id),Db.P("@item",item)}); command.ExecuteNonQuery(); }
     tx.Commit();
    }
   })) return RedirectToAction("Index");
   Options(); return View("Form",model);
  }
  [Permiso("categorias.eliminar")] public ActionResult Eliminar(int id) { var model=Find(id); if(model==null)return HttpNotFound(); ViewBag.Nombre=model.Nombre; return View("~/Views/Shared/Delete.cshtml"); }
  [Permiso("categorias.eliminar"),HttpPost,ValidateAntiForgeryToken,ActionName("Eliminar")] public ActionResult ConfirmarEliminar(int id)
  { var model=Find(id); if(model==null)return HttpNotFound(); if(Save(()=> { using(var c=Db.Open())using(var tx=c.BeginTransaction())using(var command=new SqlCommand("DELETE CategoriaAditamento WHERE CategoriaPersonalId=@id; DELETE Categoria_Personal WHERE Id=@id",c,tx)) { command.Parameters.Add(Db.P("@id",id)); command.ExecuteNonQuery(); tx.Commit(); } }))return RedirectToAction("Index"); ViewBag.Nombre=model.Nombre; return View("~/Views/Shared/Delete.cshtml"); }
 }
}

