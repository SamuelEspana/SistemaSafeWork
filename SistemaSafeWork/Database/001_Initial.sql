SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.Rol','U') IS NULL
CREATE TABLE dbo.Rol (Id int IDENTITY PRIMARY KEY, Nombre nvarchar(60) NOT NULL UNIQUE);
IF OBJECT_ID('dbo.RolPermiso','U') IS NULL
CREATE TABLE dbo.RolPermiso (RolId int NOT NULL REFERENCES dbo.Rol(Id), Permiso nvarchar(80) NOT NULL, PRIMARY KEY(RolId,Permiso));
IF OBJECT_ID('dbo.Usuario','U') IS NULL
CREATE TABLE dbo.Usuario (Id int IDENTITY PRIMARY KEY, Nombre nvarchar(120) NOT NULL, NombreUsuario nvarchar(60) NOT NULL UNIQUE, ContrasenaHash nvarchar(250) NOT NULL, RolId int NOT NULL REFERENCES dbo.Rol(Id), Activo bit NOT NULL DEFAULT 1, Intentos int NOT NULL DEFAULT 0, BloqueadoHasta datetime2 NULL);
IF OBJECT_ID('dbo.Aditamento','U') IS NULL
CREATE TABLE dbo.Aditamento (Id int IDENTITY PRIMARY KEY, Codigo nvarchar(40) NOT NULL UNIQUE, Nombre nvarchar(120) NOT NULL, Descripcion nvarchar(1000) NULL, Fotografia nvarchar(150) NULL);
IF OBJECT_ID('dbo.Categoria_Personal','U') IS NULL
CREATE TABLE dbo.Categoria_Personal (Id int IDENTITY PRIMARY KEY, Nombre nvarchar(120) NOT NULL UNIQUE, Descripcion nvarchar(1000) NULL);
IF OBJECT_ID('dbo.CategoriaAditamento','U') IS NULL
CREATE TABLE dbo.CategoriaAditamento (CategoriaPersonalId int NOT NULL REFERENCES dbo.Categoria_Personal(Id), AditamentoId int NOT NULL REFERENCES dbo.Aditamento(Id), PRIMARY KEY(CategoriaPersonalId,AditamentoId));
IF OBJECT_ID('dbo.Persona','U') IS NULL
CREATE TABLE dbo.Persona (Id int IDENTITY PRIMARY KEY, Nombre nvarchar(150) NOT NULL, CategoriaPersonalId int NOT NULL REFERENCES dbo.Categoria_Personal(Id), FechaNacimiento date NOT NULL, DPI varchar(13) NOT NULL UNIQUE CHECK(LEN(DPI)=13 AND DPI NOT LIKE '%[^0-9]%'), Telefono varchar(8) NOT NULL CHECK(LEN(Telefono)=8 AND Telefono NOT LIKE '%[^0-9]%'), Fotografia nvarchar(150) NULL);
IF NOT EXISTS(SELECT 1 FROM dbo.Rol WHERE Nombre=N'Administrador') INSERT dbo.Rol(Nombre) VALUES(N'Administrador');
COMMIT;
