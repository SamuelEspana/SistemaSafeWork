using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using SistemaSafeWork.Models;
using SistemaSafeWork.Services;
class ColorReferenceSmoke {
 static int Main(string[] args){var bin=args[0];AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>Assembly.LoadFrom(Path.Combine(bin,new AssemblyName(e.Name).Name+".dll"));try{Run(args[1]);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static byte[] Reference(string folder,string name){using(var image=Image.FromFile(Path.Combine(folder,name))){double scale=Math.Min(1,512.0/Math.Max(image.Width,image.Height));using(var resized=new Bitmap((int)(image.Width*scale),(int)(image.Height*scale)))using(var graphics=Graphics.FromImage(resized))using(var output=new MemoryStream()){graphics.DrawImage(image,0,0,resized.Width,resized.Height);resized.Save(output,ImageFormat.Jpeg);return output.ToArray();}}}
 static void Run(string folder){
  byte[] image;using(var bitmap=new Bitmap(400,600))using(var graphics=Graphics.FromImage(bitmap))using(var output=new MemoryStream()){graphics.Clear(Color.White);bitmap.Save(output,ImageFormat.Jpeg);image=output.ToArray();}
  var settings=new VisionSettings{Provider="Ollama",Endpoint="http://127.0.0.1:11435/api/generate",Model="qwen2.5vl:3b",Threshold=.85,IncludeReferencePhotos=true};
  var gear=new List<Aditamento>{new Aditamento{Id=14,Nombre="Casco Color Naranja",ZonaCuerpo="Cabeza",Fotografia="6212bb410e9b42d2afc77c3b7dfd713e.jpg"},new Aditamento{Id=15,Nombre="Casco supervisor",ZonaCuerpo="Cabeza",Fotografia="629b81f10cdf42cfab1c78c8495e559c.jpg"}};
  var start=DateTime.UtcNow;var result=new DetectorAditamentos().Analyze(image,gear,settings,a=>Reference(folder,a.Fotografia)).GetAwaiter().GetResult();
  Console.WriteLine("Motor local real con las dos fotos del catálogo; duración="+(DateTime.UtcNow-start).TotalSeconds.ToString("F1")+" s");
  foreach(var item in result.Equipos)Console.WriteLine(item.Nombre+": referencia="+item.ColorEsperado+", observado="+item.ColorObservado+", estado="+item.Estado);
  if(result.PersonasVisibles!=0||result.Resultado=="equipo_detectado")throw new Exception("Las fotos de referencia no deben contarse como personas ni como equipo portado.");
  if(result.Equipos.Single(e=>e.Id==14).ColorEsperado!="naranja"||result.Equipos.Single(e=>e.Id==15).ColorEsperado!="verde")throw new Exception("Los colores de las fotos reales no se interpretaron correctamente.");
  Console.WriteLine("ColorReferenceSmoke=PASS (fotos reales de objetos, captura vacía, solo motor local)");
 }
}