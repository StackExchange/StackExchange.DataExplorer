if dbo.fnIndexExists('CachedResults', 'idxId') = 1
begin
	drop index CachedResults.idxId
end
go
if dbo.fnIndexExists('CachedResults', 'idxIdPrimaryKey') = 0
begin
	create unique index idxIdUnique on CachedResults (Id)
end