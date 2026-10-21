param([string]$BaseUrl='http://localhost:5087')
$ErrorActionPreference='Stop'
$tag='QA-VALID-'+[Guid]::NewGuid().ToString('N').Substring(0,10);$ids=@{}
[xml]$cfg=Get-Content (Join-Path $PSScriptRoot '../SistemaSafeWork/Web.config')
$connection=New-Object System.Data.SqlClient.SqlConnection($cfg.configuration.connectionStrings.add.connectionString);$connection.Open()
function Sql([string]$sql,[hashtable]$parameters=@{}){$cmd=$connection.CreateCommand();$cmd.CommandText=$sql;foreach($key in $parameters.Keys){[void]$cmd.Parameters.AddWithValue($key,$parameters[$key])};try{return $cmd.ExecuteScalar()}finally{$cmd.Dispose()}}
function Assert($condition,[string]$message){if(!$condition){throw $message};Write-Output ('PASS: '+$message)}
function GetPage([string]$path,$session){return Invoke-WebRequest ($BaseUrl+$path) -WebSession $session -SkipHttpErrorCheck}
function Post([string]$path,[hashtable]$body,$session,[string]$formPath='/ValidacionAditamentos'){$page=GetPage $formPath $session;$match=[regex]::Match($page.Content,'name="__RequestVerificationToken"[^>]*value="([^"]+)"');if(!$match.Success){throw 'Falta token del formulario.'};$body['__RequestVerificationToken']=$match.Groups[1].Value;return Invoke-WebRequest ($BaseUrl+$path) -Method Post -Body $body -WebSession $session -SkipHttpErrorCheck}
$password='QA!'+[Guid]::NewGuid().ToString('N')
$salt=New-Object byte[] 16;$random=[Security.Cryptography.RandomNumberGenerator]::Create();$random.GetBytes($salt);$random.Dispose();$derive=[Security.Cryptography.Rfc2898DeriveBytes]::new($password,$salt,210000,[Security.Cryptography.HashAlgorithmName]::SHA256);$hash='210000.'+[Convert]::ToBase64String($salt)+'.'+[Convert]::ToBase64String($derive.GetBytes(32));$derive.Dispose()
try{
 $adminRole=Sql "SELECT Id FROM Rol WHERE Nombre=N'Administrador'"
 $ids.usuario=Sql 'INSERT Usuario(Nombre,NombreUsuario,ContrasenaHash,RolId) VALUES(@nombre,@usuario,@hash,@rol); SELECT CAST(SCOPE_IDENTITY() AS int)' @{'@nombre'=$tag;'@usuario'=$tag;'@hash'=$hash;'@rol'=$adminRole}
 $ids.rol=Sql 'INSERT Rol(Nombre) VALUES(@nombre);SELECT CAST(SCOPE_IDENTITY() AS int)' @{'@nombre'=$tag}
 [void](Sql "INSERT RolPermiso(RolId,Permiso) VALUES(@id,N'seguridad.validacion.ver')" @{'@id'=$ids.rol})
 $ids.limitado=Sql 'INSERT Usuario(Nombre,NombreUsuario,ContrasenaHash,RolId) VALUES(@nombre,@usuario,@hash,@rol); SELECT CAST(SCOPE_IDENTITY() AS int)' @{'@nombre'=($tag+' lector');'@usuario'=($tag+'-lector');'@hash'=$hash;'@rol'=$ids.rol}
 $ids.cabeza=Sql "INSERT Aditamento(Codigo,Nombre,ZonaCuerpo) VALUES(@codigo,N'Casco QA',N'Cabeza'); SELECT CAST(SCOPE_IDENTITY() AS int)" @{'@codigo'=($tag+'-C')}
 $ids.pies=Sql "INSERT Aditamento(Codigo,Nombre,ZonaCuerpo) VALUES(@codigo,N'Botas QA',N'Pies'); SELECT CAST(SCOPE_IDENTITY() AS int)" @{'@codigo'=($tag+'-P')}
 $ids.categoria=Sql 'INSERT Categoria_Personal(Nombre) VALUES(@nombre);SELECT CAST(SCOPE_IDENTITY() AS int)' @{'@nombre'=$tag}
 [void](Sql 'INSERT CategoriaAditamento(CategoriaPersonalId,AditamentoId) VALUES(@categoria,@cabeza),(@categoria,@pies)' @{'@categoria'=$ids.categoria;'@cabeza'=$ids.cabeza;'@pies'=$ids.pies})
 $dpi='900'+(Get-Date).Ticks.ToString().Substring(8,10)
 $ids.persona=Sql "INSERT Persona(Nombre,CategoriaPersonalId,FechaNacimiento,DPI,Telefono) VALUES(@nombre,@categoria,'1995-01-01',@dpi,'12345678'); SELECT CAST(SCOPE_IDENTITY() AS int)" @{'@nombre'=$tag;'@categoria'=$ids.categoria;'@dpi'=$dpi}
 $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
 Assert ((GetPage '/ValidacionAditamentos' $session).Content.Contains('Iniciar sesión')) 'La pantalla exige autenticación'
 $login=Post '/Cuenta/Login' @{Usuario=$tag;Contrasena=$password} $session '/Cuenta/Login'
 Assert ($login.Content.Contains('Dashboard')) 'Una cuenta de prueba inicia sesión sin cambiar admin'
 $page=GetPage '/ValidacionAditamentos' $session
 Assert ($page.StatusCode -eq 200 -and $page.Content.Contains('validacion-aditamentos.js')) 'Pantalla de validación y cámara disponible'
 Assert ($page.Content.Contains('SEGURIDAD')) 'Menú Seguridad disponible para Administrador'
 $lookup=Post '/ValidacionAditamentos/Buscar' @{dpi=$dpi} $session;$body=$lookup.Content|ConvertFrom-Json
 Assert ($body.ok -and $body.persona.Nombre -eq $tag) 'Búsqueda exacta de persona por DPI'
 Assert ($body.persona.Categoria -eq $tag -and $body.aditamentos.Count -eq 2) 'Solo se carga el equipo de la categoría asignada'
 Assert ($body.aditamentos.ZonaCuerpo -contains 'Cabeza' -and $body.aditamentos.ZonaCuerpo -contains 'Pies') 'Se distinguen las zonas corporales de los aditamentos'
 Assert ($body.puedeDetectar) 'Categoría configurada habilita la detección'
 $zoneEdit=Post ('/Aditamentos/Editar/'+$ids.pies) @{Codigo=($tag+'-P');Nombre='Botas QA';Descripcion='Ambos pies';ZonaCuerpo='Pies'} $session ('/Aditamentos/Editar/'+$ids.pies)
 Assert ($zoneEdit.StatusCode -eq 200 -and (Sql 'SELECT ZonaCuerpo FROM Aditamento WHERE Id=@id' @{'@id'=$ids.pies}) -eq 'Pies') 'El CRUD guarda la zona corporal desde el formulario'
 $colorEdit=Post ('/Aditamentos/Editar/'+$ids.pies) @{Codigo=($tag+'-P');Nombre='Botas QA';ZonaCuerpo='Pies';ColorRequerido='rojo'} $session ('/Aditamentos/Editar/'+$ids.pies)
 Assert ((Sql 'SELECT ColorRequerido FROM Aditamento WHERE Id=@id' @{'@id'=$ids.pies}) -eq 'rojo') 'El CRUD guarda el color elegido manualmente'
 $colorLookup=(Post '/ValidacionAditamentos/Buscar' @{dpi=$dpi} $session).Content|ConvertFrom-Json
 Assert (($colorLookup.aditamentos|Where-Object Id -eq $ids.pies).ColorRequerido -eq 'rojo') 'La validación carga el color manual del catálogo'
 $colorForm=GetPage ('/Aditamentos/Editar/'+$ids.pies) $session
 Assert ($colorForm.Content.Contains('name="ColorRequerido"') -and $colorForm.Content.Contains('value="rojo"')) 'El formulario permite editar el color requerido'
 $badColor=Post ('/Aditamentos/Editar/'+$ids.pies) @{Codigo=($tag+'-P');Nombre='Botas QA';ZonaCuerpo='Pies';ColorRequerido='inventado'} $session ('/Aditamentos/Editar/'+$ids.pies)
 Assert ([Net.WebUtility]::HtmlDecode($badColor.Content).Contains('color válido') -and (Sql 'SELECT ColorRequerido FROM Aditamento WHERE Id=@id' @{'@id'=$ids.pies}) -eq 'rojo') 'El servidor rechaza colores inválidos sin modificar el guardado'
 [void](Post '/Aditamentos/Crear' @{Codigo=($tag+'-COLOR');Nombre='Equipo color QA';ZonaCuerpo='Pecho';ColorRequerido='verde'} $session '/Aditamentos/Crear')
 $ids.color=Sql 'SELECT Id FROM Aditamento WHERE Codigo=@codigo' @{'@codigo'=($tag+'-COLOR')}
 Assert ($ids.color -and (Sql 'SELECT ColorRequerido FROM Aditamento WHERE Id=@id' @{'@id'=$ids.color}) -eq 'verde') 'Crear aditamento guarda el color elegido'
 [void](Post ('/Aditamentos/Editar/'+$ids.color) @{Codigo=($tag+'-COLOR');Nombre='Equipo color QA';ZonaCuerpo='Pecho';ColorRequerido=''} $session ('/Aditamentos/Editar/'+$ids.color))
 Assert ((Sql 'SELECT CASE WHEN ColorRequerido IS NULL THEN 1 ELSE 0 END FROM Aditamento WHERE Id=@id' @{'@id'=$ids.color}) -eq 1) 'Automático elimina la selección manual de color'
 $badZone=Post ('/Aditamentos/Editar/'+$ids.pies) @{Codigo=($tag+'-P');Nombre='Botas QA';ZonaCuerpo='ZonaInvalida'} $session ('/Aditamentos/Editar/'+$ids.pies)
 Assert ([Net.WebUtility]::HtmlDecode($badZone.Content).Contains('zona válida')) 'El CRUD rechaza una zona corporal desconocida'
 Assert (!$lookup.Content.Contains('Fotografia') -and !$lookup.Content.Contains($dpi)) 'La respuesta de búsqueda evita devolver fotografía personal y DPI'
 Assert ((Post '/ValidacionAditamentos/Buscar' @{dpi='123'} $session).StatusCode -eq 400) 'DPI inválido rechazado'
 $unknown='999'+[Guid]::NewGuid().ToString('N').GetHashCode().ToString().Replace('-','').PadLeft(10,'0').Substring(0,10)
 Assert ((Post '/ValidacionAditamentos/Buscar' @{dpi=$unknown} $session).StatusCode -eq 404) 'Persona inexistente devuelve no encontrada'
 [void](Sql "UPDATE Aditamento SET ZonaCuerpo=N'Sin definir' WHERE Id=@id" @{'@id'=$ids.pies})
 $unconfigured=(Post '/ValidacionAditamentos/Buscar' @{dpi=$dpi} $session).Content|ConvertFrom-Json
 Assert (!$unconfigured.puedeDetectar) 'Zona sin configurar mantiene la detección pendiente'
 Assert ((Post '/ValidacionAditamentos/Analizar' @{dpi=$dpi;captura='invalid'} $session).StatusCode -eq 400) 'El servidor bloquea análisis de equipo sin zona corporal'
 [void](Sql "UPDATE Aditamento SET ZonaCuerpo=N'Pies' WHERE Id=@id" @{'@id'=$ids.pies})
 if(($cfg.configuration.appSettings.add|Where-Object key -eq 'Vision:Provider').value -ne 'Ollama' -and [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable('SAFEWORK_VISION_API_KEY')) -and [string]::IsNullOrWhiteSpace(($cfg.configuration.appSettings.add|Where-Object key -eq 'Vision:ApiKey').value)){
  $disabled=Post '/ValidacionAditamentos/Analizar' @{dpi=$dpi;captura='invalid'} $session
  Assert ($disabled.StatusCode -eq 503 -and !($disabled.Content|ConvertFrom-Json).ok) 'Sin clave se informa servicio no configurado, sin simular detecciones'
 }
 if(($cfg.configuration.appSettings.add|Where-Object key -eq 'Vision:Provider').value -eq 'Ollama'){
  Assert ($page.Content.Contains('data-ready="true"') -and $page.Content.Contains('sin claves')) 'La detección local queda habilitada sin clave y explica procesamiento local'
  $invalid=Post '/ValidacionAditamentos/Analizar' @{dpi=$dpi;captura='invalid'} $session
  Assert ($invalid.StatusCode -eq 400) 'El modo local valida la captura antes de enviar al motor'
 }
 $noToken=Invoke-WebRequest ($BaseUrl+'/ValidacionAditamentos/Buscar') -Method Post -Body @{dpi=$dpi} -WebSession $session -SkipHttpErrorCheck
 Assert ($noToken.StatusCode -ge 400) 'La búsqueda por DPI conserva protección antifalsificación'
 $reader=New-Object Microsoft.PowerShell.Commands.WebRequestSession
 [void](Post '/Cuenta/Login' @{Usuario=($tag+'-lector');Contrasena=$password} $reader '/Cuenta/Login')
 Assert ((GetPage '/ValidacionAditamentos' $reader).StatusCode -eq 200) 'Un rol con permiso de consulta abre la pantalla'
 Assert ((Post '/ValidacionAditamentos/Analizar' @{dpi=$dpi;captura='invalid'} $reader).StatusCode -eq 403) 'La detección exige permiso independiente de analizar'
 [void](Sql "DELETE RolPermiso WHERE RolId=@id" @{'@id'=$ids.rol})
 Assert ((GetPage '/ValidacionAditamentos' $reader).StatusCode -eq 403) 'Revocar consulta impide acceso directo a la pantalla'
 Write-Output 'ValidationIntegration=PASS (sin llamadas reales al proveedor)'
}finally{
 foreach($pair in @(@('Persona','persona','Id'),@('CategoriaAditamento','categoria','CategoriaPersonalId'),@('Categoria_Personal','categoria','Id'),@('Aditamento','color','Id'),@('Aditamento','cabeza','Id'),@('Aditamento','pies','Id'),@('Usuario','usuario','Id'),@('Usuario','limitado','Id'),@('RolPermiso','rol','RolId'),@('Rol','rol','Id'))){if($ids.ContainsKey($pair[1])){[void](Sql ('DELETE '+$pair[0]+' WHERE '+$pair[2]+'=@id') @{'@id'=$ids[$pair[1]]})}};$connection.Dispose()
}

