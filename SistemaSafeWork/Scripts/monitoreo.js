(function(){
'use strict';
var app=document.getElementById('monitor-app');if(!app)return;
var video=document.getElementById('monitor-video'),status=document.getElementById('monitor-status'),count=document.getElementById('monitor-count'),people=document.getElementById('monitor-people'),events=document.getElementById('monitor-events'),last=document.getElementById('monitor-last');
var token=app.querySelector('input[name="__RequestVerificationToken"]').value;
var stream=null,session=null,timer=null,request=null,generation=0,sequence=0,running=false,disposed=false,facing='environment',reset=true,lastVideoTime=-1;
function body(data){data.__RequestVerificationToken=token;return new URLSearchParams(data);}
async function post(url,data,signal){var r=await fetch(url,{method:'POST',body:body(data),credentials:'same-origin',signal:signal});if(!(r.headers.get('content-type')||'').includes('application/json')){var expired=new Error('La sesión venció o no tienes permiso. Recarga la pantalla.');expired.fatal=true;throw expired;}var value=await r.json();if(!r.ok||!value.ok){var error=new Error(value.message||'No se pudo completar el monitoreo.');error.fatal=[401,403,409].includes(r.status);throw error;}return value;}
function endSession(id){if(!id)return;fetch(app.dataset.stop,{method:'POST',body:body({sesion:id}),credentials:'same-origin',keepalive:true}).catch(function(){});}
function stop(){running=false;generation++;clearTimeout(timer);if(request)request.abort();request=null;if(stream){stream.getTracks().forEach(function(t){t.stop();});stream=null;}video.srcObject=null;var old=session;session=null;endSession(old);reset=true;lastVideoTime=-1;status.textContent='Monitoreo detenido. Cámara cerrada.';}
async function start(){
 stop();if(disposed)return;if(app.dataset.ready!=='true'||app.dataset.canRun!=='true'){status.textContent='Revisa la configuración y el permiso para ejecutar monitoreo.';return;}
 var current=++generation;status.textContent='Solicitando cámara…';
 try{
  if(!navigator.mediaDevices||!navigator.mediaDevices.getUserMedia)throw new Error('La cámara requiere HTTPS o localhost y un navegador compatible.');
  var next=await navigator.mediaDevices.getUserMedia({video:{facingMode:{ideal:facing},width:{ideal:1280},height:{ideal:720}},audio:false});
  if(current!==generation||disposed){next.getTracks().forEach(function(t){t.stop();});return;}stream=next;video.srcObject=next;await video.play();
  next.getVideoTracks().forEach(function(track){track.addEventListener('ended',function(){if(current===generation){stop();status.textContent='La cámara se desconectó. Inicia el monitoreo nuevamente.';}});});
  var opened=await post(app.dataset.start,{});if(current!==generation||disposed){endSession(opened.sesion);return;}
  session=opened.sesion;running=true;sequence=0;reset=true;events.replaceChildren();sample(current);
 }catch(error){if(current===generation){stop();status.textContent=error.message;}}
}
function capture(){var scale=Math.min(1,640/Math.max(video.videoWidth,video.videoHeight)),canvas=document.createElement('canvas');canvas.width=Math.round(video.videoWidth*scale);canvas.height=Math.round(video.videoHeight*scale);canvas.getContext('2d').drawImage(video,0,0,canvas.width,canvas.height);return canvas.toDataURL('image/jpeg',0.9);}
function localTime(utc){return new Intl.DateTimeFormat('es-GT',{timeZone:'America/Guatemala',dateStyle:'short',timeStyle:'medium'}).format(new Date(utc));}
async function sample(current){
 if(!running||current!==generation)return;
 if(!video.videoWidth||video.currentTime<=lastVideoTime){reset=true;status.textContent='Esperando una imagen nueva de la cámara…';timer=setTimeout(function(){sample(current);},2000);return;}
 lastVideoTime=video.currentTime;var controller=new AbortController();request=controller;var mustReset=reset;reset=false;
 try{
  var snapshot=capture();people.replaceChildren();count.textContent='Analizando nueva captura…';status.textContent='Cámara activa · Analizando escena…';var data=await post(app.dataset.analyze,{sesion:session,secuencia:++sequence,captura:snapshot,reiniciar:mustReset},controller.signal);
  if(current!==generation||!running)return;
  var analyzed=document.getElementById('monitor-analyzed-image');analyzed.src=snapshot;analyzed.hidden=false;people.replaceChildren();count.textContent=data.personas.length+' persona(s) en la última captura';
  data.personas.forEach(function(p,index){var item=document.createElement('p');item.textContent='Persona temporal '+(index+1)+': '+({con_casco:'con casco',sin_casco:'sin casco',no_verificable:'cabeza o casco no verificables'}[p.Casco]||'no verificable');people.appendChild(item);});
  data.eventos.forEach(function(event){var item=document.createElement('p');item.className='notice';item.textContent='Captura guardada #'+event.id+' · '+localTime(event.fechaUtc)+' · Persona sin casco en la cabeza';events.prepend(item);while(events.children.length>20)events.removeChild(events.lastChild);});
  last.textContent='Captura analizada: '+localTime(data.fechaUtc);status.textContent='Cámara activa · Esperando la próxima captura. Los eventos guardados están en Capturas de monitoreo.';
 }catch(error){if(current===generation&&error.name!=='AbortError'){reset=true;people.replaceChildren();count.textContent='Seguimiento reiniciado';if(error.fatal){stop();status.textContent=error.message;}else status.textContent=error.message+' Se intentará otra captura.';}}
 finally{if(current===generation&&running){request=null;timer=setTimeout(function(){sample(current);},2000);}}
}
document.getElementById('monitor-start').addEventListener('click',start);
document.getElementById('monitor-stop').addEventListener('click',stop);
document.getElementById('monitor-switch').addEventListener('click',function(){facing=facing==='user'?'environment':'user';start();});
document.addEventListener('visibilitychange',function(){reset=true;});
window.addEventListener('pagehide',function(){disposed=true;var id=session;stop();if(id&&navigator.sendBeacon)navigator.sendBeacon(app.dataset.stop,body({sesion:id}));});
start();
})();
