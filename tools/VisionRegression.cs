using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SistemaSafeWork.Models;
using SistemaSafeWork.Services;
class VisionRegression
{
 static string bin;
 static int Main(string[] args){bin=args[0];AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>Assembly.LoadFrom(Path.Combine(bin,new AssemblyName(e.Name).Name+".dll"));try{Run().GetAwaiter().GetResult();return 0;}catch(Exception e){Console.Error.WriteLine(e.ToString());return 1;}}
 static void Assert(bool condition,string text){if(!condition)throw new Exception(text);Console.WriteLine("PASS: "+text);}
 static List<Aditamento> Gear(){return new List<Aditamento>{new Aditamento{Id=10,Nombre="Casco",ZonaCuerpo="Cabeza"},new Aditamento{Id=20,Nombre="Botas",ZonaCuerpo="Pies"}};}
 static string Data(int people,string items){return "{\"personas_visibles\":"+people+",\"equipos\":["+items+"]}";}
 static string Item(int id,string state,double confidence){return new JObject{{"id",id},{"estado",state},{"confianza",confidence},{"observacion","Evidencia de prueba"},{"color_referencia","no_verificable"},{"color_observado","no_verificable"},{"confianza_color",0}}.ToString();}
 static void Rejected(string json,string text){try{DetectorAditamentos.ParseResult(json,Gear(),.85);}catch(VisionException){Console.WriteLine("PASS: "+text);return;}throw new Exception(text);}
 static async Task Run()
 {
  var all=Data(1,Item(10,"detectado",.95)+","+Item(20,"detectado",.96));
  Assert(DetectorAditamentos.ParseResult(all,Gear(),.85).Resultado=="equipo_detectado","Equipo completo solo cuando todos los aditamentos se detectan");
  Assert(DetectorAditamentos.ParseResult(Data(1,Item(10,"detectado",.95)+","+Item(20,"no_detectado",.95)),Gear(),.85).Resultado=="equipo_faltante","La ausencia visible de botas se informa como equipo faltante");
  Assert(DetectorAditamentos.ParseResult(Data(1,Item(10,"detectado",.95)+","+Item(20,"no_verificable",.9)),Gear(),.85).Resultado=="revision_necesaria","Pies fuera del encuadre requieren revisión");
  Assert(DetectorAditamentos.ParseResult(Data(1,Item(10,"detectado",.5)+","+Item(20,"no_detectado",.5)),Gear(),.85).Equipos.All(x=>x.Estado=="no_verificable"),"Baja confianza no confirma presencia ni ausencia");
  Assert(DetectorAditamentos.ParseResult(Data(2,Item(10,"detectado",.99)+","+Item(20,"detectado",.99)),Gear(),.85).Equipos.All(x=>x.Estado=="no_verificable"),"Varias personas nunca confirman el equipo");
  Assert(DetectorAditamentos.ParseResult(Data(0,Item(10,"detectado",.99)+","+Item(20,"detectado",.99)),Gear(),.85).Resultado=="revision_necesaria","Una captura sin persona nunca confirma el equipo");
  Assert(DetectorAditamentos.ParseResult(Data(1,Item(10,"detectado",.99)),Gear(),.85).Equipos.Single(x=>x.Id==20).Estado=="no_verificable","Un resultado incompleto conserva como pendientes los aditamentos omitidos");
  Rejected(Data(1,Item(10,"detectado",.95)+","+Item(10,"detectado",.95)),"IDs duplicados se rechazan");
  Rejected(Data(1,Item(99,"detectado",.95)),"IDs ajenos al catálogo se rechazan");
  Rejected(Data(1,Item(10,"detectado",2)),"Confianza fuera de rango se rechaza");
  Rejected(Data(1,"7"),"Un resultado sin estructura de objeto se rechaza");
  Rejected(Data(1,"{\"id\":999999999999999999,\"estado\":\"detectado\",\"confianza\":1,\"observacion\":\"x\"}"),"Un ID fuera del rango entero se rechaza");
  Rejected(Data(1,"{\"id\":10,\"estado\":[],\"confianza\":1,\"observacion\":\"x\"}"),"Un estado con tipo incorrecto se rechaza");
  var settings=new VisionSettings{Endpoint="https://api.openai.com/v1/responses",Model="gpt-4.1-mini",ApiKey="TEST-ONLY",IncludeReferencePhotos=false};
  var payload=DetectorAditamentos.BuildRequest(new byte[]{1,2},Gear(),settings).ToString();
  Assert(!payload.Contains("TEST-ONLY")&&!payload.Contains("CategoriaPersonalId")&&!payload.Contains("DPI")&&!payload.Contains("persona.Nombre"),"El cuerpo de la petición no incluye clave ni datos identificativos");
  Assert((bool)JObject.Parse(payload)["store"]==false,"La petición deshabilita almacenamiento de respuestas");
  settings.Endpoint="http://example.invalid/responses";Assert(!settings.Ready,"El endpoint del proveedor requiere HTTPS");settings.Endpoint="https://api.openai.com/v1/responses";
  var handler=new FakeHandler(all);using(var client=new HttpClient(handler)){
   var result=await new DetectorAditamentos(client).Analyze(new byte[]{1,2},Gear(),settings);Assert(result.Resultado=="equipo_detectado"&&handler.Called,"Se interpreta una respuesta estructurada del servicio simulado");
  }
  using(var client=new HttpClient(new FakeHandler(all,HttpStatusCode.Unauthorized))){try{await new DetectorAditamentos(client).Analyze(new byte[]{1},Gear(),settings);throw new Exception("401 no rechazado");}catch(VisionException e){Assert(!e.Message.Contains(settings.ApiKey),"Un error del proveedor no expone la clave");}}
  var local=new VisionSettings{Provider="Ollama",Endpoint="http://127.0.0.1:11435/api/generate",Model="qwen2.5vl:3b"};
  Assert(local.Ready,"El motor local funciona sin API key");
  var localPayload=DetectorAditamentos.BuildLocalRequest(new byte[]{1,2},Gear(),local);
  Assert((bool)localPayload["stream"]==false&&localPayload["format"]["type"].ToString()=="object"&&localPayload["images"].Count()==1,"La petición local envía captura y esquema estructurado sin streaming");
  Assert(!localPayload.ToString().Contains("TEST-ONLY")&&!localPayload.ToString().Contains("CategoriaPersonalId"),"La petición local evita claves y datos personales");
  local.Endpoint="http://example.com/api/generate";Assert(!local.Ready,"El motor sin clave solo permite loopback, nunca servidores externos");local.Endpoint="http://127.0.0.1:11435/api/generate";
  var localHandler=new FakeHandler(all,HttpStatusCode.OK,true);
  using(var client=new HttpClient(localHandler)){
   var result=await new DetectorAditamentos(client).Analyze(new byte[]{1,2},Gear(),local);
   Assert(result.Resultado=="equipo_detectado"&&localHandler.Called&&!localHandler.HasAuthorization,"Una respuesta de Ollama se interpreta sin enviar Authorization");
  }
  foreach(var reason in new[]{"length","",null}){
   using(var client=new HttpClient(new FakeHandler(all,HttpStatusCode.OK,true,reason))){
    try{await new DetectorAditamentos(client).Analyze(new byte[]{1},Gear(),local);throw new Exception("Respuesta truncada confirmada");}catch(VisionException){Console.WriteLine("PASS: Ollama incompleto o truncado nunca confirma equipo");}
   }
  }
  using(var client=new HttpClient(new FakeHandler(all,HttpStatusCode.NotFound,true))){
   try{await new DetectorAditamentos(client).Analyze(new byte[]{1},Gear(),local);throw new Exception("404 no rechazado");}catch(VisionException e){Assert(e.Message.Contains("modelo local"),"Un modelo local faltante devuelve instrucciones claras");}
  }
  var colored=new List<Aditamento>{new Aditamento{Id=10,Nombre="Casco Color Naranja",ZonaCuerpo="Cabeza",Fotografia="referencia.jpg"}};
  var refIds=new HashSet<int>{10};
  Func<string,string,double,string> colorData=(reference,observed,confidence)=>{
   var item=JObject.Parse(Item(10,"detectado",.99));item["color_referencia"]=reference;item["color_observado"]=observed;item["confianza_color"]=confidence;return Data(1,item.ToString());
  };
  Assert(DetectorAditamentos.ParseResult(colorData("naranja","azul",.99),colored,.85,refIds).Resultado=="equipo_faltante","Casco azul nunca cumple una referencia naranja aunque la IA diga detectado");
  Assert(DetectorAditamentos.ParseResult(colorData("naranja","naranja",.99),colored,.85,refIds).Resultado=="equipo_detectado","El casco colocado y con color coincidente cumple el requisito");
  Assert(DetectorAditamentos.ParseResult(colorData("naranja","azul",.5),colored,.85,refIds).Resultado=="revision_necesaria","Color con baja confianza requiere revisión");
  Assert(DetectorAditamentos.ParseResult(colorData("no_verificable","naranja",.99),colored,.85,refIds).Resultado=="revision_necesaria","Una referencia de color ambigua nunca confirma equipo");
  Assert(DetectorAditamentos.ParseResult(colorData("naranja","naranja",.99),colored,.85,new HashSet<int>()).Resultado=="revision_necesaria","Una fotografía ausente o ilegible nunca se sustituye por una referencia inventada");
  Assert(DetectorAditamentos.ParseResult(colorData("azul","azul",.99),colored,.85,refIds).Resultado=="revision_necesaria","Nombre naranja y foto azul contradictorios requieren corregir el catálogo");
  colored[0].Nombre="Casco supervisor";
  Assert(DetectorAditamentos.ParseResult(colorData("verde","azul",.99),colored,.85,refIds).Resultado=="equipo_faltante","El color se compara desde la foto aunque el nombre no indique color");
  Assert(DetectorAditamentos.ParseResult(colorData("verde","verde",.99),colored,.85,refIds).Resultado=="equipo_detectado","Referencia verde sin color explícito en el nombre se valida por fotografía");
  colored[0].Fotografia=null;colored[0].Nombre="Casco naranja";
  Assert(DetectorAditamentos.ParseResult(colorData("no_verificable","azul",.99),colored,.85).Resultado=="equipo_faltante","Sin foto también se respeta un color explícito del nombre");
  local.IncludeReferencePhotos=true;colored[0].Fotografia="referencia.jpg";
  var calls=0;var imagePayload=DetectorAditamentos.BuildLocalRequest(new byte[]{1},colored,local,a=>{calls++;return new byte[]{2};});
  Assert(imagePayload["images"].Count()==2&&imagePayload["prompt"].ToString().Contains("Imagen 2: referencia de objeto id=10")&&calls==1,"La captura y su referencia están separadas y vinculadas por id");
  var responseJson=colorData("naranja","azul",.99);
  using(var client=new HttpClient(new FakeHandler(responseJson,HttpStatusCode.OK,true))){
   var result=await new DetectorAditamentos(client).Analyze(new byte[]{1},colored,local,a=>new byte[]{2});Assert(result.Resultado=="equipo_faltante","El flujo real del adaptador aplica la comparación después de recibir la respuesta");
  }
  using(var client=new HttpClient(new FakeHandler(colorData("naranja","naranja",.99),HttpStatusCode.OK,true))){
   var result=await new DetectorAditamentos(client).Analyze(new byte[]{1},colored,local,a=>null);Assert(result.Resultado=="revision_necesaria","El adaptador detecta una referencia faltante pese a una respuesta positiva");
  }
  Assert(DetectorAditamentos.ParseResult(colorData("naranja","naranja",.70),colored,.75,refIds,.65).Resultado=="equipo_detectado","Color naranja con confianza 0.70 se acepta con umbral de color 0.65");
  Assert(DetectorAditamentos.ParseResult(colorData("naranja","azul",.70),colored,.75,refIds,.65).Resultado=="equipo_faltante","Ampliar el umbral nunca permite azul como naranja");
  Assert(DetectorAditamentos.ParseResult(colorData("naranja","naranja",.60),colored,.75,refIds,.65).Resultado=="revision_necesaria","Color por debajo de 0.65 sigue requiriendo revisión");
  var trustedColors=new Dictionary<int,string>{{10,"naranja"}};
  Assert(DetectorAditamentos.ParseResult(colorData("azul","azul",.99),colored,.75,refIds,.65,trustedColors).Resultado=="equipo_faltante","La foto leída por separado manda sobre un color de referencia inventado en la captura");
  var actualOnly=DetectorAditamentos.BuildLocalRequest(new byte[]{1},colored,local,null,trustedColors);
  Assert(actualOnly["images"].Count()==1&&actualOnly["prompt"].ToString().Contains("color_foto_verificado"),"La detección local analiza una sola captura y recibe los colores de las referencias por separado");
  colored[0].ColorRequerido="rojo";
  Assert(DetectorAditamentos.ParseResult(colorData("naranja","rojo",.99),colored,.75,new HashSet<int>(),.65).Resultado=="equipo_detectado","Color manual rojo prevalece sobre nombre naranja y fotografía interpretada como naranja");
  var wrongManual=DetectorAditamentos.ParseResult(colorData("rojo","azul",.99),colored,.75,new HashSet<int>(),.65);
  Assert(wrongManual.Resultado=="equipo_faltante"&&wrongManual.Equipos[0].ColorEsperado=="rojo","El color manual rechaza azul y se muestra como color requerido");
  var photosRead=0;
  using(var client=new HttpClient(new FakeHandler(colorData("naranja","rojo",.99),HttpStatusCode.OK,true))){
   var result=await new DetectorAditamentos(client).Analyze(new byte[]{1},colored,local,a=>{photosRead++;return new byte[]{2};});
   Assert(result.Resultado=="equipo_detectado"&&photosRead==0,"Con color manual no se analiza ni exige fotografía de referencia");
  }
  colored[0].ColorRequerido=null;
  Assert(DetectorAditamentos.ParseResult(colorData("naranja","azul",.99),colored,.75,refIds,.65).Resultado=="equipo_faltante","Automático conserva la comparación anterior desde la foto");
  var malformed=JObject.Parse(Item(10,"detectado",.99));malformed["confianza_color"]=2;Rejected(Data(1,malformed.ToString()),"Confianza de color fuera de rango se rechaza");
  malformed["confianza_color"]=1;malformed["color_observado"]="inventado";Rejected(Data(1,malformed.ToString()),"Colores fuera del vocabulario se rechazan");
  Console.WriteLine("VisionRegression=PASS (servicios simulados, sin imágenes externas)");
 }
 class FakeHandler:HttpMessageHandler
 {
  readonly string json;readonly HttpStatusCode status;readonly bool local;readonly string reason;public bool Called;public bool HasAuthorization;
  public FakeHandler(string json,HttpStatusCode status=HttpStatusCode.OK,bool local=false,string reason="stop"){this.json=json;this.status=status;this.local=local;this.reason=reason;}
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Called=true;HasAuthorization=request.Headers.Authorization!=null;var envelope=new JObject{{"status","completed"},{"output",new JArray(new JObject{{"type","message"},{"content",new JArray(new JObject{{"type","output_text"},{"text",json}})}})}};if(local){var payload=JObject.Parse(await request.Content.ReadAsStringAsync());var reference=payload["system"]?.ToString().Contains("SOLO_REFERENCIA_COLOR")==true;envelope=new JObject{{"done",true},{"done_reason",reason},{"response",reference?"{\"color\":\"naranja\",\"confianza\":0.99}":json}};}return new HttpResponseMessage(status){Content=new StringContent(envelope.ToString())};}
 }
}

