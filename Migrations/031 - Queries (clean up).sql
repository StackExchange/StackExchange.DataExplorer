IF dbo.fnColumnExists('Queries', 'CreatorId') = 1
	ALTER TABLE [dbo].[Queries] DROP COLUMN CreatorId;
IF dbo.fnColumnExists('Queries', 'CreatorIP') = 1
	ALTER TABLE [dbo].[Queries] DROP COLUMN CreatorIP;
IF dbo.fnColumnExists('Queries', 'FirstRun') = 1
	ALTER TABLE [dbo].[Queries] DROP COLUMN FirstRun;
IF dbo.fnColumnExists('Queries', 'Views') = 1
	ALTER TABLE [dbo].[Queries] DROP COLUMN [Views];
IF dbo.fnColumnExists('Queries', 'Name') = 1
	ALTER TABLE [dbo].[Queries] DROP COLUMN Name;
IF dbo.fnColumnExists('Queries', 'Description') = 1
	ALTER TABLE [dbo].[Queries] DROP COLUMN [Description];
	
IF NOT EXISTS(
	SELECT * FROM
		INFORMATION_SCHEMA.TABLE_CONSTRAINTS constraints JOIN
		INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE usage ON constraints.CONSTRAINT_NAME = usage.CONSTRAINT_NAME
	WHERE
		constraints.TABLE_NAME = 'Queries' AND
		usage.COLUMN_NAME = 'Id'
)
	ALTER TABLE [dbo].[Queries] ADD PRIMARY KEY (Id);