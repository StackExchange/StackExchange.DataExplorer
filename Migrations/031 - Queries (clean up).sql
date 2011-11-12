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