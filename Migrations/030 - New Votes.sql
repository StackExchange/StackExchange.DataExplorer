IF OBJECT_ID('Votes') IS NULL
BEGIN
	CREATE TABLE [dbo].[Votes](
		[Id] [int] IDENTITY(1,1) NOT NULL,
		[OwnerId] [int] NOT NULL,
		[RootId] [int] NOT NULL,
		[UserId] [int] NOT NULL,
		[VoteTypeId] [int] NOT NULL,
		[CreationDate] [datetime] NOT NULL,
	PRIMARY KEY CLUSTERED 
	(
		[Id] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	) ON [PRIMARY]
END
ELSE
BEGIN
	if not exists (select 1 from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME = 'Votes' and COLUMN_NAME = 'OwnerId')
		ALTER TABLE [dbo].[Votes] ADD OwnerId int NOT NULL default(-1)
	if not  exists (select 1 from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME = 'Votes' and COLUMN_NAME = 'RootId')
		ALTER TABLE [dbo].[Votes] ADD RootId int NOT NULL default(-1)
	
	if exists (select 1 from sys.indexes where name = 'queryIdx' and object_id = object_id('Votes'))
		drop index Votes.queryIdx
		
	if exists (select 1 from sys.indexes where name = 'queryIdx2' and object_id = object_id('Votes'))
		drop index Votes.queryIdx2
	
	if exists (select 1 from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME = 'Votes' and COLUMN_NAME = 'SavedQueryId')
		ALTER TABLE [dbo].[Votes] DROP column SavedQueryId
END


