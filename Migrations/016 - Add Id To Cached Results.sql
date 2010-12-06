if dbo.fnColumnExists('CachedResults', 'Id') = 0
begin 
	alter table CachedResults add Id int identity not null
	create index idxId on CachedResults(Id)
end