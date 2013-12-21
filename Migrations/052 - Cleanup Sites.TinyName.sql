ALTER TABLE [dbo].Sites ALTER COLUMN TinyName NVARCHAR(50);

UPDATE [dbo].Sites SET TinyName = SUBSTRING(Url, CHARINDEX('//', Url) + 2, (CASE WHEN SUBSTRING(Url, CHARINDEX('//', Url) + 2, 5) = 'meta.' THEN CHARINDEX('.', Url, CHARINDEX('.', Url) + 1) ELSE CHARINDEX('.', Url) END) - (CHARINDEX('//', Url) + 2)) FROM Sites