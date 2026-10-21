using System;
using System.Collections.Generic;
using System.Linq;
using SistemaSafeWork.Models;
namespace SistemaSafeWork.Services
{
 // Asociación temporal por posición. No compara rostros ni identifica personas.
 public sealed class SeguimientoMonitoreo
 {
  private sealed class Pista
  {
   public Guid Id=Guid.NewGuid(); public PersonaObservada Persona; public DateTime UltimaGuardada;
   public EventoMonitoreo Pendiente;
  }
  private List<Pista> pistas=new List<Pista>(); private DateTime ultima;
  public void Reset(){pistas.Clear();ultima=default(DateTime);}
  private static double Coincidencia(PersonaObservada a,PersonaObservada b)
  {
   var w=Math.Max(0,Math.Min(a.X+a.Ancho,b.X+b.Ancho)-Math.Max(a.X,b.X));
   var h=Math.Max(0,Math.Min(a.Y+a.Alto,b.Y+b.Alto)-Math.Max(a.Y,b.Y));
   var intersection=w*h;var union=a.Ancho*a.Alto+b.Ancho*b.Alto-intersection;
   var distance=Math.Sqrt(Math.Pow(a.X+a.Ancho/2-b.X-b.Ancho/2,2)+Math.Pow(a.Y+a.Alto/2-b.Y-b.Alto/2,2));
   return union>0&&distance<=0.18?intersection/union:0;
  }
  public List<EventoMonitoreo> Update(List<PersonaObservada> personas,DateTime fechaUtc)
  {
   if(ultima!=default(DateTime)&&(fechaUtc<=ultima||(fechaUtc-ultima).TotalSeconds>20))Reset();
   ultima=fechaUtc;var next=new List<Pista>();var events=new List<EventoMonitoreo>();
   foreach(var person in personas)
   {
    var candidates=pistas.Where(p=>Coincidencia(p.Persona,person)>=0.35).ToList();
    // Un cruce o dos posibles asociaciones inicia una pista nueva, sin heredar el período entre capturas.
    var track=candidates.Count==1&&personas.Count(x=>Coincidencia(candidates[0].Persona,x)>=0.35)==1?candidates[0]:new Pista();
    track.Persona=person;
    if(person.Casco=="con_casco")
    {track.Pendiente=null;track.UltimaGuardada=default(DateTime);}
    else if(person.Casco=="sin_casco")
    {
     if(track.Pendiente==null&&(track.UltimaGuardada==default(DateTime)||(fechaUtc-track.UltimaGuardada).TotalSeconds>=15))
      track.Pendiente=new EventoMonitoreo{Pista=track.Id,FechaUtc=fechaUtc,Observacion="Persona sin casco colocado en la cabeza. Se registra desde la primera observación, sin comprobar color ni exigir un retiro previo."};
     if(track.Pendiente!=null)events.Add(track.Pendiente);
    }
    else track.Pendiente=null;
    next.Add(track);
   }
   pistas=next;return events;
  }
  public void Confirmar(Guid evento)
  {var track=pistas.FirstOrDefault(p=>p.Pendiente?.Id==evento);if(track!=null){track.UltimaGuardada=track.Pendiente.FechaUtc;track.Pendiente=null;}}
 }
}
