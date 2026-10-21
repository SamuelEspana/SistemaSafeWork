param([string]$BaseUrl='http://localhost:5088')
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent;$tag='QA-MON-'+[Guid]::NewGuid().ToString('N');$ids=@{};$stub=$null;$iis=$null
$site=Join-Path $project ('artifacts/'+$tag);New-Item -ItemType Directory -Path $site|Out-Null
Copy-Item (Join-Path $project 'artifacts/Publish/*') $site -Recurse
[xml]$cfg=Get-Content (Join-Path $project 'SistemaSafeWork/Web.config')
$builder=[Data.SqlClient.SqlConnectionStringBuilder]::new($cfg.configuration.connectionStrings.add.connectionString);$server=[string]$builder['Data Source'];if(!$server.StartsWith('tcp:')){$builder['Data Source']='tcp:'+$server};$builder['Connect Timeout']=10
$cfg.configuration.connectionStrings.add.connectionString=$builder.ConnectionString
($cfg.configuration.appSettings.add|Where-Object key -eq 'Vision:Endpoint').value='http://127.0.0.1:11436/api/generate'
$cfg.Save((Join-Path $site 'Web.config'))
$connection=[Data.SqlClient.SqlConnection]::new($builder.ConnectionString);$connection.Open()
function Sql([string]$sql,[hashtable]$parameters=@{}){$cmd=$connection.CreateCommand();$cmd.CommandText=$sql;foreach($key in $parameters.Keys){[void]$cmd.Parameters.AddWithValue($key,$parameters[$key])};try{return $cmd.ExecuteScalar()}finally{$cmd.Dispose()}}
function Assert($value,[string]$message){if(!$value){throw $message};Write-Output ('PASS: '+$message)}
function GetPage([string]$path,$session){return Invoke-WebRequest ($BaseUrl+$path) -WebSession $session -SkipHttpErrorCheck}
function Post([string]$path,[hashtable]$body,$session,[string]$formPath='/Monitoreo'){$page=GetPage $formPath $session;$match=[regex]::Match($page.Content,'name="__RequestVerificationToken"[^>]*value="([^"]+)"');if(!$match.Success){throw 'Falta token.'};$body['__RequestVerificationToken']=$match.Groups[1].Value;return Invoke-WebRequest ($BaseUrl+$path) -Method Post -Body $body -WebSession $session -SkipHttpErrorCheck}
$password='QA!'+[Guid]::NewGuid().ToString('N');$salt=New-Object byte[] 16;[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($salt);$derive=[Security.Cryptography.Rfc2898DeriveBytes]::new($password,$salt,210000,[Security.Cryptography.HashAlgorithmName]::SHA256);$hash='210000.'+[Convert]::ToBase64String($salt)+'.'+[Convert]::ToBase64String($derive.GetBytes(32));$derive.Dispose()
try{
 $ids.admin=Sql "SELECT Id FROM Rol WHERE Nombre=N'Administrador'"
 $ids.user=Sql 'INSERT Usuario(Nombre,NombreUsuario,ContrasenaHash,RolId) VALUES(@tag,@tag,@hash,@rol);SELECT CAST(SCOPE_IDENTITY() AS int)' @{'@tag'=$tag;'@hash'=$hash;'@rol'=$ids.admin}
 $ids.role=Sql 'INSERT Rol(Nombre) VALUES(@tag);SELECT CAST(SCOPE_IDENTITY() AS int)' @{'@tag'=$tag}
 [void](Sql "INSERT RolPermiso(RolId,Permiso) VALUES(@rol,N'seguridad.monitoreo.ver')" @{'@rol'=$ids.role})
 $ids.reader=Sql 'INSERT Usuario(Nombre,NombreUsuario,ContrasenaHash,RolId) VALUES(@tag,@tag,@hash,@rol);SELECT CAST(SCOPE_IDENTITY() AS int)' @{'@tag'=($tag+'-lector');'@hash'=$hash;'@rol'=$ids.role}
 $stub=Start-Process -FilePath (Get-Command node).Source -ArgumentList ('"'+(Join-Path $PSScriptRoot 'MonitorStub.cjs')+'"') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $site 'stub.log') -RedirectStandardError (Join-Path $site 'stub-error.log')
 $iis=Start-Process -FilePath 'C:\Program Files\IIS Express\iisexpress.exe' -ArgumentList ('/path:"'+$site+'"'),'/port:5088' -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $site 'iis.log') -RedirectStandardError (Join-Path $site 'iis-error.log')
 $session=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
 for($i=0;$i -lt 30;$i++){try{$page=GetPage '/Cuenta/Login' $session;if($page.StatusCode -eq 200){break}}catch{};Start-Sleep -Milliseconds 500}
 Assert ((GetPage '/Monitoreo' $session).Content.Contains('Iniciar sesión')) 'Monitoreo exige autenticación'
 [void](Post '/Cuenta/Login' @{Usuario=$tag;Contrasena=$password} $session '/Cuenta/Login')
 Assert ((GetPage '/Monitoreo' $session).Content.Contains('monitoreo.js')) 'Monitoreo y menú disponibles para Administrador'
 $started=(Post '/Monitoreo/Iniciar' @{} $session).Content|ConvertFrom-Json;Assert $started.ok 'Se inicia una sesión vinculada al usuario'
 Add-Type -AssemblyName System.Drawing;$bitmap=[Drawing.Bitmap]::new(320,240);$graphics=[Drawing.Graphics]::FromImage($bitmap);$graphics.Clear([Drawing.Color]::White);$output=[IO.MemoryStream]::new();$bitmap.Save($output,[Drawing.Imaging.ImageFormat]::Jpeg);$capture='data:image/jpeg;base64,'+[Convert]::ToBase64String($output.ToArray());$bitmap.Dispose();$graphics.Dispose();$output.Dispose()
 for($n=1;$n -le 4;$n++){
  $sample=Post '/Monitoreo/Analizar' @{sesion=$started.sesion;secuencia=$n;captura=$capture} $session;$body=$sample.Content|ConvertFrom-Json
  Assert $body.ok ('Captura '+$n+' procesada con transporte simulado')
  if($n -eq 1){Assert ($body.eventos.Count -eq 1) 'Se guarda desde la primera captura sin casco, sin transición previa'}
  if($n -eq 2 -or $n -eq 3){Assert ($body.eventos.Count -eq 0) 'No se duplica una captura inmediatamente ni se registra con casco puesto'}
 }
 Assert ($body.eventos.Count -eq 1) 'Volver a quitarse el casco se registra desde la primera captura'
 $event=$body.eventos[0].id;$rows=Sql 'SELECT COUNT(*) FROM Monitoreo_Detalle WHERE UsuarioId=@user' @{'@user'=$ids.user};Assert ($rows -eq 2) 'Monitoreo_Detalle guarda el ingreso sin casco y el nuevo estado sin casco'
 $foto=GetPage ('/CapturasMonitoreo/Foto/'+$event) $session;Assert ($foto.StatusCode -eq 200 -and $foto.Headers['Content-Type'].Contains('image/jpeg')) 'La captura se sirve desde almacenamiento protegido'
 $history=GetPage '/CapturasMonitoreo' $session;Assert ($history.Content.Contains('UTC') -and $history.Content.Contains('Evento #'+$event)) 'Historial muestra captura con fecha y hora de Guatemala'
 Assert ((Post '/Monitoreo/Analizar' @{sesion=$started.sesion;secuencia=4;captura=$capture} $session).StatusCode -eq 409) 'Una secuencia repetida no duplica eventos'
 $next=(Post '/Monitoreo/Analizar' @{sesion=$started.sesion;secuencia=5;captura=$capture} $session).Content|ConvertFrom-Json;Assert ($next.ok -and $next.eventos.Count -eq 0) 'Seguir sin casco no vuelve a guardar la misma alerta'
 $reader=[Microsoft.PowerShell.Commands.WebRequestSession]::new();[void](Post '/Cuenta/Login' @{Usuario=($tag+'-lector');Contrasena=$password} $reader '/Cuenta/Login')
 Assert ((GetPage '/Monitoreo' $reader).StatusCode -eq 200) 'Permiso de consulta permite abrir Monitoreo'
 Assert ((Post '/Monitoreo/Iniciar' @{} $reader).StatusCode -eq 403) 'Ejecutar monitoreo tiene permiso independiente'
 Assert ((GetPage ('/CapturasMonitoreo/Foto/'+$event) $reader).StatusCode -eq 403) 'Una captura no es visible sin permiso de historial'
 [void](Sql "INSERT RolPermiso(RolId,Permiso) VALUES(@rol,N'seguridad.monitoreo.ejecutar')" @{'@rol'=$ids.role})
 Assert ((Post '/Monitoreo/Analizar' @{sesion=$started.sesion;secuencia=6;captura=$capture} $reader).StatusCode -eq 403) 'Otro usuario no puede analizar una sesión ajena'
 $noToken=Invoke-WebRequest ($BaseUrl+'/Monitoreo/Iniciar') -Method Post -Body @{} -WebSession $session -SkipHttpErrorCheck;Assert ($noToken.StatusCode -ge 400) 'Iniciar monitoreo conserva antifalsificación'
 [void](Post '/Monitoreo/Detener' @{sesion=$started.sesion} $session)
 Assert ((Post '/Monitoreo/Analizar' @{sesion=$started.sesion;secuencia=7;captura=$capture} $session).StatusCode -eq 409) 'Una sesión detenida no analiza ni registra eventos'
 Write-Output 'MonitorIntegration=PASS (sitio aislado, cámara sintética y motor simulado)'
}finally{
 if($iis){Stop-Process -Id $iis.Id -Force -ErrorAction SilentlyContinue};if($stub){Stop-Process -Id $stub.Id -Force -ErrorAction SilentlyContinue}
 if($ids.user){[void](Sql 'DELETE Monitoreo_Detalle WHERE UsuarioId=@user' @{'@user'=$ids.user});[void](Sql 'DELETE Usuario WHERE Id=@id' @{'@id'=$ids.user})}
 if($ids.reader){[void](Sql 'DELETE Usuario WHERE Id=@id' @{'@id'=$ids.reader})};if($ids.role){[void](Sql 'DELETE RolPermiso WHERE RolId=@id' @{'@id'=$ids.role});[void](Sql 'DELETE Rol WHERE Id=@id' @{'@id'=$ids.role})};$connection.Dispose()
}