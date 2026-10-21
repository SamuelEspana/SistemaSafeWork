using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using SistemaSafeWork.Models;
using SistemaSafeWork.Services;
class LightingSmoke {
 static int Main(string[] args){var bin=args[0];AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>Assembly.LoadFrom(Path.Combine(bin,new AssemblyName(e.Name).Name+".dll"));try{Run(args[1],args[2]);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static byte[] Jpeg(string path,int max){using(var image=Image.FromFile(path)){double scale=Math.Min(1,(double)max/Math.Max(image.Width,image.Height));using(var bitmap=new Bitmap((int)(image.Width*scale),(int)(image.Height*scale)))using(var g=Graphics.FromImage(bitmap))using(var output=new MemoryStream()){g.DrawImage(image,0,0,bitmap.Width,bitmap.Height);bitmap.Save(output,ImageFormat.Jpeg);return output.ToArray();}}}
 static void Run(string capture,string reference){
  var settings=new VisionSettings{Provider="Ollama",Endpoint="http://127.0.0.1:11435/api/generate",Model="qwen2.5vl:3b",Threshold=.75,ColorThreshold=.65,IncludeReferencePhotos=true};
  var gear=new List<Aditamento>{new Aditamento{Id=14,Nombre="Casco Color Naranja",Descripcion="Este casco lo utiliza el personal obrero.",ZonaCuerpo="Cabeza",Fotografia="referencia.jpg"}};
  var start=DateTime.UtcNow;var result=new DetectorAditamentos().Analyze(Jpeg(capture,1600),gear,settings,a=>Jpeg(reference,512)).GetAwaiter().GetResult();
  Console.WriteLine("Prueba local de la captura adjunta, sin reconocimiento de identidad ni servicios externos. Duración="+(DateTime.UtcNow-start).TotalSeconds.ToString("F1")+" s");
  Console.WriteLine("Personas visibles="+result.PersonasVisibles+", resultado="+result.Resultado);
  foreach(var item in result.Equipos)Console.WriteLine("Estado="+item.Estado+", confianza="+item.Confianza+", referencia="+item.ColorEsperado+", observado="+item.ColorObservado+", confianza_color="+item.ConfianzaColor+"; "+item.Observacion);
  if(result.PersonasVisibles!=1||result.Resultado!="equipo_detectado")throw new Exception("La captura de prueba no confirma el casco naranja colocado.");
  Console.WriteLine("LightingSmoke=PASS (captura real adjunta y referencia del catálogo, solo procesamiento local)");
 }
}