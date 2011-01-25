if OBJECT_ID('BlackList') is null 
begin 
	create table BlackList(Id int identity primary key, IPAddress varchar(15), CreationDate datetime)
	create index BlackList_IPAddress on BlackList(IPAddress)
end