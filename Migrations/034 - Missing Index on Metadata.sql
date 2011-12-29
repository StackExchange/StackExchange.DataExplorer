if dbo.fnIndexExists('Metadata', 'Metadata_OwnerId') = 0
begin 
	create index Metadata_OwnerId on Metadata(OwnerId)
end