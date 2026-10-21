# Motor privado de SafeWork: solo escucha en 127.0.0.1, no requiere API key.
param([switch]$Install)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$runtime=Join-Path $project 'artifacts/local-vision'
$exe=Join-Path $runtime 'ollama/ollama.exe'
[xml]$config=Get-Content -LiteralPath (Join-Path $project 'SistemaSafeWork/Web.config')
$model=[string]($config.configuration.appSettings.add|Where-Object key -eq 'Vision:Model').value
$endpoint=[uri][string]($config.configuration.appSettings.add|Where-Object key -eq 'Vision:Endpoint').value
if(!$endpoint.IsLoopback -or $endpoint.Scheme -ne 'http' -or $endpoint.AbsolutePath -ne '/api/generate'){throw 'El motor local debe usar HTTP loopback y /api/generate.'}
$base=$endpoint.GetLeftPart([UriPartial]::Authority)
if(!(Test-Path -LiteralPath $exe)){
 if(!$Install){throw 'Falta Ollama. Ejecuta este script con -Install para preparar el motor y modelo.'}
 New-Item -ItemType Directory -Force -Path $runtime | Out-Null
 $zip=Join-Path $runtime 'ollama-windows-amd64.zip'
 if(!(Test-Path -LiteralPath $zip)){
  Write-Host 'Descargando Ollama oficial (la primera vez requiere internet)...'
  Invoke-WebRequest 'https://github.com/ollama/ollama/releases/download/v0.40.1/ollama-windows-amd64.zip' -OutFile $zip -UseBasicParsing
 }
 Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $runtime 'ollama') -Force
}
$env:OLLAMA_HOST=$base
$env:OLLAMA_MODELS=Join-Path $runtime 'models'
$env:OLLAMA_NO_CLOUD='1'
$env:OLLAMA_MAX_LOADED_MODELS='1'
$env:OLLAMA_NUM_PARALLEL='1'
$env:OLLAMA_CONTEXT_LENGTH='8192'
function Tags { try { return Invoke-RestMethod ($base+'/api/tags') -TimeoutSec 3 } catch { return $null } }
$tags=Tags
if($null -eq $tags){
 Write-Host 'Iniciando motor local de SafeWork...'
 $proc=Start-Process -FilePath $exe -ArgumentList 'serve' -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime 'server-output.log') -RedirectStandardError (Join-Path $runtime 'server-error.log')
 $proc.Id | Set-Content -LiteralPath (Join-Path $runtime 'server.pid')
 for($i=0;$i -lt 30;$i++){Start-Sleep -Milliseconds 500;$tags=Tags;if($null -ne $tags){break};if($proc.HasExited){throw 'Ollama no pudo iniciar. Revisa artifacts/local-vision/server-error.log.'}}
 if($null -eq $tags){throw 'El motor local no respondió.'}
}
if(!($tags.models | Where-Object name -eq $model)){
 if(!$Install){throw 'Falta descargar el modelo. Ejecuta este script con -Install.'}
 Write-Host ('Descargando modelo '+$model+'; no requiere cuenta ni clave...')
 & $exe pull $model
 if($LASTEXITCODE -ne 0){throw 'No se completó la descarga del modelo.'}
}
$detail=Invoke-RestMethod ($base+'/api/show') -Method Post -ContentType 'application/json' -Body (@{model=$model}|ConvertTo-Json) -TimeoutSec 10
if($detail.capabilities -notcontains 'vision'){throw 'El modelo seleccionado no admite imágenes.'}
Write-Host ('SafeWork: visión local lista en '+$base+' con '+$model+'. No requiere API key.')