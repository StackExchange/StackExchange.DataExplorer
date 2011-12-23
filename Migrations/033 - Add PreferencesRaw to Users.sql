IF dbo.fnColumnExists('Users', 'PreferencesRaw') = 0
	ALTER TABLE [dbo].Users Add PreferencesRaw nvarchar(2000)

IF dbo.fnColumnExists('Users', 'HideSchema') = 1
	ALTER TABLE [dbo].Users drop column HideSchema 