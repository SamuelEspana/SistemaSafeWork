SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.Monitoreo_Detalle','U') IS NULL
BEGIN
 CREATE TABLE dbo.Monitoreo_Detalle(
  Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
  EventoId uniqueidentifier NOT NULL UNIQUE,
  SesionId uniqueidentifier NOT NULL,
  PistaId uniqueidentifier NOT NULL,
  UsuarioId int NOT NULL REFERENCES dbo.Usuario(Id),
  FechaUtc datetime2(3) NOT NULL,
  Fotografia nvarchar(40) NOT NULL,
  Observacion nvarchar(500) NOT NULL
 );
 CREATE INDEX IX_Monitoreo_Detalle_Fecha ON dbo.Monitoreo_Detalle(FechaUtc DESC,Id DESC);
END
COMMIT;
