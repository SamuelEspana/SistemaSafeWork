# Sistema SafeWork

Primera etapa en ASP.NET MVC 5 / .NET Framework 4.7.2, basada en la estructura de SistemaPlastiHogar.

## Funciones

- Login con usuario y contraseña, cierre de sesión y cambio de contraseña.
- Dashboard y menú según los permisos del rol.
- CRUD de personas: nombre, categoría, nacimiento, DPI de 13 dígitos, teléfono de exactamente 8 dígitos y fotografía opcional por archivo o cámara.
- CRUD de aditamentos: código único, nombre, descripción y fotografía opcional.
- CRUD de categorías de personal con detalle de uno o más aditamentos obligatorios.
- CRUD de usuarios y roles. Administrador tiene acceso total y su rol no se puede renombrar ni eliminar. Se protege la última cuenta administrativa activa y la propia cuenta frente a eliminación, desactivación o cambio de rol.
- Permisos por módulo y acción: `ver`, `crear`, `editar`, `eliminar`. Además `dashboard.ver` y `cuenta.contrasena`. Se comprueban en el servidor y en el menú; las fotografías requieren el permiso de consulta del módulo correspondiente.

Persona representa al personal que entrará al lugar de trabajo. `Categoria_Personal` determina qué aditamentos necesitará. La detección visual de equipo está integrada como servicio configurable en Seguridad → Validación Aditamentos. El registro de entradas corresponde a una etapa posterior.

## Configuración después de clonar

El repositorio no incluye credenciales, fotografías de personas, capturas, base de datos, paquetes restaurados ni los pesos del modelo. Cada compañero debe configurar su propia base de pruebas.

Antes de abrir la solución, copiar la configuración de ejemplo:

```powershell
Copy-Item SistemaSafeWork/Web.config.example SistemaSafeWork/Web.config
```

Editar la conexión `SafeWork` en el archivo copiado para su instancia SQL Server. El ejemplo usa autenticación integrada de Windows para SQL Server; el inicio de sesión web sigue usando exclusivamente la tabla `Usuario`. Si SQL requiere usuario y contraseña, configurarlos únicamente en el archivo local `Web.config`, que está excluido de Git.

Restaurar NuGet, ejecutar el inicializador indicado abajo y arrancar `Iniciar-Detector-Local.cmd` para descargar e iniciar el detector en ese equipo. No se requiere una clave de IA para el modo local. Las pruebas con fotografías reales requieren archivos propios; esas imágenes no se distribuyen en el repositorio.

## Base de datos

La conexión está en `SistemaSafeWork/Web.config`, bajo el nombre `SafeWork`. No se crea ni cambia el esquema automáticamente al abrir el sitio.

`tools/Initialize-Database.ps1` consulta `DB_ID` antes de crear `SistemaSafeWork`, aplica el esquema idempotente `Database/001_Initial.sql` y crea una cuenta administrativa solo si falta un administrador activo. No sobrescribe contraseñas existentes. La contraseña inicial generada se muestra una sola vez en la salida del script. Si ya existe una cuenta `admin` pero no un administrador activo, se detiene para evitar reemplazarla.

```powershell
./tools/Initialize-Database.ps1
```

La autenticación usa exclusivamente las cuentas de la tabla Usuario. Se rechazan identidades Windows/dominio. IIS debe tener autenticación anónima habilitada y autenticación Windows deshabilitada; esto también está definido en las propiedades de IIS Express del proyecto. Las cookies de autenticación, antifalsificación y sesión tienen nombres propios de SafeWork para evitar cruces entre aplicaciones en localhost. Un formulario de login vencido o con identidad anterior vuelve a pedir las credenciales sin ejecutar el acceso ni deshabilitar antifalsificación.

Las contraseñas de usuarios utilizan PBKDF2 SHA-256 con sal aleatoria y 210000 iteraciones. El login se bloquea durante 15 minutos después de 5 intentos fallidos. Las operaciones de escritura requieren token antifalsificación y las consultas usan parámetros SQL.

## Ejecutar

