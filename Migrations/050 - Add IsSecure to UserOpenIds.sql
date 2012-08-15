IF dbo.fnColumnExists('UserOpenIds', 'IsSecure') = 0
BEGIN
	ALTER TABLE UserOpenIds ADD IsSecure BIT NOT NULL DEFAULT 0
END