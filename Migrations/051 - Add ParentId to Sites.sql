IF dbo.fnColumnExists('Sites', 'ParentId') = 0
BEGIN
	ALTER TABLE Sites ADD ParentId INT NULL
	
	EXEC('
		UPDATE
			meta
		SET
			meta.ParentId = main.Id
		FROM
			Sites meta
		JOIN
			Sites main ON REPLACE(meta.Url, ''http://meta.'', ''http://'') = main.Url
		WHERE
			LEFT(meta.Url, 12) = ''http://meta.''
	')
END