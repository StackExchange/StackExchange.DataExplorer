if OBJECT_ID('AppSettings') is null
begin
	CREATE TABLE [dbo].AppSettings
	(
		Id int identity not null primary key,
		Setting varchar(50) not null, 
		Value nvarchar(max)
	)
	
	exec ('create unique index idxUniqueSettings on AppSettings(Setting)')
end
