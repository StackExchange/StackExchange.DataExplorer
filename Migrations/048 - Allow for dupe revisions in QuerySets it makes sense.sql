if dbo.fnIndexExists('QuerySets', 'QuerySets_InitialRevisionId_Unique') = 1
begin 
	drop index QuerySets.QuerySets_InitialRevisionId_Unique
	create index QuerySets_InitialRevisionId on QuerySets(InitialRevisionId)
end

go 

if dbo.fnIndexExists('QuerySets', 'QuerySets_CurrentRevisionId_Unique') = 1
begin 
	drop index QuerySets.QuerySets_CurrentRevisionId_Unique
	create index QuerySets_CurrentRevisionId on QuerySets(CurrentRevisionId)
end

