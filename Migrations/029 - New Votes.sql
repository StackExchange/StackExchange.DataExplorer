IF dbo.fnColumnExists('Votes', 'OwnerId') = 0
	ALTER TABLE [dbo].[Votes] ADD OwnerId int NOT NULL default(-1)
IF dbo.fnColumnExists('Votes', 'RootId') = 0
	ALTER TABLE [dbo].[Votes] ADD RootId int NOT NULL default(-1)

IF dbo.fnIndexExistsWith('Votes', 'queryIdx', 'SavedQueryId') = 1
BEGIN
	CREATE UNIQUE INDEX [queryIdx] ON [dbo].[Votes]
	(
		[RootId] ASC,
		[OwnerId] ASC,
		[UserId] ASC
	) WITH (DROP_EXISTING = ON)
END

IF dbo.fnIndexExists('Votes','queryIdx2') = 1
	DROP INDEX Votes.queryIdx2