1. Abrir `SistemaSafeWork.sln` en Visual Studio con herramientas de desarrollo web y .NET Framework 4.7.2.
2. Restaurar paquetes NuGet si es necesario.
3. Verificar la conexión SQL en Web.config e inicializar la base si todavía no se instaló.
4. Ejecutar con IIS Express (F5). La configuración del proyecto usa `https://localhost:44379/`.
5. Entrar con `admin` y la contraseña inicial entregada durante la instalación. Cambiarla desde el menú de cuenta.

Primero registrar aditamentos, luego categorías, después personal.

## Publicación

Publicar en IIS con .NET CLR v4 e integración de ASP.NET. El perfil `Carpeta` genera los archivos en `artifacts/Publish`; se comprobó esa publicación, incluyendo Roslyn y la transformación Release. La transformación Release elimina el modo debug y exige cookies sobre HTTPS. El sitio publicado necesita HTTPS, también para `getUserMedia` en dispositivos remotos. La cámara solicita autorización al navegador, muestra una vista previa y se detiene al capturar, cerrar, guardar o abandonar la página. El usuario puede cargar un archivo si la cámara no está disponible.

Las imágenes se validan como JPG/PNG (máximo 5 MB, 6000 px por lado y 20 megapíxeles), se convierten a JPEG y se guardan en `App_Data/Fotos`; no son archivos públicos. El usuario del pool de IIS necesita permiso de escritura en esa carpeta. Respaldar esta carpeta junto con la base; conservarla al volver a publicar. No subir Web.config con credenciales a repositorios públicos. Para producción se recomienda una cuenta SQL propia con permisos limitados a esta base en lugar de `sa`, y un certificado SQL verificable para poder retirar `TrustServerCertificate=True`.

`Build/SafeWork.CodeDom.props` copia y empaqueta el compilador Roslyn requerido para las vistas sin depender de las tareas antiguas CodeTaskFactory/WMI del paquete. Los archivos de `bin/roslyn` se incluyen en la publicación.

## Verificación

Compilar también las vistas:

```powershell
msbuild SistemaSafeWork.sln /p:Configuration=Debug /p:MvcBuildViews=true
```

Prueba de integración contra una instancia local y su base configurada:

```powershell
./tools/Test-Integration.ps1 -AdminPassword '<contraseña de administrador>' -BaseUrl 'http://localhost:5087'
```

La prueba crea registros ficticios identificados por un GUID, prueba autenticación, vistas, CRUD, fotografías, maestro-detalle, teléfono, duplicados, restricciones de integridad, permisos y antifalsificación, y elimina exclusivamente sus propios registros al terminar. Requiere una base de pruebas y acceso SQL, y no debe usarse contra producción. No modifica la contraseña del administrador.

La interfaz inicial se revisó en navegador de escritorio. Para Validación Aditamentos se verificaron el HTML servido, el flujo JavaScript y el ciclo de cámara con dispositivos y transporte simulados. El navegador de pruebas no pudo iniciar en el entorno actual; quedan pendientes la revisión visual de esta nueva pantalla, la cámara física y las pruebas en dispositivo móvil real.



## Validación Aditamentos (Seguridad)

La pantalla `Seguridad → Validación Aditamentos` busca al personal por DPI exacto y consulta los aditamentos actuales de su categoría. La cámara intenta activarse al abrir la pantalla, solicitando permiso del navegador, y no graba audio. Se puede cambiar entre cámara frontal/trasera o cargar una imagen JPG/PNG. La captura se envía únicamente al pulsar **Capturar y detectar aditamentos**. Al capturar, abandonar la pantalla o cambiar de pestaña se detiene la cámara.

Cada aditamento dispone de `ZonaCuerpo`: Cabeza, Rostro, Pecho, Manos, Piernas, Pies, Cuerpo completo u Otra. Los registros anteriores se migran como **Sin definir**: editar cada uno y elegir la zona correcta antes de analizar. Describir características visibles y cargar una fotografía del equipo mejora el contexto del análisis. No se infieren certificaciones ni propiedades que no puedan verse. El detector revisa una única persona por imagen y que el objeto esté colocado en la zona requerida; un equipo en el fondo o sostenido en la mano no debería contar.

