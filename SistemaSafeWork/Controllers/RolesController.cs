using System.Linq;
using System.Data.SqlClient;
using System.Web.Mvc;
using SistemaSafeWork.Models;
namespace SistemaSafeWork.Controllers
{
 public class RolesController : BaseController
 {
  private Rol Find(int id){var rows=Db.Query("SELECT * FROM Rol WHERE Id=@id",Db.P("@id",id));return rows.Rows.Count==0?null:new Rol{Id=id,Nombre=Db.Text(rows.Rows[0],"Nombre"),Permisos=Db.Map(Db.Query("SELECT Permiso FROM RolPermiso WHERE RolId=@id",Db.P("@id",id)),r=>Db.Text(r,"Permiso")).ToArray()};}
  [Permiso("roles.ver")]public ActionResult Index(){return View(Db.Query("SELECT r.*, (SELECT COUNT(*) FROM RolPermiso p WHERE p.RolId=r.Id) Permisos FROM Rol r ORDER BY r.Nombre"));}
  [Permiso("roles.crear")]public ActionResult Crear(){return View("Form",new Rol());}
  [Permiso("roles.editar")]public ActionResult Editar(int id){var model=Find(id);ViewBag.AdminRole=model?.Nombre=="Administrador";return model==null?(ActionResult)HttpNotFound():View("Form",model);}
  [Permiso("roles.crear"),HttpPost,ValidateAntiForgeryToken]public ActionResult Crear([Bind(Include="Nombre,Permisos")]Rol model){return Persist(model,null);}
  [Permiso("roles.editar"),HttpPost,ValidateAntiForgeryToken]public ActionResult Editar(int id,[Bind(Include="Nombre,Permisos")]Rol model){var old=Find(id);if(old==null)return HttpNotFound();model.Id=id;return Persist(model,old);}
  private ActionResult Persist(Rol model,Rol old)
  {
   ViewBag.AdminRole=old?.Nombre=="Administrador"; model.Permisos=(model.Permisos??new string[0]).Distinct().ToArray();
   if(old?.Nombre=="Administrador")ModelState.AddModelError("","El rol Administrador tiene acceso total y no puede modificarse.");
   if(model.Nombre?.Trim().ToLowerInvariant()=="administrador" && old?.Nombre!="Administrador")ModelState.AddModelError("Nombre","Este nombre está reservado.");
   if(model.Permisos.Except(Access.Permisos).Any())ModelState.AddModelError("Permisos","Hay permisos no válidos.");
   if(ModelState.IsValid && Save(()=>{using(var c=Db.Open())using(var tx=c.BeginTransaction())
   {
    using(var cmd=new SqlCommand(old==null?"INSERT Rol(Nombre) VALUES(@nombre); SELECT CAST(SCOPE_IDENTITY() AS int)":"UPDATE Rol SET Nombre=@nombre WHERE Id=@id; SELECT @id",c,tx)){cmd.Parameters.AddRange(new[]{Db.P("@nombre",model.Nombre.Trim()),Db.P("@id",model.Id)});model.Id=(int)cmd.ExecuteScalar();}
    using(var cmd=new SqlCommand("DELETE RolPermiso WHERE RolId=@id",c,tx)){cmd.Parameters.Add(Db.P("@id",model.Id));cmd.ExecuteNonQuery();}
    foreach(var p in model.Permisos)using(var cmd=new SqlCommand("INSERT RolPermiso(RolId,Permiso) VALUES(@id,@permiso)",c,tx)){cmd.Parameters.AddRange(new[]{Db.P("@id",model.Id),Db.P("@permiso",p)});cmd.ExecuteNonQuery();}
    tx.Commit();
   }}))return RedirectToAction("Index");return View("Form",model);
  }
  [Permiso("roles.eliminar")]public ActionResult Eliminar(int id){var model=Find(id);if(model==null)return HttpNotFound();ViewBag.Nombre=model.Nombre;return View("~/Views/Shared/Delete.cshtml");}
  [Permiso("roles.eliminar"),HttpPost,ValidateAntiForgeryToken,ActionName("Eliminar")]public ActionResult ConfirmarEliminar(int id)
  {var model=Find(id);if(model==null)return HttpNotFound();if(model.Nombre=="Administrador")ModelState.AddModelError("","El rol Administrador no puede eliminarse.");if(ModelState.IsValid&&Save(()=>{using(var c=Db.Open())using(var tx=c.BeginTransaction())using(var cmd=new SqlCommand("DELETE RolPermiso WHERE RolId=@id;DELETE Rol WHERE Id=@id",c,tx)){cmd.Parameters.Add(Db.P("@id",id));cmd.ExecuteNonQuery();tx.Commit();}}))return RedirectToAction("Index");ViewBag.Nombre=model.Nombre;return View("~/Views/Shared/Delete.cshtml");}
 }
}


