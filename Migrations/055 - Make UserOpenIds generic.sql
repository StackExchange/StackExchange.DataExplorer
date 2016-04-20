IF OBJECT_ID('UserAuthClaims') IS NULL
BEGIN
	EXEC sp_rename 'UserOpenIds', 'UserAuthClaims'
END

GO

IF dbo.fnIndexExists('UserAuthClaims', 'OpenIdClaimIdx') = 1
BEGIN
	DROP INDEX OpenIdClaimIdx ON UserAuthClaims
END

GO

IF dbo.fnColumnExists('UserAuthClaims', 'OpenIdClaim') = 1
BEGIN
	EXEC sp_rename 'UserAuthClaims.OpenIdClaim', 'ClaimIdentifier', 'COLUMN'
END

GO

IF dbo.fnColumnExists('UserAuthClaims', 'IdentifierType') = 0
BEGIN
	ALTER TABLE UserAuthClaims ADD IdentifierType TINYINT NOT NULL DEFAULT 1
END

GO

IF dbo.fnColumnExists('UserAuthClaims', 'Display') = 0
BEGIN
	ALTER TABLE UserAuthClaims ADD Display NVARCHAR(150)
END

GO

IF dbo.fnIndexExists('UserAuthClaims', 'ClaimIdentifierIdx') = 0
BEGIN
	ALTER TABLE UserAuthClaims ALTER COLUMN ClaimIdentifier NVARCHAR(449) NOT NULL
	CREATE UNIQUE NONCLUSTERED INDEX ClaimIdentifierIdx ON UserAuthClaims(IdentifierType, ClaimIdentifier)
END