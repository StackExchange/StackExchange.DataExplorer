IF dbo.fnColumnExists('Votes', 'SavedQueryId') = 1
	ALTER TABLE [dbo].[Votes] DROP COLUMN SavedQueryId;