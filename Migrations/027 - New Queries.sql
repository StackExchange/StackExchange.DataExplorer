IF OBJECT_ID('Queries') IS NULL
BEGIN
	CREATE TABLE [dbo].[Queries](
		[Id] [int] IDENTITY(1,1) NOT NULL,
		[QueryHash] [uniqueidentifier] NOT NULL,
		[QueryBody] [nvarchar](max) NOT NULL,
	 CONSTRAINT [PK_Queries] PRIMARY KEY CLUSTERED 
	(
		[Id] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	) ON [PRIMARY]
END
ELSE
BEGIN
	ALTER TABLE [dbo].[Queries] DROP COLUMN CreatorId
	ALTER TABLE [dbo].[Queries] DROP COLUMN CreatorIP
	ALTER TABLE [dbo].[Queries] DROP COLUMN FirstRun
	ALTER TABLE [dbo].[Queries] DROP COLUMN [Views]
	ALTER TABLE [dbo].[Queries] DROP COLUMN Name
	ALTER TABLE [dbo].[Queries] DROP COLUMN [Description]
END