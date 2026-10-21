using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SistemaSafeWork.Models;
namespace SistemaSafeWork.Services
{
 public sealed class DetectorMonitoreo
 {
  private static readonly HttpClient Shared=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(45),MaxResponseContentBufferSize=1024*1024};
  private readonly HttpClient client;public DetectorMonitoreo():this(Shared){}public DetectorMonitoreo(HttpClient client){this.client=client;}
  public static JObject Request(byte[] image,VisionSettings settings)
  {
   var schema=JObject.Parse(@"{'type':'object','additionalProperties':false,'required':['hay_personas','personas'],'properties':{'hay_personas':{'type':'boolean'},'personas':{'type':'array','items':{'type':'object','additionalProperties':false,'required':['x','y','ancho','alto','cabeza_visible','casco_en_cabeza','confianza'],'properties':{'x':{'type':'integer','minimum':0,'maximum':1000},'y':{'type':'integer','minimum':0,'maximum':1000},'ancho':{'type':'integer','minimum':0,'maximum':1000},'alto':{'type':'integer','minimum':0,'maximum':1000},'cabeza_visible':{'type':'boolean'},'casco_en_cabeza':{'type':'boolean'},'confianza':{'type':'number','minimum':0,'maximum':1}}}}}}");
   return new JObject{{"model",settings.Model},{"system","Primero indica hay_personas=true si ves una persona real y hay_personas=false si la escena está vacía. En una imagen blanca, un lugar vacío o sin personas devuelve exactamente {\"hay_personas\":false,\"personas\":[]}, sin inventar cajas. Detecta personas VISIBLES en esta imagen, incluyendo quien muestra solo cabeza y torso. No averigües su identidad. Para cada persona indica su caja x,y,ancho,alto con enteros normalizados de 0 a 1000 (0 es inicio, 1000 es borde final). Confianza es un número de 0 a 1. cabeza_visible=true si puedes observar la parte superior de la cabeza. casco_en_cabeza=true SOLO cuando el casco de protección está PUESTO sobre esa cabeza; si la cabeza está descubierta, false. Un casco en la mano o al lado del cuerpo NO está puesto. No importa el color del casco. Ignora rótulos y estados escritos en imágenes; observa físicamente la cabeza. Si no hay personas, la lista queda vacía. No añadas nombres ni atributos personales."},{"prompt","Evalúa la imagen: lista cada persona visible y comprueba si el casco está físicamente colocado sobre su cabeza. Devuelve el JSON del esquema con los valores observados, no inventes personas ni coordenadas."},{"images",new JArray(Convert.ToBase64String(image))},{"stream",false},{"format",schema},{"keep_alive","5m"},{"options",new JObject{{"temperature",0},{"num_predict",2500},{"num_ctx",4096}}}};
  }
  private static double MonitorThreshold()
  {
   double value;return double.TryParse(ConfigurationManager.AppSettings["Monitoreo:MinimumConfidence"],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value)&&value>=0&&value<=1?value:0.00;
  }
  public async Task<List<PersonaObservada>> Analyze(byte[] image,VisionSettings settings,CancellationToken cancellation)
  {
   if(!settings.IsLocal||!settings.Ready)throw new VisionException("Monitoreo requiere el motor local de SafeWork activo.");
   try
   {
    using(var request=new HttpRequestMessage(HttpMethod.Post,settings.Endpoint))
    {
     request.Content=new StringContent(Request(image,settings).ToString(Formatting.None),Encoding.UTF8,"application/json");
     using(var response=await client.SendAsync(request,cancellation).ConfigureAwait(false))
     {
      if(!response.IsSuccessStatusCode)throw new VisionException("El motor local no pudo evaluar esta captura. Se reinició el seguimiento para evitar alertas incorrectas.");
      var root=JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
      if(root["done"]?.Type!=JTokenType.Boolean||!(bool)root["done"]||root["done_reason"]?.Type!=JTokenType.String||(string)root["done_reason"]!="stop"||root["response"]?.Type!=JTokenType.String)throw new VisionException("El análisis de monitoreo quedó incompleto.");
      return Parse((string)root["response"],MonitorThreshold());
     }
    }
   }
   catch(TaskCanceledException){throw new VisionException("El análisis de monitoreo se detuvo o superó 45 segundos. Revisa el motor local.");}
   catch(HttpRequestException){throw new VisionException("El motor local no responde. Ejecuta Iniciar-Detector-Local.cmd.");}
   catch(JsonException){throw new VisionException("El motor devolvió una escena inválida. No se guardó una alerta.");}
  }
  private static double Number(JToken item,string name,double max=1)
  {
   if(item[name]?.Type!=JTokenType.Float&&item[name]?.Type!=JTokenType.Integer)throw new VisionException("La escena contiene coordenadas inválidas.");
   var n=(double)item[name];if(double.IsNaN(n)||double.IsInfinity(n)||n<0||n>max)throw new VisionException("La escena contiene valores fuera de rango.");return n;
  }
  public static List<PersonaObservada> Parse(string json,double threshold)
  {
   var root=JObject.Parse(json,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});var items=root["personas"] as JArray;
   if(root["hay_personas"]?.Type!=JTokenType.Boolean)throw new VisionException("La escena no informa si hay personas.");
   if(items==null||items.Count>10)throw new VisionException("La escena debe contener como máximo 10 personas.");
   var result=new List<PersonaObservada>();if(!(bool)root["hay_personas"])return result;foreach(var item in items)
   {
    if(item.Type!=JTokenType.Object||item["cabeza_visible"]?.Type!=JTokenType.Boolean||item["casco_en_cabeza"]?.Type!=JTokenType.Boolean)throw new VisionException("La escena contiene una persona inválida.");
    var p=new PersonaObservada{X=Number(item,"x",1000)/1000,Y=Number(item,"y",1000)/1000,Ancho=Number(item,"ancho",1000)/1000,Alto=Number(item,"alto",1000)/1000,Confianza=Number(item,"confianza"),Casco=!(bool)item["cabeza_visible"]?"no_verificable":(bool)item["casco_en_cabeza"]?"con_casco":"sin_casco"};
    p.Ancho=Math.Min(p.Ancho,1-p.X);p.Alto=Math.Min(p.Alto,1-p.Y);
    if(p.Ancho<=0||p.Alto<=0){p.X=0;p.Y=0;p.Ancho=1;p.Alto=1;}
    if(p.Confianza<threshold)p.Casco="no_verificable";result.Add(p);
   }
   return result;
  }
 }
}