Permisos: `seguridad.validacion.ver` para pantalla/búsqueda y `seguridad.validacion.analizar` adicional para enviar capturas al motor de visión. Administrador mantiene acceso completo.

### Detector local listo para pruebas, sin cuentas ni claves

SafeWork usa Ollama local y `qwen2.5vl:3b`. El programa y modelo se instalan en `artifacts/local-vision` y el servicio escucha solo en `127.0.0.1:11435`; no se expone a la red. No requiere API key, suscripción ni pago por consulta. La descarga inicial requiere internet y varios GB de disco; los análisis posteriores se ejecutan en este equipo, con los recursos de CPU/GPU disponibles.

Después de reiniciar Windows, hacer doble clic en `Iniciar-Detector-Local.cmd` antes de usar SafeWork. El lanzador comprueba si ya está activo, lo inicia si es necesario y descarga el modelo únicamente si falta. Alternativa: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Start-LocalVision.ps1 -Install`. No modifica variables globales ni crea tareas de inicio automático.

| Configuración en Web.config | Valor actual |
| --- | --- |
| `Vision:Provider` | `Ollama` |
| `Vision:Endpoint` | `http://127.0.0.1:11435/api/generate` |
| `Vision:Model` | `qwen2.5vl:3b` |
| `Vision:ApiKey` | Vacío, no se utiliza en modo local |
| `Vision:MinimumConfidence` | `0.75` para presencia/ausencia del equipo |
| `Vision:MinimumColorConfidence` | `0.65` para comparación de color |
| `Vision:IncludeReferencePhotos` | `true`, para comparar el color con la foto de cada aditamento |

Se envía al motor local la captura y los nombres/descripciones/zonas del equipo requerido. No se envían DPI, nombre/categoría ni fotografía de identificación del personal. La aplicación no guarda capturas en disco. El servicio local se inicia con `OLLAMA_NO_CLOUD=1`. La configuración local solo admite direcciones loopback; no tiene fallback a un servicio de pago. En modo local, cada fotografía de referencia se analiza por separado para extraer el color del objeto. Se conserva en memoria por hasta 6 horas usando el hash de la foto, nombre del equipo, modelo y endpoint; cambiar la foto obliga a volver a leerla. Solo se cachea el color y su confianza, no la captura. Después se analiza una sola imagen de cámara con el catálogo y esos colores como datos. Así se evita que la foto de un casco aislado interfiera en el conteo de personas. En el adaptador OpenAI se conservan las fotos separadas y etiquetadas dentro de la petición.

Para publicar en otro servidor debe instalarse e iniciarse el motor y modelo en ese servidor: `localhost` siempre significa el servidor de la aplicación, aunque la cámara esté en un teléfono. Los binarios y pesos de Ollama no se incluyen en la publicación web; quedan en `artifacts/local-vision`. Esta instalación portátil no cambia programas instalados del usuario.

Cada aditamento permite definir **Color requerido** en Crear/Editar: negro, blanco, gris, rojo, naranja, amarillo, verde, azul, morado, rosado o marrón. El color manual tiene prioridad sobre la foto, el nombre y la descripción. Si se define, no se analiza ni exige foto para determinar el color requerido; se sigue comprobando el color del equipo observado con el umbral configurado. Por ejemplo, elegir rojo evita que una foto interpretada como naranja cambie el requisito del bote rojo. La opción **Automático (según la foto)** conserva el comportamiento anterior. La migración idempotente `003_ColorRequerido.sql` añade una columna nullable, sin cambiar la selección de los registros existentes.

