IF dbo.fnColumnExists('Users', 'DOB') = 1
	ALTER TABLE [dbo].[Users] DROP COLUMN [DOB]