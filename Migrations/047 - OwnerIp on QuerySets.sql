if dbo.fnColumnExists('QuerySets','OwnerIp') = 0
begin
	alter table QuerySets add OwnerIp varchar(15)
end
