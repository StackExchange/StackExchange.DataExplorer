IF dbo.fnIndexExistsWith('Votes', 'queryIdx', 'SavedQueryId') = 1
BEGIN
	CREATE UNIQUE INDEX [queryIdx] ON [dbo].[Votes]
	(
		[RootId] ASC,
		[OwnerId] ASC,
		[UserId] ASC
	) WITH (DROP_EXISTING = ON)
END

IF dbo.fnColumnExists('Votes', 'SavedQueryId') = 1
	ALTER TABLE [dbo].[Votes] DROP COLUMN SavedQueryId;