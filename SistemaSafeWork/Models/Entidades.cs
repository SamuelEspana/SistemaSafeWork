using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SistemaSafeWork.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        [Required, StringLength(120)] public string Nombre { get; set; }
        [Required, StringLength(60), Display(Name = "Usuario")] public string NombreUsuario { get; set; }
        [StringLength(128, MinimumLength = 10), DataType(DataType.Password)] public string Contrasena { get; set; }
        [Range(1, int.MaxValue), Display(Name = "Rol")] public int RolId { get; set; }
        public string Rol { get; set; }
        public bool Activo { get; set; } = true;
    }
    public class Persona
    {
        public int Id { get; set; }
        [Required, StringLength(150)] public string Nombre { get; set; }
        [Range(1, int.MaxValue), Display(Name = "Categoría de personal")] public int CategoriaPersonalId { get; set; }
        public string Categoria { get; set; }
        [Required, DataType(DataType.Date), Display(Name = "Fecha de nacimiento")] public DateTime? FechaNacimiento { get; set; }
        [Required, RegularExpression(@"^[0-9]{13}$", ErrorMessage = "El DPI debe contener 13 dígitos.")] public string DPI { get; set; }
        [Required, RegularExpression(@"^[0-9]{8}$", ErrorMessage = "El teléfono debe contener exactamente 8 dígitos."), Display(Name = "Número de teléfono")] public string Telefono { get; set; }
        public string Fotografia { get; set; }
    }
    public class Aditamento
    {
        public int Id { get; set; }
        [Required, StringLength(40), Display(Name = "Código")] public string Codigo { get; set; }
        [Required, StringLength(30), Display(Name = "Zona del cuerpo")] public string ZonaCuerpo { get; set; } = "Sin definir";
        [StringLength(20), Display(Name = "Color requerido")] public string ColorRequerido { get; set; }
        [Required, StringLength(120)] public string Nombre { get; set; }
        [StringLength(1000), Display(Name = "Descripción")] public string Descripcion { get; set; }
        public string Fotografia { get; set; }
    }
    public class Categoria_Personal
    {
        public int Id { get; set; }
        [Required, StringLength(120)] public string Nombre { get; set; }
        [StringLength(1000), Display(Name = "Descripción")] public string Descripcion { get; set; }
        public int[] AditamentoIds { get; set; } = new int[0];
    }
    public class Rol
    {
        public int Id { get; set; }
        [Required, StringLength(60)] public string Nombre { get; set; }
        public string[] Permisos { get; set; } = new string[0];
    }
    public class LoginModel
    {
        [Required, StringLength(60)] public string Usuario { get; set; }
        [Required, DataType(DataType.Password)] public string Contrasena { get; set; }
    }
    public class CambioContrasena
    {
        [Required, DataType(DataType.Password), Display(Name = "Contraseña actual")] public string Actual { get; set; }
        [Required, StringLength(128, MinimumLength = 10), DataType(DataType.Password), Display(Name = "Nueva contraseña")] public string Nueva { get; set; }
        [Compare("Nueva"), DataType(DataType.Password), Display(Name = "Confirmar contraseña")] public string Confirmacion { get; set; }
    }
}

