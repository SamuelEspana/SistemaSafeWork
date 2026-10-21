using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using SistemaSafeWork.Models;
namespace SistemaSafeWork.Controllers
{
 public class UsuariosController : BaseController
 {
  private Usuario Find(int id) { var list=Db.Map(Db.Query("SELECT u.*,r.Nombre Rol FROM Usuario u JOIN Rol r ON r.Id=u.RolId WHERE u.Id=@id",Db.P("@id",id)),r=>new Usuario {Id=(int)r["Id"],Nombre=Db.Text(r,"Nombre"),NombreUsuario=Db.Text(r,"NombreUsuario"),RolId=(int)r["RolId"],Rol=Db.Text(r,"Rol"),Activo=(bool)r["Activo"]}); return list.Count==0?null:list[0]; }
  [Permiso("usuarios.ver")] public ActionResult Index() { return View(Db.Query("SELECT u.Id,u.Nombre,u.NombreUsuario,u.Activo,r.Nombre Rol FROM Usuario u JOIN Rol r ON r.Id=u.RolId ORDER BY u.Nombre")); }
  [Permiso("usuarios.crear")] public ActionResult Crear() { Roles(); return View("Form",new Usuario()); }
  [Permiso("usuarios.editar")] public ActionResult Editar(int id) { var model=Find(id); if(model==null)return HttpNotFound(); Roles(model.RolId); return View("Form",model); }
  [Permiso("usuarios.crear"),HttpPost,ValidateAntiForgeryToken] public ActionResult Crear([Bind(Include="Nombre,NombreUsuario,Contrasena,RolId,Activo")] Usuario model) { if(string.IsNullOrWhiteSpace(model.Contrasena))ModelState.AddModelError("Contrasena","La contraseña es obligatoria."); return Persist(model,null); }
  [Permiso("usuarios.editar"),HttpPost,ValidateAntiForgeryToken] public ActionResult Editar(int id,[Bind(Include="Nombre,NombreUsuario,Contrasena,RolId,Activo")] Usuario model) { var old=Find(id); if(old==null)return HttpNotFound(); model.Id=id; return Persist(model,old); }
  private ActionResult Persist(Usuario model,Usuario old)
  {
   if(old!=null && model.Id==Access.UserId(HttpContext) && (!model.Activo || model.RolId!=old.RolId)) ModelState.AddModelError("","No puedes desactivar tu propia cuenta ni cambiar tu propio rol.");
   if(ModelState.IsValid && Save(()=>
   {
    using(var c=Db.Open())using(var tx=c.BeginTransaction(IsolationLevel.Serializable))
    {
     if(old!=null) EnsureAdmin(c,tx,model.Id,!model.Activo,model.RolId);
     using(var cmd=new SqlCommand(old==null?"INSERT Usuario(Nombre,NombreUsuario,ContrasenaHash,RolId,Activo) VALUES(@nombre,@usuario,@hash,@rol,@activo)":"UPDATE Usuario SET Nombre=@nombre,NombreUsuario=@usuario,RolId=@rol,Activo=@activo,ContrasenaHash=COALESCE(@hash,ContrasenaHash),Intentos=CASE WHEN @hash IS NULL THEN Intentos ELSE 0 END,BloqueadoHasta=CASE WHEN @hash IS NULL THEN BloqueadoHasta ELSE NULL END WHERE Id=@id",c,tx))
     { cmd.Parameters.AddRange(new[]{Db.P("@nombre",model.Nombre.Trim()),Db.P("@usuario",model.NombreUsuario.Trim()),Db.P("@hash",string.IsNullOrEmpty(model.Contrasena)?null:Passwords.Hash(model.Contrasena)),Db.P("@rol",model.RolId),Db.P("@activo",model.Activo),Db.P("@id",model.Id)});cmd.ExecuteNonQuery(); }
     tx.Commit();
    }
   }))return RedirectToAction("Index");
   Roles(model.RolId); model.Contrasena=null;  return View("Form",model);
  }
  private static void EnsureAdmin(SqlConnection c,SqlTransaction tx,int id,bool remove,int rol)
  {
   using(var cmd=new SqlCommand("SELECT COUNT(*) FROM Usuario u WITH(UPDLOCK,HOLDLOCK) JOIN Rol r ON r.Id=u.RolId WHERE u.Activo=1 AND r.Nombre=N'Administrador' AND (u.Id<>@id OR (@remove=0 AND @rol=u.RolId))",c,tx))
   {cmd.Parameters.AddRange(new[]{Db.P("@id",id),Db.P("@remove",remove),Db.P("@rol",rol)}); if((int)cmd.ExecuteScalar()==0)throw new LastAdministratorException();}
  }
  [Permiso("usuarios.eliminar")] public ActionResult Eliminar(int id) { var model=Find(id); if(model==null)return HttpNotFound(); ViewBag.Nombre=model.Nombre; return View("~/Views/Shared/Delete.cshtml"); }
  [Permiso("usuarios.eliminar"),HttpPost,ValidateAntiForgeryToken,ActionName("Eliminar")] public ActionResult ConfirmarEliminar(int id)
  {
   var model=Find(id); if(model==null)return HttpNotFound();
   if(id==Access.UserId(HttpContext))ModelState.AddModelError("","No puedes eliminar tu propia cuenta.");
   if(ModelState.IsValid && Save(()=>{using(var c=Db.Open())using(var tx=c.BeginTransaction(IsolationLevel.Serializable)){EnsureAdmin(c,tx,id,true,0);using(var cmd=new SqlCommand("DELETE Usuario WHERE Id=@id",c,tx)){cmd.Parameters.Add(Db.P("@id",id));cmd.ExecuteNonQuery();}tx.Commit();}}))return RedirectToAction("Index");
   ViewBag.Nombre=model.Nombre;return View("~/Views/Shared/Delete.cshtml");
  }
 }
 public class LastAdministratorException : Exception { }
}


