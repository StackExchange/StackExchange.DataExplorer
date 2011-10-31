IF OBJECT_ID('QueryExecutions') IS NULL
BEGIN
CREATE TABLE [dbo].[QueryExecutions](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[RevisionId] [int] NOT NULL,
	[QueryId] [int] NOT NULL,
	[UserId] [int] NULL,
	[SiteId] [int] NOT NULL,
	[FirstRun] [datetime] NOT NULL,
	[LastRun] [datetime] NOT NULL,
	[ExecutionCount] [int] NOT NULL
) ON [PRIMARY]
END
ELSE
BEGIN
	ALTER TABLE [dbo].[QueryExecutions] ALTER COLUMN UserId int NULL
	ALTER TABLE [dbo].[QueryExecutions] ADD RevisionId int NOT NULL
END

