const fs=require('fs');const vm=require('vm');const assert=require('assert');
class Element{
 constructor(){this.handlers={};this.dataset={};this.value='';this.hidden=false;this.disabled=false;this.textContent='';this.children=[];this.videoWidth=720;this.videoHeight=1280;}
 addEventListener(name,fn){(this.handlers[name]||=[]).push(fn);}
 emit(name){for(const fn of this.handlers[name]||[])fn({preventDefault(){}});}
 append(...items){this.children.push(...items);}appendChild(item){this.children.push(item);}replaceChildren(){this.children=[];}
 removeAttribute(){}querySelector(){return this.child||(this.child=new Element());}play(){return Promise.resolve();}
}
const nodes={};const get=id=>nodes[id]||(nodes[id]=new Element());
const app=get('validation-app');app.dataset={ready:'true',canAnalyze:'true',search:'/Buscar',analyze:'/Analizar'};app.querySelector=()=>({value:'token-test'});
const requests=[];const media=[];let stopped=0;let mediaCalls=0;
const document={hidden:false,handlers:{},getElementById:get,addEventListener:Element.prototype.addEventListener,createElement(name){const el=new Element();if(name==='canvas'){el.getContext=()=>({drawImage(){}});el.toDataURL=()=> 'data:image/jpeg;base64,TEST';}return el;}};
const window={handlers:{},addEventListener:Element.prototype.addEventListener};
const context={document,window,navigator:{mediaDevices:{getUserMedia(options){mediaCalls++;assert.strictEqual(options.audio,false);return new Promise(resolve=>media.push(resolve));}}},AbortController,URLSearchParams,URL,Image:class{},console,fetch(url,options){return new Promise(resolve=>requests.push({url,options,resolve}));}};
function resolveMedia(){media.shift()({getTracks(){return[{stop(){stopped++;}}];}});}
function response(body){return{ok:true,status:200,headers:{get(){return'application/json';}},json:async()=>body};}
const tick=()=>new Promise(resolve=>setImmediate(resolve));
(async()=>{
 vm.runInNewContext(fs.readFileSync('SistemaSafeWork/Scripts/validacion-aditamentos.js','utf8'),context);
 assert.strictEqual(mediaCalls,1);console.log('PASS: La cámara se solicita automáticamente al abrir la pantalla y sin audio');
 resolveMedia();await tick();assert.strictEqual(get('validation-camera-state').textContent,'Cámara activa');
 get('validation-dpi').value='9000000000001';get('validation-search-form').emit('submit');await tick();
 const search=requests.shift();assert.strictEqual(new URLSearchParams(search.options.body).get('dpi'),'9000000000001');
 search.resolve(response({ok:true,persona:{Nombre:'Personal de prueba',Categoria:'Categoría de prueba'},aditamentos:[{Id:1,Nombre:'Casco',ZonaCuerpo:'Cabeza'}],puedeDetectar:true}));await tick();
 assert.strictEqual(get('validation-person-name').textContent,'Personal de prueba');assert.strictEqual(get('validation-analyze').disabled,false);console.log('PASS: La búsqueda carga el personal y habilita el análisis solo con equipo y cámara disponibles');
 get('validation-analyze').emit('click');await tick();const analysis=requests.shift();
 assert.strictEqual(analysis.url,'/Analizar');assert.strictEqual(new URLSearchParams(analysis.options.body).get('captura'),'data:image/jpeg;base64,TEST');assert.strictEqual(stopped,1);console.log('PASS: Analizar captura una imagen y detiene el dispositivo de cámara');
 analysis.resolve(response({ok:true,persona:{Nombre:'Personal de prueba',Categoria:'Categoría de prueba'},resultado:{Resultado:'equipo_faltante',Mensaje:'Color incorrecto',Equipos:[{Nombre:'Casco naranja',ZonaCuerpo:'Cabeza',Estado:'no_detectado',ColorEsperado:'naranja',ColorObservado:'azul',Observacion:'El color no cumple.'}]}}));await tick();
 const row=get('validation-result-rows').children[0];assert.strictEqual(row.children[3].textContent,'naranja');assert.strictEqual(row.children[4].textContent,'azul');assert.strictEqual(row.children[2].children[0].textContent,'No detectado');console.log('PASS: El resultado muestra los colores requerido y observado y conserva el rechazo del equipo');
 get('validation-analyze').emit('click');await tick();const pendingAnalysis=requests.shift();
 get('validation-dpi').value='9000000000002';get('validation-dpi').emit('input');
 assert.strictEqual(get('validation-person').hidden,true);assert.strictEqual(get('validation-analyze').disabled,true);assert.strictEqual(pendingAnalysis.options.signal.aborted,true);
 pendingAnalysis.resolve(response({ok:true,persona:{Nombre:'Persona anterior',Categoria:'Anterior'},resultado:{Resultado:'equipo_detectado',Mensaje:'Anterior',Equipos:[]}}));await tick();
 assert.strictEqual(get('validation-results').hidden,true);assert.notStrictEqual(get('validation-person-name').textContent,'Persona anterior');console.log('PASS: Cambiar el DPI cancela e invalida resultados anteriores');
 get('validation-camera-retry').emit('click');resolveMedia();await tick();document.hidden=true;for(const fn of document.handlers.visibilitychange)fn();assert.strictEqual(stopped,2);console.log('PASS: La cámara se detiene cuando la página pasa a segundo plano');
 get('validation-camera-retry').emit('click');for(const fn of window.handlers.pagehide)fn();resolveMedia();await tick();assert.strictEqual(stopped,3);console.log('PASS: Una cámara abierta tardíamente se cierra si la página ya se abandonó');
 console.log('ValidationFrontend=PASS (DOM, cámara y transporte simulados)');
})().catch(error=>{console.error(error);process.exitCode=1;});
