IF dbo.fnColumnExists('Users', 'HideSchema') = 0
	ALTER TABLE [dbo].[Users] ADD HideSchema bit NULL