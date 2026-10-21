SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.Aditamento','ColorRequerido') IS NULL
 ALTER TABLE dbo.Aditamento ADD ColorRequerido nvarchar(20) NULL;
COMMIT;