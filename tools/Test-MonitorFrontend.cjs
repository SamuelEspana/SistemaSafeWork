const fs=require('fs'),vm=require('vm'),assert=require('assert');
class Element{constructor(){this.children=[];this.handlers={};this.dataset={};this.currentTime=1;this.videoWidth=640;this.videoHeight=480;this.textContent='';}addEventListener(n,f){(this.handlers[n]??=[]).push(f);}emit(n){for(const f of this.handlers[n]||[])f({preventDefault(){}});}appendChild(x){this.children.push(x);}prepend(x){this.children.unshift(x);}replaceChildren(){this.children=[];}removeChild(x){this.children.splice(this.children.indexOf(x),1);}get lastChild(){return this.children.at(-1);}querySelector(){return{value:'TOKEN'};}play(){return Promise.resolve();}}
const elements=new Map();function get(id){if(!elements.has(id))elements.set(id,new Element());return elements.get(id);}
get('monitor-app').dataset={start:'/Iniciar',stop:'/Detener',analyze:'/Analizar',ready:'true',canRun:'true'};
const requests=[],media=[],timers=[];let stops=0;
const document={handlers:{},getElementById:get,addEventListener:Element.prototype.addEventListener,createElement(name){const e=new Element();if(name==='canvas'){e.getContext=()=>({drawImage(){}});e.toDataURL=()=> 'data:image/jpeg;base64,TEST';}return e;}};
const window={handlers:{},addEventListener:Element.prototype.addEventListener};
const context={document,window,AbortController,URLSearchParams,Intl,Date,navigator:{mediaDevices:{getUserMedia(opts){assert.strictEqual(opts.audio,false);return new Promise(resolve=>media.push(resolve));}}},setTimeout(fn){timers.push(fn);return timers.length;},clearTimeout(){timers.length=0;},fetch(url,options){if(url==='/Detener')return Promise.resolve(response({ok:true}));return new Promise(resolve=>requests.push({url,options,resolve}));}};
function response(body){return{ok:true,headers:{get(){return'application/json';}},json:async()=>body};}
function camera(){const t={stop(){stops++;},addEventListener(){}};media.shift()({getTracks(){return[t];},getVideoTracks(){return[t];}});}
const tick=()=>new Promise(resolve=>setImmediate(resolve));
(async()=>{
 vm.runInNewContext(fs.readFileSync('SistemaSafeWork/Scripts/monitoreo.js','utf8'),context);assert.strictEqual(media.length,1);camera();await tick();
 const begin=requests.shift();assert.strictEqual(begin.url,'/Iniciar');begin.resolve(response({ok:true,sesion:'s1'}));await tick();
 const first=requests.shift();assert.strictEqual(first.url,'/Analizar');assert.strictEqual(stops,0);assert.strictEqual(timers.length,0);console.log('PASS: Cámara automática continua, sin audio y sin análisis superpuestos');
 first.resolve(response({ok:true,personas:[{Casco:'con_casco'}],eventos:[],fechaUtc:'2026-10-12T15:00:00Z'}));await tick();assert.strictEqual(stops,0);assert.strictEqual(timers.length,1);
 get('monitor-video').currentTime=2;timers.shift()();await tick();const second=requests.shift();assert.strictEqual(get('monitor-count').textContent,'Analizando nueva captura…');assert.strictEqual(get('monitor-people').children.length,0);assert.strictEqual(new URLSearchParams(second.options.body).get('secuencia'),'2');
 second.resolve(response({ok:true,personas:[{Casco:'sin_casco'}],eventos:[{id:7,fechaUtc:'2026-10-12T15:00:10Z'}],fechaUtc:'2026-10-12T15:00:10Z'}));await tick();assert.strictEqual(stops,0);assert.ok(get('monitor-events').children[0].textContent.includes('#7'));assert.strictEqual(get('monitor-analyzed-image').src,'data:image/jpeg;base64,TEST');assert.strictEqual(get('monitor-analyzed-image').hidden,false);console.log('PASS: El resultado muestra la fotografía analizada, guarda el evento y la cámara permanece activa');
 timers.shift()();await tick();assert.strictEqual(requests.length,0);console.log('PASS: Una imagen de video congelada no se vuelve a analizar');
 get('monitor-video').currentTime=3;timers.shift()();await tick();const pending=requests.shift();get('monitor-stop').emit('click');await tick();assert.strictEqual(pending.options.signal.aborted,true);assert.strictEqual(stops,1);assert.strictEqual(timers.length,0);
 pending.resolve(response({ok:true,personas:[],eventos:[{id:99,fechaUtc:'2026-10-12T15:00:20Z'}],fechaUtc:'2026-10-12T15:00:20Z'}));await tick();assert.ok(!get('monitor-events').children.some(x=>x.textContent.includes('#99')));console.log('PASS: Detener cierra cámara y descarta una respuesta tardía');
 get('monitor-start').emit('click');get('monitor-stop').emit('click');camera();await tick();assert.strictEqual(stops,2);console.log('PASS: Una cámara que abre tarde después de detener se cierra');
 console.log('MonitorFrontend=PASS (cámara y transporte simulados)');
})().catch(e=>{console.error(e);process.exitCode=1;});