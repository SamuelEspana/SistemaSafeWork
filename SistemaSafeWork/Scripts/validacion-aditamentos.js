(function(){
'use strict';
var app=document.getElementById('validation-app');if(!app)return;
var dpi=document.getElementById('validation-dpi'),form=document.getElementById('validation-search-form'),person=document.getElementById('validation-person'),name=document.getElementById('validation-person-name'),category=document.getElementById('validation-person-category'),equipment=document.getElementById('validation-equipment'),searchMessage=document.getElementById('validation-search-message'),video=document.getElementById('validation-video'),image=document.getElementById('validation-image'),placeholder=document.getElementById('validation-camera-placeholder'),cameraState=document.getElementById('validation-camera-state'),cameraMessage=document.getElementById('validation-camera-message'),upload=document.getElementById('validation-upload'),analyze=document.getElementById('validation-analyze'),status=document.getElementById('validation-status'),results=document.getElementById('validation-results');
var stream=null,snapshot=null,selectedDpi=null,eligible=false,busy=false,request=null,revision=0,cameraGeneration=0,facing='environment',disposed=false;
var token=app.querySelector('input[name="__RequestVerificationToken"]').value;
function update(){analyze.disabled=busy||app.dataset.ready!=='true'||app.dataset.canAnalyze!=='true'||!selectedDpi||!eligible||(!snapshot&&(!stream||!video.videoWidth));analyze.textContent=busy?'Analizando equipo…':'Capturar y detectar aditamentos';}
function invalidate(){revision++;if(request){request.abort();request=null;}busy=false;results.hidden=true;status.textContent='';update();}
function stopCamera(){cameraGeneration++;if(stream){stream.getTracks().forEach(function(t){t.stop();});stream=null;}video.srcObject=null;cameraState.textContent='Cámara cerrada';update();}
function clearSnapshot(){snapshot=null;image.hidden=true;image.removeAttribute('src');video.hidden=false;upload.value='';}
async function startCamera(){
 invalidate();stopCamera();clearSnapshot();placeholder.hidden=false;placeholder.querySelector('p').textContent='Esperando acceso a la cámara';cameraState.textContent='Conectando…';
 if(!navigator.mediaDevices||!navigator.mediaDevices.getUserMedia){cameraState.textContent='No disponible';cameraMessage.textContent='La cámara necesita HTTPS o localhost y un navegador compatible. Puedes cargar una fotografía.';return;}
 var generation=++cameraGeneration;
 try{var next=await navigator.mediaDevices.getUserMedia({video:{facingMode:{ideal:facing},width:{ideal:1280},height:{ideal:1920}},audio:false});if(disposed||generation!==cameraGeneration){next.getTracks().forEach(function(t){t.stop();});return;}stream=next;video.srcObject=next;await video.play();placeholder.hidden=true;cameraState.textContent='Cámara activa';cameraMessage.textContent='Posiciona a una sola persona y asegúrate de que se vean las zonas requeridas.';update();}
 catch(error){if(generation!==cameraGeneration)return;stopCamera();cameraState.textContent='Sin acceso';cameraMessage.textContent='No se pudo abrir la cámara. Autoriza su uso en el navegador, revisa si está ocupada o carga una fotografía.';}
}
async function post(url,data,signal){
 data.__RequestVerificationToken=token;
 var response=await fetch(url,{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded;charset=UTF-8'},body:new URLSearchParams(data).toString(),credentials:'same-origin',signal:signal});
 if(!(response.headers.get('content-type')||'').includes('application/json'))throw new Error(response.status===403?'Tu rol no tiene permiso para esta acción.':'La sesión o el formulario venció. Recarga la pantalla e ingresa nuevamente.');
 var body=await response.json();if(!response.ok||!body.ok)throw new Error(body.message||'No se pudo completar la operación.');return body;
}
function showEquipment(items){equipment.replaceChildren();items.forEach(function(item){var card=document.createElement('div');card.className='validation-equipment-item';var title=document.createElement('strong');title.textContent=item.Nombre;var zone=document.createElement('small');zone.textContent=item.ZonaCuerpo+(item.ColorRequerido?" · Color requerido: "+item.ColorRequerido:"");card.append(title,zone);equipment.appendChild(card);});}
dpi.addEventListener('input',function(){invalidate();selectedDpi=null;eligible=false;person.hidden=true;searchMessage.textContent='Ingresa el DPI y pulsa Buscar para cargar el personal.';update();});
form.addEventListener('submit',async function(event){
 event.preventDefault();invalidate();selectedDpi=null;eligible=false;person.hidden=true;var value=dpi.value;var current=revision;
 if(!/^[0-9]{13}$/.test(value)){searchMessage.textContent='Ingresa un DPI de exactamente 13 dígitos.';return;}
 searchMessage.textContent='Buscando personal…';var controller=new AbortController();request=controller;
 try{var body=await post(app.dataset.search,{dpi:value},controller.signal);if(current!==revision||dpi.value!==value)return;selectedDpi=value;eligible=body.puedeDetectar;name.textContent=body.persona.Nombre;category.textContent=body.persona.Categoria;showEquipment(body.aditamentos);person.hidden=false;searchMessage.textContent=body.aditamentos.length===0?'La categoría no tiene aditamentos requeridos.':!eligible?'Configura las zonas de todos los aditamentos (máximo 40 por categoría) antes de detectar.':'Personal encontrado. Revisa el encuadre y detecta los aditamentos.';}
 catch(error){if(current===revision&&error.name!=='AbortError')searchMessage.textContent=error.message;}
 finally{if(current===revision){request=null;update();}}
});
function jpeg(source,width,height){var scale=Math.min(1,1600/Math.max(width,height));var canvas=document.createElement('canvas');canvas.width=Math.max(1,Math.round(width*scale));canvas.height=Math.max(1,Math.round(height*scale));canvas.getContext('2d').drawImage(source,0,0,canvas.width,canvas.height);return canvas.toDataURL('image/jpeg',0.92);}
upload.addEventListener('change',function(){
 var file=upload.files[0];if(!file)return;invalidate();stopCamera();snapshot=null;update();var current=revision;
 if(file.size>5*1024*1024||!['image/jpeg','image/png'].includes(file.type)){cameraMessage.textContent='Selecciona una imagen JPG o PNG de hasta 5 MB.';upload.value='';return;}
 var url=URL.createObjectURL(file),picture=new Image();picture.onload=function(){URL.revokeObjectURL(url);if(current!==revision)return;if(picture.width>6000||picture.height>6000||picture.width*picture.height>20000000){cameraMessage.textContent='La imagen debe tener máximo 6000 px por lado y 20 megapíxeles.';upload.value='';return;}snapshot=jpeg(picture,picture.width,picture.height);image.src=snapshot;image.hidden=false;video.hidden=true;placeholder.hidden=true;cameraState.textContent='Fotografía seleccionada';cameraMessage.textContent='Se analizará esta fotografía cuando pulses detectar.';update();};picture.onerror=function(){URL.revokeObjectURL(url);if(current===revision){cameraMessage.textContent='No se pudo leer la imagen.';upload.value='';}};picture.src=url;
});
function render(result){
 document.getElementById('validation-summary').textContent={equipo_detectado:'Equipo requerido detectado',equipo_faltante:'Equipo no detectado',revision_necesaria:'Revisión necesaria'}[result.Resultado]||'Revisión necesaria';document.getElementById('validation-result-message').textContent=result.Mensaje;
 var rows=document.getElementById('validation-result-rows');rows.replaceChildren();result.Equipos.forEach(function(item){var row=document.createElement('tr');[item.Nombre,item.ZonaCuerpo].forEach(function(text){var cell=document.createElement('td');cell.textContent=text;row.appendChild(cell);});var stateCell=document.createElement('td'),badge=document.createElement('span');badge.className='validation-state '+item.Estado;badge.textContent={detectado:'Detectado',no_detectado:'No detectado',no_verificable:'No verificable'}[item.Estado]||'No verificable';stateCell.appendChild(badge);row.appendChild(stateCell);[item.ColorEsperado||"Sin requisito",item.ColorObservado||"no_verificable"].forEach(function(color){var cell=document.createElement("td");cell.textContent=color==="no_verificable"?"No verificable":color;row.appendChild(cell);});var note=document.createElement('td');note.textContent=item.Observacion;row.appendChild(note);rows.appendChild(row);});results.hidden=false;
}
analyze.addEventListener('click',async function(){
 if(analyze.disabled)return;invalidate();var current=revision,value=selectedDpi;
 if(!snapshot){if(!stream||!video.videoWidth){status.textContent='Activa la cámara o carga una imagen.';return;}snapshot=jpeg(video,video.videoWidth,video.videoHeight);image.src=snapshot;image.hidden=false;video.hidden=true;stopCamera();cameraState.textContent='Captura para detectar';}
 busy=true;update();status.textContent='Analizando el equipo en las zonas requeridas…';var controller=new AbortController();request=controller;
 try{var body=await post(app.dataset.analyze,{dpi:value,captura:snapshot},controller.signal);if(current!==revision||dpi.value!==value)return;name.textContent=body.persona.Nombre;category.textContent=body.persona.Categoria;render(body.resultado);status.textContent='Detección finalizada. Para una captura nueva, activa la cámara nuevamente.';}
 catch(error){if(current===revision&&error.name!=='AbortError'){results.hidden=true;status.textContent=error.message;}}
 finally{if(current===revision){busy=false;request=null;update();}}
});
document.getElementById('validation-camera-retry').addEventListener('click',startCamera);
document.getElementById('validation-camera-switch').addEventListener('click',function(){facing=facing==='user'?'environment':'user';startCamera();});
document.getElementById('validation-camera-stop').addEventListener('click',function(){invalidate();stopCamera();cameraMessage.textContent='Cámara cerrada. Puedes activarla nuevamente o cargar una fotografía.';});
video.addEventListener('loadeddata',update);
window.addEventListener('pagehide',function(){disposed=true;invalidate();stopCamera();});document.addEventListener('visibilitychange',function(){if(document.hidden&&stream){invalidate();stopCamera();cameraMessage.textContent='La cámara se cerró al salir de la pantalla. Actívala para continuar.';}});
startCamera();
})();
