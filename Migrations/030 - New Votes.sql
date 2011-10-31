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
	ALTER TABLE [dbo].[Votes] ADD OwnerId int NOT NULL
	ALTER TABLE [dbo].[Votes] ADD RootId int NOT NULL
	ALTER TABLE [dbo].[Votes] DROP SavedQueryId
END