using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using SistemaSafeWork.Models;
using SistemaSafeWork.Services;
class MonitorRegression {
 static int Main(string[] args){var bin=args[0];AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>Assembly.LoadFrom(Path.Combine(bin,new AssemblyName(e.Name).Name+".dll"));try{Run();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static void Assert(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
 static PersonaObservada Person(string state,double x=.1){return new PersonaObservada{X=x,Y=.1,Ancho=.3,Alto=.7,Casco=state,Confianza=.99};}
 static List<PersonaObservada> Frame(string state,double x=.1){return new List<PersonaObservada>{Person(state,x)};}
 static string Scene(bool visible,bool worn,double confidence,string extra=""){return "{\"hay_personas\":true,\"personas\":[{\"x\":100,\"y\":100,\"ancho\":300,\"alto\":700,\"cabeza_visible\":"+(visible?"true":"false")+",\"casco_en_cabeza\":"+(worn?"true":"false")+",\"confianza\":"+confidence.ToString(System.Globalization.CultureInfo.InvariantCulture)+extra+"}]}";}
 static void Run(){
  var now=DateTime.UtcNow;var track=new SeguimientoMonitoreo();
  var first=track.Update(Frame("sin_casco"),now);Assert(first.Count==1,"Entrar sin casco se captura desde la primera observación");
  Assert(track.Update(Frame("sin_casco"),now.AddSeconds(3))[0].Id==first[0].Id,"Un evento pendiente mantiene el identificador al reintentar");
  track.Confirmar(first[0].Id);Assert(track.Update(Frame("sin_casco"),now.AddSeconds(6)).Count==0,"No se duplica inmediatamente la misma pista");
  Assert(track.Update(Frame("sin_casco"),now.AddSeconds(15)).Count==1,"Una persona que sigue sin casco genera otra captura después de 15 segundos");
  track.Reset();Assert(track.Update(Frame("con_casco"),now).Count==0,"Un casco puesto no genera una captura");
  Assert(track.Update(Frame("sin_casco"),now.AddSeconds(3)).Count==1,"Quitar el casco se registra con una sola observación sin casco");
  track.Reset();Assert(track.Update(new List<PersonaObservada>(),now).Count==0,"Una escena vacía no genera capturas");
  Assert(track.Update(Frame("no_verificable"),now.AddSeconds(3)).Count==0,"Una cabeza no visible se informa como no verificable");
  Assert(track.Update(Frame("sin_casco"),now.AddSeconds(6)).Count==1,"No se exige un historial de casco tras una escena incierta");
  track.Reset();var two=new List<PersonaObservada>{Person("sin_casco",.05),Person("sin_casco",.65)};Assert(track.Update(two,now).Count==2,"Varias personas sin casco se registran independientemente desde la primera escena");
  Assert(DetectorMonitoreo.Parse(Scene(true,false,.9,",\"casco\":\"con_casco\""),.5)[0].Casco=="sin_casco","Casco presente en otra parte no sustituye casco_en_cabeza=false");
  Assert(DetectorMonitoreo.Parse(Scene(true,true,.9),.5)[0].Casco=="con_casco","Cualquier casco colocado en la cabeza se considera puesto");
  Assert(DetectorMonitoreo.Parse(Scene(false,false,.99),.5)[0].Casco=="no_verificable","No se infiere ausencia si la cabeza queda fuera del encuadre");
  Assert(DetectorMonitoreo.Parse(Scene(true,false,.55),.5)[0].Casco=="sin_casco","Umbral de monitoreo 0.50 acepta detecciones para pruebas con confianza moderada");
  Assert(DetectorMonitoreo.Parse(Scene(true,false,.4),.5)[0].Casco=="no_verificable","La incertidumbre por debajo del umbral permanece explícita");
  Assert(DetectorMonitoreo.Parse(Scene(true,false,.1),0)[0].Casco=="sin_casco","El modo de pruebas registra ausencia observada aunque la confianza sea baja");
  foreach(var bad in new[]{"{\"hay_personas\":true,\"personas\":[7]}","{}",Scene(true,false,2)}){bool rejected=false;try{DetectorMonitoreo.Parse(bad,.5);}catch(Exception ex)when(ex is VisionException||ex is Newtonsoft.Json.JsonException){rejected=true;}Assert(rejected,"Una respuesta inválida no crea una captura");}
  Assert(DetectorMonitoreo.Request(new byte[]{1},new VisionSettings{Model="qwen2.5vl:3b"})["images"].Count()==1,"Se analiza solo la captura actual, sin catálogo de colores");
  Console.WriteLine("MonitorRegression=PASS");
 }
}