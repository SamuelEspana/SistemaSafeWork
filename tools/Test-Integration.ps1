param([Parameter(Mandatory=$true)][string]$AdminPassword,[string]$BaseUrl='http://localhost:5087')
$ErrorActionPreference='Stop'
$session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
$tag='QA-'+[Guid]::NewGuid().ToString('N').Substring(0,10)
$ids=@{}
[xml]$cfg=Get-Content (Join-Path $PSScriptRoot '../SistemaSafeWork/Web.config')
$connection=New-Object System.Data.SqlClient.SqlConnection($cfg.configuration.connectionStrings.add.connectionString)
$connection.Open()
function Sql([string]$sql,[hashtable]$parameters=@{}){$cmd=$connection.CreateCommand();$cmd.CommandText=$sql;foreach($key in $parameters.Keys){[void]$cmd.Parameters.AddWithValue($key,$parameters[$key])};try{return $cmd.ExecuteScalar()}finally{$cmd.Dispose()}}
function Assert($condition,[string]$message){if(!$condition){throw $message};Write-Output ('PASS: '+$message)}
function GetPage([string]$path,$s=$session){return Invoke-WebRequest ($BaseUrl+$path) -WebSession $s -SkipHttpErrorCheck}
function Token($page){$match=[regex]::Match($page.Content,'name="__RequestVerificationToken"[^>]*value="([^"]+)"');if(!$match.Success){throw 'No se encontró token antifalsificación.'};return $match.Groups[1].Value}
function Post([string]$path,[hashtable]$body,$s=$session){$page=GetPage $path $s;$body['__RequestVerificationToken']=Token $page; $fields=@(); foreach($key in $body.Keys){foreach($value in @($body[$key])){$fields += [Uri]::EscapeDataString($key)+'='+[Uri]::EscapeDataString([string]$value)}}; return Invoke-WebRequest ($BaseUrl+$path) -Method Post -ContentType 'application/x-www-form-urlencoded' -Body ($fields -join '&') -WebSession $s -SkipHttpErrorCheck}
try {
 $anonymous=GetPage '/Personas'
 Assert ($anonymous.Content.Contains('Iniciar sesión')) 'Una ruta protegida exige login'
 $attempts=Sql 'SELECT Intentos FROM Usuario WHERE NombreUsuario=N''admin'''
 $invalidLogin=Invoke-WebRequest ($BaseUrl+'/Cuenta/Login') -Method Post -Body @{Usuario='admin';Contrasena='invalida'} -WebSession $session -SkipHttpErrorCheck
 Assert ($invalidLogin.StatusCode -eq 200 -and $invalidLogin.Content.Contains('formulario de acceso')) 'Login con token ausente se recupera sin error de servidor'
 Assert ((Sql 'SELECT Intentos FROM Usuario WHERE NombreUsuario=N''admin''') -eq $attempts) 'Token inválido se rechaza antes de validar credenciales'
 $session.Cookies.Add([Net.Cookie]::new('.ASPXAUTH','legacy-session','/',([Uri]$BaseUrl).Host))
 $login=Post '/Cuenta/Login' @{Usuario='admin';Contrasena=$AdminPassword}
 Assert ($login.Content.Contains('<h1>Dashboard</h1>')) 'Login de Administrador y dashboard'
 Assert ($null -ne $session.Cookies.GetCookies([Uri]$BaseUrl)['.SafeWork.Auth']) 'Login usa cookie propia de SafeWork'
 Assert ($null -ne $session.Cookies.GetCookies([Uri]$BaseUrl)['.SafeWork.AntiForgery']) 'Token antifalsificación usa cookie propia de SafeWork'
 foreach($path in @('/Personas','/Personas/Crear','/Aditamentos','/Aditamentos/Crear','/Categorias','/Categorias/Crear','/Usuarios','/Usuarios/Crear','/Roles','/Roles/Crear','/Cuenta/CambiarContrasena')){Assert ((GetPage $path).StatusCode -eq 200) ('Vista '+$path)}
 $missing=Invoke-WebRequest ($BaseUrl+'/Aditamentos/Crear') -Method Post -Body @{Codigo=$tag;Nombre=$tag} -WebSession $session -SkipHttpErrorCheck
 Assert ($missing.StatusCode -ge 400) 'POST sin token antifalsificación rechazado'
 $page=GetPage '/Aditamentos/Crear';$photo=Get-Item (Join-Path $PSScriptRoot '../SistemaSafeWork/Content/Images/safework.png')
 $result=Invoke-WebRequest ($BaseUrl+'/Aditamentos/Crear') -Method Post -Form @{__RequestVerificationToken=(Token $page);Codigo=$tag;Nombre=$tag;Descripcion='Prueba automatizada';foto=$photo} -WebSession $session -SkipHttpErrorCheck
 $ids.aditamento=Sql 'SELECT Id FROM Aditamento WHERE Codigo=@tag' @{'@tag'=$tag}
 Assert ($null -ne $ids.aditamento) 'Crear aditamento con fotografía'
 Assert ((GetPage ('/Fotos/Ver?tipo=aditamento&id='+$ids.aditamento)).Headers['Content-Type'][0].Contains('image/jpeg')) 'Fotografía normalizada y servida con permisos'
 $duplicate=Post '/Aditamentos/Crear' @{Codigo=$tag;Nombre=$tag}
 Assert ($duplicate.Content.Contains('Ya existe')) 'Código duplicado rechazado'
 $edit=Post ('/Aditamentos/Editar/'+$ids.aditamento) @{Codigo=$tag;Nombre=($tag+' editado');Descripcion='Equipo actualizado'}
 Assert ((Sql 'SELECT Nombre FROM Aditamento WHERE Id=@id' @{'@id'=$ids.aditamento}) -eq ($tag+' editado')) 'Editar aditamento'
 $category=Post '/Categorias/Crear' @{Nombre=$tag;Descripcion='Categoría QA';AditamentoIds=@($ids.aditamento)}
 $ids.categoria=Sql 'SELECT Id FROM Categoria_Personal WHERE Nombre=@tag' @{'@tag'=$tag}
 if($null -eq $ids.categoria){$category.Content|Set-Content (Join-Path $PSScriptRoot 'test-error.html') -Encoding utf8}; Assert ($null -ne $ids.categoria) 'Crear categoría con detalle de aditamentos'
 Assert ((Sql 'SELECT COUNT(*) FROM CategoriaAditamento WHERE CategoriaPersonalId=@id' @{'@id'=$ids.categoria}) -eq 1) 'Relación categoría-aditamento persistida'
 Assert ((GetPage ('/Categorias/Detalle/'+$ids.categoria)).Content.Contains($tag+' editado')) 'Consultar detalle de categoría con sus aditamentos'
 $editCategory=GetPage ('/Categorias/Editar/'+$ids.categoria)
 Assert ($editCategory.Content -match 'checked="checked"[^>]*name="AditamentoIds"|name="AditamentoIds"[^>]*checked="checked"') 'Detalle seleccionado al editar categoría'
 $dpi='900'+(Get-Date).Ticks.ToString().Substring(8,10)
 $invalid=Post '/Personas/Crear' @{Nombre=$tag;CategoriaPersonalId=$ids.categoria;FechaNacimiento='1995-06-15';DPI=$dpi;Telefono='123456789'}
 Assert ($invalid.Content.Contains('exactamente 8')) 'Teléfono de 9 dígitos rechazado en servidor'
 $person=Post '/Personas/Crear' @{Nombre=$tag;CategoriaPersonalId=$ids.categoria;FechaNacimiento='1995-06-15';DPI=$dpi;Telefono='12345678'}
 $ids.persona=Sql 'SELECT Id FROM Persona WHERE DPI=@dpi' @{'@dpi'=$dpi}
 Assert ($null -ne $ids.persona) 'Crear persona con categoría, DPI y teléfono'
 $storedPhoto=Sql 'SELECT Fotografia FROM Aditamento WHERE Id=@id' @{'@id'=$ids.aditamento};$captureBytes=[IO.File]::ReadAllBytes((Join-Path (Join-Path $PSScriptRoot '../SistemaSafeWork/App_Data/Fotos') $storedPhoto));$capture='data:image/jpeg;base64,'+[Convert]::ToBase64String($captureBytes)
 $editPerson=Post ('/Personas/Editar/'+$ids.persona) @{Nombre=($tag+' editado');CategoriaPersonalId=$ids.categoria;FechaNacimiento='1995-06-15';DPI=$dpi;Telefono='87654321';captura=$capture}
 Assert ((Sql 'SELECT Telefono FROM Persona WHERE Id=@id' @{'@id'=$ids.persona}) -eq '87654321') 'Editar persona'
 Assert ((GetPage ('/Fotos/Ver?tipo=persona&id='+$ids.persona)).Headers['Content-Type'][0].Contains('image/jpeg')) 'Guardar fotografía capturada desde base64 JPEG'
 $referenced=Post ('/Categorias/Eliminar/'+$ids.categoria) @{}
 Assert ([Net.WebUtility]::HtmlDecode($referenced.Content).Contains('está en uso')) 'Categoría en uso no se elimina'
 Assert ((Sql 'SELECT COUNT(*) FROM CategoriaAditamento WHERE CategoriaPersonalId=@id' @{'@id'=$ids.categoria}) -eq 1) 'El fallo de eliminación conserva el detalle'
 $rol=Post '/Roles/Crear' @{Nombre=$tag;Permisos=@('personas.ver','cuenta.contrasena')}
 $ids.rol=Sql 'SELECT Id FROM Rol WHERE Nombre=@tag' @{'@tag'=$tag}
 Assert ($null -ne $ids.rol) 'Crear rol con permisos específicos'
 $reserved=Post ('/Roles/Editar/'+$ids.rol) @{Nombre='Administrador';Permisos=@('personas.ver')}
 Assert ($reserved.Content.Contains('reservado')) 'Un rol no puede convertirse en Administrador'
 $testPassword='Prueba!'+[Guid]::NewGuid().ToString('N')
 $user=Post '/Usuarios/Crear' @{Nombre=$tag;NombreUsuario=$tag;Contrasena=$testPassword;RolId=$ids.rol;Activo='true'}
 $ids.usuario=Sql 'SELECT Id FROM Usuario WHERE NombreUsuario=@tag' @{'@tag'=$tag}
 Assert ($null -ne $ids.usuario) 'Crear usuario con rol'
 Assert (!(Sql 'SELECT ContrasenaHash FROM Usuario WHERE Id=@id' @{'@id'=$ids.usuario}).Contains($testPassword)) 'Contraseña almacenada con hash'
 $limited=New-Object Microsoft.PowerShell.Commands.WebRequestSession
 $limitedLogin=Post '/Cuenta/Login' @{Usuario=$tag;Contrasena=$testPassword} $limited
 Assert ($limitedLogin.Content.Contains('Bienvenido a SafeWork')) 'Usuario sin dashboard accede a inicio según permisos'
 Assert ((GetPage '/Personas' $limited).StatusCode -eq 200) 'Rol con personas.ver puede consultar personal'
 Assert ((GetPage '/Personas/Crear' $limited).StatusCode -eq 403) 'Permiso de crear validado en servidor'
 Assert ((GetPage '/Roles' $limited).StatusCode -eq 403) 'Acceso a roles restringido'
 Assert ((GetPage ('/Fotos/Ver?tipo=aditamento&id='+$ids.aditamento) $limited).StatusCode -eq 403) 'Fotografía protegida por el permiso del módulo'
 $csrfPerson=GetPage '/Personas' $limited
 $blocked=Invoke-WebRequest ($BaseUrl+'/Personas/Eliminar/'+$ids.persona) -Method Post -Body @{__RequestVerificationToken=(Token $csrfPerson)} -WebSession $limited -SkipHttpErrorCheck
 Assert ($blocked.StatusCode -eq 403) 'Eliminación denegada por rol aun con token válido'
 $adminId=Sql 'SELECT Id FROM Usuario WHERE NombreUsuario=N''admin'''; $self=Post ('/Usuarios/Eliminar/'+$adminId) @{}
 Assert ($self.Content.Contains('propia cuenta')) 'Administrador no puede eliminar su propia cuenta'
 $deletedPerson=Post ('/Personas/Eliminar/'+$ids.persona) @{}
 Assert ((Sql 'SELECT COUNT(*) FROM Persona WHERE Id=@id' @{'@id'=$ids.persona}) -eq 0) 'Eliminar persona'
 $deletedCategory=Post ('/Categorias/Eliminar/'+$ids.categoria) @{}
 Assert ((Sql 'SELECT COUNT(*) FROM Categoria_Personal WHERE Id=@id' @{'@id'=$ids.categoria}) -eq 0) 'Eliminar categoría y detalle'
 $deletedAditamento=Post ('/Aditamentos/Eliminar/'+$ids.aditamento) @{}
 Assert ((Sql 'SELECT COUNT(*) FROM Aditamento WHERE Id=@id' @{'@id'=$ids.aditamento}) -eq 0) 'Eliminar aditamento'
 $deletedUser=Post ('/Usuarios/Eliminar/'+$ids.usuario) @{}
 $deletedRole=Post ('/Roles/Eliminar/'+$ids.rol) @{}
 Assert ((Sql 'SELECT COUNT(*) FROM Usuario WHERE Id=@id' @{'@id'=$ids.usuario}) -eq 0) 'Eliminar usuario de prueba'
 Assert ((Sql 'SELECT COUNT(*) FROM Rol WHERE Id=@id' @{'@id'=$ids.rol}) -eq 0) 'Eliminar rol de prueba'
 Write-Output 'IntegrationResult=PASS'
} finally {
 foreach($entity in @('Persona','Aditamento')){$cmd=$connection.CreateCommand();$cmd.CommandText='SELECT Fotografia FROM '+$entity+' WHERE Nombre LIKE @tag';[void]$cmd.Parameters.AddWithValue('@tag',$tag+'%');$reader=$cmd.ExecuteReader();$files=@();while($reader.Read()){if(!$reader.IsDBNull(0)){$files+=$reader.GetString(0)}};$reader.Close();$cmd.Dispose();foreach($file in $files){if($file -eq [System.IO.Path]::GetFileName($file)){$photoPath=Join-Path (Join-Path $PSScriptRoot '../SistemaSafeWork/App_Data/Fotos') $file;if(Test-Path -LiteralPath $photoPath){Remove-Item -LiteralPath $photoPath}}}}
 foreach($pair in @(@('Persona','persona'),@('CategoriaAditamento','categoria'),@('Categoria_Personal','categoria'),@('Aditamento','aditamento'),@('Usuario','usuario'),@('RolPermiso','rol'),@('Rol','rol'))){if($ids.ContainsKey($pair[1]) -and $null -ne $ids[$pair[1]]){$column=if($pair[0] -eq 'CategoriaAditamento'){'CategoriaPersonalId'}elseif($pair[0] -eq 'RolPermiso'){'RolId'}else{'Id'};[void](Sql ('DELETE FROM '+$pair[0]+' WHERE '+$column+'=@id') @{'@id'=$ids[$pair[1]]})}}
 $connection.Dispose()
}







