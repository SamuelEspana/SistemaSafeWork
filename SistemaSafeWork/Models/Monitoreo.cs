using System;
using System.Collections.Generic;
namespace SistemaSafeWork.Models
{
 public sealed class PersonaObservada
 {
  public double X {get;set;} public double Y {get;set;} public double Ancho {get;set;} public double Alto {get;set;}
  public string Casco {get;set;} public double Confianza {get;set;}
 }
 public sealed class EventoMonitoreo
 {
  public Guid Id {get;set;}=Guid.NewGuid(); public Guid Pista {get;set;}
  public DateTime FechaUtc {get;set;} public string Observacion {get;set;}
 }
 public sealed class CapturaMonitoreo
 {
  public int Id {get;set;} public DateTime FechaUtc {get;set;} public string Observacion {get;set;}
  public string FechaLocal {get {return DateTime.SpecifyKind(FechaUtc,DateTimeKind.Utc).AddHours(-6).ToString("dd/MM/yyyy HH:mm:ss");}}
 }
 public sealed class ListaMonitoreo
 {
  public List<CapturaMonitoreo> Capturas {get;set;} public int Pagina {get;set;} public bool HayMas {get;set;}
 }
}
