IF NOT EXISTS(SELECT 1 FROM sys.tables WHERE [name] = 'Migrations')
BEGIN
	CREATE TABLE Migrations 
	(
		Id int identity primary key, 
		[Filename] nvarchar(260),
		[Hash] varchar(40), 
		[ExecutionDate] datetime,
		[Duration] int		
	)
	CREATE UNIQUE INDEX UQ_Filename ON Migrations([Filename])
	CREATE UNIQUE INDEX UQ_Hash ON Migrations([Hash])
END
