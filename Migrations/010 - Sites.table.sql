if OBJECT_ID('Sites') is null
begin

CREATE TABLE [dbo].[Sites]
(
	Id int NOT NULL primary key, 
	TinyName nvarchar(12) not null,
	Name nvarchar(64) not null, 
	LongName nvarchar(64) not null,
	Url nvarchar(max) not null, 
	ImageUrl nvarchar(max) not null, 
	IconUrl nvarchar(max) not null,
	DatabaseName nvarchar(max) not null,
	Tagline nvarchar(max) not null, 
	TagCss nvarchar(max) not null,
	TotalQuestions int, 
	TotalAnswers int, 
	TotalUsers int, 
	TotalComments int, 
	TotalTags int, 
	LastPost datetime,
	ODataEndpoint varchar(max)
)

end

