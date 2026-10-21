using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using SistemaSafeWork.Models;
using SistemaSafeWork.Services;
class LocalVisionSmoke {
 static int Main(string[] args){var bin=args[0];AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>Assembly.LoadFrom(Path.Combine(bin,new AssemblyName(e.Name).Name+".dll"));try{Run();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static void Run(){
  byte[] image;using(var bitmap=new Bitmap(600,800))using(var graphics=Graphics.FromImage(bitmap))using(var output=new MemoryStream()){graphics.Clear(Color.White);bitmap.Save(output,ImageFormat.Jpeg);image=output.ToArray();}
  var settings=new VisionSettings{Provider="Ollama",Endpoint="http://127.0.0.1:11435/api/generate",Model="qwen2.5vl:3b",Threshold=.85};
  var gear=new List<Aditamento>{new Aditamento{Id=1,Nombre="Casco de seguridad naranja",Descripcion="Casco de protección de color naranja colocado en la cabeza",ZonaCuerpo="Cabeza"}};
  var start=DateTime.UtcNow;var result=new DetectorAditamentos().Analyze(image,gear,settings).GetAwaiter().GetResult();
  Console.WriteLine("Modelo local real: "+settings.Model+"; duración: "+(DateTime.UtcNow-start).TotalSeconds.ToString("F1")+" s");
  Console.WriteLine("Personas visibles="+result.PersonasVisibles+"; resultado="+result.Resultado);
  foreach(var item in result.Equipos)Console.WriteLine(item.Nombre+": "+item.Estado+"; "+item.Observacion);
  if(result.PersonasVisibles!=0||result.Resultado=="equipo_detectado")throw new Exception("La imagen vacía no debe confirmar persona ni equipo.");
  Console.WriteLine("LocalVisionSmoke=PASS (inferencia real con imagen sintética vacía, sin servicios externos)");
 }
}