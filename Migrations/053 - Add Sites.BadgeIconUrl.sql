IF dbo.fnColumnExists('Sites', 'BadgeIconUrl') = 0
BEGIN
	ALTER TABLE dbo.[Sites] ADD BadgeIconUrl NVARCHAR(255);
	
	EXEC('
		UPDATE dbo.[Sites]
		   SET BadgeIconUrl = ''//cdn.sstatic.net/'' + (CASE WHEN CHARINDEX(''meta.'', TinyName) = 1 THEN SUBSTRING(TinyName, 6, LEN(TinyName) - 5) + ''meta'' ELSE TinyName END) + ''/img/apple-touch-icon.png''
		 WHERE Id IN( 
			SELECT Id
			  FROM dbo.[Sites]
			 WHERE CHARINDEX(''stackexchange.com'', Url) != 0
			    OR CHARINDEX(''stackoverflow.com'', Url) != 0
			    OR CHARINDEX(''serverfault.com'', Url) != 0
			    OR CHARINDEX(''superuser.com'', Url) != 0
			    OR CHARINDEX(''stackapps.com'', Url) != 0
			    OR CHARINDEX(''askubuntu.com'', Url) != 0
			    OR CHARINDEX(''mathoverflow.net'', Url) != 0
		 )
	');
END