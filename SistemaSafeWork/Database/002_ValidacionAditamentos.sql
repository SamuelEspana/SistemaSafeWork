SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.Aditamento','ZonaCuerpo') IS NULL
 ALTER TABLE dbo.Aditamento ADD ZonaCuerpo nvarchar(30) NOT NULL CONSTRAINT DF_Aditamento_ZonaCuerpo DEFAULT N'Sin definir';
COMMIT;
