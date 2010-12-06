if OBJECT_ID('[spGrantReadonly]') is not null
 exec ('drop proc spGrantReadonly')
go 

CREATE PROCEDURE [dbo].[spGrantReadonly]
AS

declare sites_cursor cursor 
for select DatabaseName from Sites

open sites_cursor 

declare @database_name nvarchar(max)  

declare @local varchar(2000) 
select @local = DB_NAME()

fetch next from sites_cursor INTO  @database_name
While (@@FETCH_STATUS <> -1)
BEGIN
	IF (@@FETCH_STATUS <> -2)
	
	declare @sql varchar(2000) 
	
	set @sql = 'use [' + @database_name + '] 
ALTER AUTHORIZATION ON SCHEMA::[db_datareader] TO [db_datareader] 
if exists (select * from sys.sysusers where name = ''readonly'')
drop user [readonly]
create user [readonly] FOR LOGIN [readonly] 
exec sp_addrolemember N''db_datareader'', N''readonly'' 
'
   exec(@sql)
   
	fetch next from sites_cursor INTO  @database_name
   
END
close sites_cursor
deallocate sites_cursor 

delete from CachedResults





