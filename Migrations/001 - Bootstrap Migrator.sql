-- This script will set helper function for the migration system.

IF OBJECT_ID('fnColumnExists') IS NOT NULL
BEGIN
	 DROP FUNCTION fnColumnExists
END 

GO

CREATE FUNCTION fnColumnExists(
	@table_name nvarchar(max),
	@column_name nvarchar(max) 
)
RETURNS bit 
BEGIN  
	DECLARE @found bit
	SET @found = 0
	IF	EXISTS (
			SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
			WHERE TABLE_NAME = @table_name AND COLUMN_NAME = @column_name ) 
	BEGIN
		SET @found = 1
	END
	 
	
	RETURN @found
END
 
GO 

IF OBJECT_ID('fnIndexExists') IS NOT NULL
BEGIN
	 DROP FUNCTION fnIndexExists
END 

GO

CREATE FUNCTION fnIndexExists(
	@table_name nvarchar(max),
	@index_name nvarchar(max) 
)
RETURNS bit 
BEGIN  
	DECLARE @found bit
	SET @found = 0
	IF	EXISTS (
			SELECT 1 FROM sys.indexes
			WHERE object_id = OBJECT_ID(@table_name) AND name = @index_name ) 
	BEGIN
		SET @found = 1
	END
	 
	
	RETURN @found
END

GO
