using System;
using System.Collections.Generic;
using System.Linq;
namespace SistemaSafeWork.Models
{
 public static class ZonasCuerpo
 {
  public static readonly string[] Todas={"Sin definir","Cabeza","Rostro","Pecho","Manos","Piernas","Pies","Cuerpo completo","Otra"};
 }
 public static class ColoresAditamento
 {
  public static readonly string[] Valores={"negro","blanco","gris","rojo","naranja","amarillo","verde","azul","morado","rosado","marron"};
 }
 public class PersonaValidacion
 {
  public int Id {get;set;}
  public string Nombre {get;set;}
  public string Categoria {get;set;}
  public List<Aditamento> Aditamentos {get;set;} = new List<Aditamento>();
 }
 public class ResultadoEquipo
 {
  public int Id {get;set;}
  public string Nombre {get;set;}
  public string ZonaCuerpo {get;set;}
  public string Estado {get;set;}
  public double Confianza {get;set;}
  public string ColorEsperado {get;set;}
  public string ColorObservado {get;set;}
  public double ConfianzaColor {get;set;}
  public string Observacion {get;set;}
 }
 public class ResultadoValidacion
 {
  public string Resultado {get;set;}
  public string Mensaje {get;set;}
  public int PersonasVisibles {get;set;}
  public List<ResultadoEquipo> Equipos {get;set;} = new List<ResultadoEquipo>();
 }
 public class VisionException : Exception
 {
  public VisionException(string message):base(message){}
 }
}
