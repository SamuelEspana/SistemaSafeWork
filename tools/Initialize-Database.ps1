param([string]$WebRoot = (Join-Path $PSScriptRoot '../SistemaSafeWork'))
$ErrorActionPreference='Stop'
[xml]$config=Get-Content (Join-Path $WebRoot 'Web.config')
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.configuration.connectionStrings.add.connectionString)
$builder['Data Source']='tcp:'+ $builder['Data Source']
$database=$builder['Initial Catalog']
if($database -ne 'SistemaSafeWork'){throw 'El script solo admite SistemaSafeWork.'}
$builder['Initial Catalog']='master'
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
try {
 $connection.Open()
 $cmd=$connection.CreateCommand()
 $cmd.CommandText="SELECT CASE WHEN DB_ID(N'SistemaSafeWork') IS NULL THEN 0 ELSE 1 END"
 $exists=$cmd.ExecuteScalar()
 Write-Output ('DatabaseExists='+$exists)
 if($exists -eq 0){$cmd.CommandText='CREATE DATABASE [SistemaSafeWork]';[void]$cmd.ExecuteNonQuery();Write-Output 'DatabaseCreated=True'}
} finally {$connection.Dispose()}
$builder['Initial Catalog']=$database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
try {
 $connection.Open()
 $cmd=$connection.CreateCommand(); $cmd.CommandText=(Get-ChildItem (Join-Path $WebRoot 'Database') -Filter '*.sql' | Sort-Object Name | ForEach-Object {Get-Content $_.FullName -Raw}) -join [Environment]::NewLine
 [void]$cmd.ExecuteNonQuery()
 $cmd.CommandText="SELECT COUNT(*) FROM dbo.Usuario u JOIN dbo.Rol r ON r.Id=u.RolId WHERE r.Nombre=N'Administrador' AND u.Activo=1"
 if($cmd.ExecuteScalar() -eq 0){
  $random=[System.Security.Cryptography.RandomNumberGenerator]::Create();$bytes=New-Object byte[] 18;$salt=New-Object byte[] 16;$random.GetBytes($bytes);$random.GetBytes($salt);$random.Dispose()
  $password='SW!'+[Convert]::ToBase64String($bytes)
  $derive=[System.Security.Cryptography.Rfc2898DeriveBytes]::new($password,$salt,210000,[System.Security.Cryptography.HashAlgorithmName]::SHA256)
  $hash='210000.'+[Convert]::ToBase64String($salt)+'.'+[Convert]::ToBase64String($derive.GetBytes(32));$derive.Dispose()
  $cmd.CommandText="IF NOT EXISTS(SELECT 1 FROM dbo.Usuario WHERE NombreUsuario=N'admin') INSERT dbo.Usuario(Nombre,NombreUsuario,ContrasenaHash,RolId) SELECT N'Administrador SafeWork',N'admin',@hash,Id FROM dbo.Rol WHERE Nombre=N'Administrador' ELSE THROW 50001,'La cuenta admin ya existe; no se modificó su contraseña.',1;"
  [void]$cmd.Parameters.AddWithValue('@hash',$hash);[void]$cmd.ExecuteNonQuery()
  Write-Output ('InitialAdminUser=admin');Write-Output ('InitialAdminPassword='+$password)
 }
 $cmd.Parameters.Clear();$cmd.CommandText="SELECT 'Personas' Entidad,COUNT(*) Total FROM dbo.Persona UNION ALL SELECT 'Aditamentos',COUNT(*) FROM dbo.Aditamento UNION ALL SELECT 'Categorias',COUNT(*) FROM dbo.Categoria_Personal UNION ALL SELECT 'Usuarios',COUNT(*) FROM dbo.Usuario"
 $adapter=New-Object System.Data.SqlClient.SqlDataAdapter($cmd);$table=New-Object System.Data.DataTable;[void]$adapter.Fill($table);$table|Format-Table -AutoSize
 Write-Output 'DatabaseInitialized=True'
} finally {$connection.Dispose()}