En modo Automático, la validación compara el color predominante del objeto en la foto de referencia con el color del equipo colocado en la captura. El servidor contrasta ambos campos: un casco azul no cumple una referencia naranja aunque el modelo marque `detectado`. La pantalla muestra color requerido y observado. Baja confianza, referencia faltante/ilegible, multicolor ambiguo o discrepancia entre foto y color explícito del nombre/descripción requieren revisión si el modelo intenta confirmar el equipo. La comparación usa familias de color, no una igualdad de tonos RGB: naranja claro y oscuro siguen siendo naranja pese a reflejos o cambios de brillo. Los umbrales de presencia y color son independientes; ampliarlos no convierte azul en naranja. Para validar Cabeza basta ver cabeza y torso, sin exigir pies si la categoría no los requiere. Si no hay foto, se respeta un único color explícito en nombre/descripción; si tampoco se indica un color no se exige comparación de color. Usar una foto del aditamento aislado con fondo neutro, buena iluminación y sin filtros. Sombras, reflejos y balance de blancos pueden afectar al modelo; estas pruebas no sustituyen validación humana. Las capturas no se comparan como rostros ni se identifica a la persona por su imagen.

Resultado por aditamento: **Detectado**, **No detectado** o **No verificable**. Una zona fuera del encuadre, imagen borrosa, más de una persona, baja confianza u omisión del resultado produce un pendiente. Nunca se marca completo un resultado parcial ni una respuesta truncada. La confianza es una estimación del modelo, no una probabilidad calibrada ni una certificación. Hay que evaluar capturas reales con los equipos utilizados. El operador confirma el resultado; el módulo no acciona puertas ni verifica que el titular del DPI sea la persona de la imagen.

El análisis admite hasta 180 segundos (incluye carga inicial del modelo), evita concurrencia por usuario y deja 3 segundos entre análisis. El servicio local tiene un único trabajador para limitar memoria. Si está detenido o falta el modelo, devuelve un mensaje claro; no genera detecciones simuladas. La categoría admite hasta 40 aditamentos por captura, aunque con el modelo pequeño conviene comenzar con pocos equipos y descripciones visuales claras.

El adaptador anterior de OpenAI sigue disponible con `Vision:Provider=OpenAI`, endpoint HTTPS de Responses, modelo compatible y clave del servidor (`SAFEWORK_VISION_API_KEY` o `Vision:ApiKey`). No se usa en la configuración local actual.

### Pruebas

`tools/VisionRegression.cs` comprueba la interpretación común de resultados, zonas no visibles, baja confianza, varias personas, IDs desconocidos/duplicados, peticiones y respuestas de ambos proveedores con HTTP simulado, restricciones de endpoint local y respuestas incompletas. `tools/Test-Validation.ps1` comprueba DPI/categorías/zonas/permisos/antifalsificación con registros temporales y elimina exclusivamente esos registros. `tools/Test-ValidationFrontend.cjs` comprueba el flujo de cámara y peticiones obsoletas con navegador simulado. `tools/LocalVisionSmoke.cs` ejecuta el motor local real con una imagen sintética vacía y comprueba que no confirme persona ni equipo. `tools/ColorReferenceSmoke.cs` comprueba el modelo local con las fotos reales del casco naranja y verde y una captura vacía: lee ambos colores correctamente y evita contar las referencias como equipo portado. La comparación de azul contra naranja se verificó con respuestas simuladas. `tools/LightingSmoke.cs` comprueba el detector local con la captura adjunta de una persona con casco naranja: se reconocieron una persona y casco/color correctos después de separar las referencias. Es una comprobación de una imagen, no una medición de precisión para todas las condiciones de iluminación. Los dos cascos existentes se configuraron en Cabeza.

La instalación y prueba real del motor local no certifican la precisión de detección de equipo. La evaluación con cámara y fotografías reales de aditamentos debe hacerse antes de confiar en los resultados.

