using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Web;
using System.Web.Mvc;
using SistemaSafeWork.Models;
namespace SistemaSafeWork.Controllers
{
    public abstract class BaseController : Controller
    {
        protected bool Save(Action action)
        {
            try { action(); TempData["Success"] = "Cambios guardados correctamente."; return true; }
            catch (LastAdministratorException) { ModelState.AddModelError("", "Debe quedar al menos un administrador activo."); return false; }
            catch (SqlException ex)
            {
                if (ex.Number == 2601 || ex.Number == 2627) ModelState.AddModelError("", "Ya existe un registro con el mismo código, DPI, usuario o nombre único.");
                else if (ex.Number == 547) ModelState.AddModelError("", "El registro está en uso o la selección ya no existe. Revisa las referencias antes de continuar.");
                else throw;
                return false;
            }
        }
        protected byte[] ReadPhoto(HttpPostedFileBase foto, string captura = null)
        {
            if ((foto == null || foto.ContentLength == 0) && string.IsNullOrEmpty(captura)) return null;
            if (foto != null && foto.ContentLength > 0 && !string.IsNullOrEmpty(captura)) { ModelState.AddModelError("Fotografia", "Elige una fotografía cargada o una captura de cámara."); return null; }
            try
            {
                byte[] input;
                if (!string.IsNullOrEmpty(captura))
                {
                    const string prefix = "data:image/jpeg;base64,";
                    if (!captura.StartsWith(prefix, StringComparison.Ordinal) || captura.Length > 7000000) throw new ArgumentException();
                    input = Convert.FromBase64String(captura.Substring(prefix.Length));
                }
                else { if (foto.ContentLength > 5 * 1024 * 1024) throw new ArgumentException(); using (var stream = new MemoryStream()) { foto.InputStream.CopyTo(stream); input = stream.ToArray(); } }
                if (input.Length > 5 * 1024 * 1024) throw new ArgumentException();
                using (var stream = new MemoryStream(input)) using (var image = Image.FromStream(stream, true, true))
                {
                    if (image.Width > 6000 || image.Height > 6000 || image.Width * (long)image.Height > 20000000) throw new ArgumentException();
                    if (image.RawFormat.Guid != ImageFormat.Jpeg.Guid && image.RawFormat.Guid != ImageFormat.Png.Guid) throw new ArgumentException();
                    using (var bitmap = new Bitmap(image)) using (var output = new MemoryStream()) { bitmap.Save(output, ImageFormat.Jpeg); return output.ToArray(); }
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is OutOfMemoryException)
            { ModelState.AddModelError("Fotografia", "Usa una imagen JPG o PNG de hasta 5 MB y máximo 6000 px por lado (20 megapíxeles)."); return null; }
        }
        protected string StorePhoto(byte[] bytes, string previous)
        {
            if (bytes == null) return previous;
            string directory = Server.MapPath("~/App_Data/Fotos"); Directory.CreateDirectory(directory);
            string name = Guid.NewGuid().ToString("N") + ".jpg"; System.IO.File.WriteAllBytes(Path.Combine(directory, name), bytes); return name;
        }
        protected void DeletePhoto(string name)
        { if (!string.IsNullOrEmpty(name) && name == Path.GetFileName(name)) { var path = Server.MapPath("~/App_Data/Fotos/" + name); if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } }
        protected void Categories(int? selected = null) { ViewBag.Categorias = new SelectList(Db.Query("SELECT Id,Nombre FROM Categoria_Personal ORDER BY Nombre").DefaultView, "Id", "Nombre", selected); }
        protected void Roles(int? selected = null) { ViewBag.Roles = new SelectList(Db.Query("SELECT Id,Nombre FROM Rol ORDER BY Nombre").DefaultView, "Id", "Nombre", selected); }
    }
}

