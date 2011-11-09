IF dbo.fnColumnExists('CachedResults', 'ExecutionPlan') = 0
	ALTER TABLE [dbo].[CachedResults] ADD ExecutionPlan nvarchar(max) NULL
IF dbo.fnColumnExists('CachedResults', 'Truncated') = 0
	ALTER TABLE [dbo].[CachedResults] ADD Truncated bit NOT NULL default(0)
IF dbo.fnColumnExists('CachedResults', 'Messages') = 0
	ALTER TABLE [dbo].[CachedResults] ADD [Messages] nvarchar(max) NULL