Referencias oficiales: [Ollama en Windows](https://docs.ollama.com/windows), [API local de generación e imágenes](https://docs.ollama.com/api/generate), [salidas estructuradas](https://docs.ollama.com/capabilities/structured-outputs), [modelo Qwen2.5-VL](https://ollama.com/library/qwen2.5vl).
### Monitoreo continuo de personas sin casco en la cabeza

Seguridad → **Monitoreo** y **Capturas de monitoreo**. La cámara se abre automáticamente y permanece activa entre análisis, hasta detener o salir de la pantalla. No requiere DPI, categoría, identificación facial, foto de referencia ni color determinado de casco. Solo utiliza el motor local.

La evaluación observa la cabeza de cada persona: `cabeza_visible` y `casco_en_cabeza` son booleanos. El servidor deriva `con_casco`, `sin_casco` o `no_verificable` de esos valores. Un casco sostenido en la mano, al lado del cuerpo o en una mesa no equivale a casco colocado; si lleva uno puesto y sostiene otro, sí cumple. El color no importa. `Monitoreo:MinimumConfidence=0.00` evita descartar por confianza las detecciones de esta etapa de pruebas, independientemente de los umbrales de Validación Aditamentos. El modelo puede equivocarse: el historial almacena detecciones que podrán revisarse posteriormente.

Una persona detectada **sin casco en la cabeza** genera captura desde la **primera observación**, incluso si entra ya sin casco. No se exige haber visto un casco previamente ni dos confirmaciones consecutivas. Las pistas se asocian por posición (IoU ≥ 0.35, distancia de centros ≤ 0.18), sin rasgos biométricos; sirven para limitar duplicados. Si la misma pista permanece sin casco, se registra como máximo una captura cada 15 segundos. Si vuelve a colocarse el casco y luego aparece sin él, se registra nuevamente desde la primera observación. Cruces, pausas de más de 20 segundos y pérdida de encuadre pueden iniciar otra pista y otra captura. Un evento pendiente conserva su id para reintentar el guardado sin duplicarlo.

La columna de estado corresponde a la **fotografía analizada**, visible junto al resultado, no al instante del video en vivo. Mientras llega un nuevo análisis se limpia el estado anterior para evitar confundirlo con lo que muestra la cámara. Hay una solicitud a la vez, con 2 segundos entre respuestas; no es video procesado cuadro a cuadro y puede perder cambios rápidos. Mantén la pantalla abierta, el equipo encendido y la cámara autorizada. Un video congelado no se reenvía como nueva observación. Una cabeza fuera del encuadre se informa como no verificable. La precisión se medirá en una pantalla futura; no se implementa esa revisión en este cambio.

Capturas de máximo 640 px por lado, JPEG calidad 90, sin guardar video ni escenas habituales con casco. `004_Monitoreo.sql` crea idempotentemente `Monitoreo_Detalle` con evento, sesión, pista temporal, operador, fecha UTC, fotografía y observación. Las imágenes permanecen en `App_Data/Monitoreo`, excluidas de Git y protegidas por permiso. El historial pagina 50 eventos ordenados por fecha; muestra hora de Guatemala (UTC−6). Los registros anteriores se conservan con sus observaciones originales. La fecha corresponde a la captura analizada, no al instante exacto del movimiento. No hay retención ni eliminación automática.

Permisos: `seguridad.monitoreo.ver`, `seguridad.monitoreo.ejecutar`, `seguridad.monitoreo.capturas.ver`. Administrador accede a todo. Sesiones ligadas al usuario, antifalsificación y rechazo de secuencias repetidas; detener cancela análisis pendientes. Las pistas están en memoria; el historial permanece en la BD.

Pruebas: `tools/MonitorRegression.cs` comprueba entrada sin casco, captura inmediata, deduplicación, captura periódica, casco puesto y ausencia/estado incierto. `tools/Test-MonitorFrontend.cjs` comprueba cámara continua, limpieza de estados antiguos, fotografía asociada, video congelado y respuestas tardías. `tools/Test-MonitorIntegration.ps1` usa un sitio aislado (5088), imágenes sintéticas y un motor simulado (11436), prueba guardado inmediato en BD, historial y permisos; limpia exclusivamente sus registros temporales. `tools/MonitorSmoke.cs` prueba el modelo real con una captura anterior con casco, el área de video de la captura adjunta del casco azul en la mano y una imagen vacía; no guarda estos eventos en BD. La prueba de área de cámara usa los píxeles del video de la captura adjunta, sin modificar el archivo original.

La preparación JPEG evita en las capturas comprobadas un fallo observado de GPU; existe un [reporte similar del modelo en Ollama](https://github.com/ollama/ollama/issues/18369). Las respuestas inválidas o incompletas nunca se convierten en eventos.
