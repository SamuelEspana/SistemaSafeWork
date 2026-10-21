using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using SistemaSafeWork.Services;
using System.Drawing.Drawing2D;
class MonitorSmoke {
 static bool cameraRegion; static bool wornRegion;
 static int Main(string[] args){var bin=args[0];AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>Assembly.LoadFrom(Path.Combine(bin,new AssemblyName(e.Name).Name+".dll"));try{cameraRegion=args.Length>3&&args[3]=="camera-region";wornRegion=args.Length>3&&args[3]=="camera-region-worn";Run(args[1],args.Length>2?args[2]:"con_casco");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static byte[] Picture(string path){
  using(var original=(Bitmap)Image.FromFile(path))using(var input=cameraRegion?original.Clone(new Rectangle(58,254,806,452),PixelFormat.Format24bppRgb):wornRegion?original.Clone(new Rectangle(40,112,740,555),PixelFormat.Format24bppRgb):(Bitmap)original.Clone()){
   var scale=Math.Min(1,640.0/Math.Max(input.Width,input.Height));
   using(var bitmap=new Bitmap((int)(input.Width*scale),(int)(input.Height*scale)))using(var g=Graphics.FromImage(bitmap))using(var output=new MemoryStream()){
    g.DrawImage(input,0,0,bitmap.Width,bitmap.Height);
    using(var quality=new EncoderParameters(1)){quality.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,90L);bitmap.Save(output,Array.Find(ImageCodecInfo.GetImageEncoders(),c=>c.MimeType=="image/jpeg"),quality);}return output.ToArray();
   }
  }
 }
 static void Run(string capture,string expected){
  var settings=new VisionSettings{Provider="Ollama",Endpoint="http://127.0.0.1:11435/api/generate",Model="qwen2.5vl:3b",Threshold=.75};
  var detector=new DetectorMonitoreo();var started=DateTime.UtcNow;var people=detector.Analyze(Picture(capture),settings,CancellationToken.None).GetAwaiter().GetResult();
  Console.WriteLine("Escena real local: personas="+people.Count+", duración="+(DateTime.UtcNow-started).TotalSeconds.ToString("F1")+" s");
  foreach(var person in people)Console.WriteLine("Casco="+person.Casco+", confianza="+person.Confianza+", caja="+person.X+","+person.Y+","+person.Ancho+","+person.Alto);
  if(people.Count!=1||people[0].Casco!=expected)throw new Exception("La escena adjunta no se evaluó correctamente: se esperaba "+expected+".");
  byte[] blank;using(var image=new Bitmap(400,300))using(var g=Graphics.FromImage(image))using(var output=new MemoryStream()){g.Clear(Color.White);image.Save(output,ImageFormat.Jpeg);blank=output.ToArray();}
  if(detector.Analyze(blank,settings,CancellationToken.None).GetAwaiter().GetResult().Count!=0)throw new Exception("Se inventó una persona en la escena vacía.");
  Console.WriteLine("MonitorSmoke=PASS (motor local real, estado del casco en la cabeza y captura vacía; no se guardan eventos)");
 }
}