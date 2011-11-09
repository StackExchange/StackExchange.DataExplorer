ALTER TABLE [dbo].[QueryExecutions] ALTER COLUMN UserId int NULL

IF dbo.fnColumnExists('QueryExecutions', 'RevisionId') = 0
	ALTER TABLE [dbo].[QueryExecutions] ADD RevisionId int NOT NULL default(-1)

IF dbo.fnIndexExistsWith('QueryExecutions', 'idxUniqueQE', 'RevisionId') = 0
BEGIN
	CREATE UNIQUE CLUSTERED INDEX [idxUniqueQE] ON [dbo].[QueryExecutions]
	(
		[UserId] ASC,
		[RevisionId] ASC,
		[QueryId] ASC
	) WITH (DROP_EXISTING = ON)
END