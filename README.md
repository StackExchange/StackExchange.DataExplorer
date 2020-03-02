# Data Explorer

The Stack Exchange [Data Explorer](https://data.stackexchange.com) is a tool for executing arbitrary SQL queries against data from the various question and answer sites in the [Stack Exchange](https://stackexchange.com) network.

## Quick Guide

### Prerequisites

 - [Visual Studio](https://www.visualstudio.com/en-us/visual-studio-homepage-vs.aspx) 2015 or later
 - [.NET Framework 4.7](https://www.microsoft.com/en-us/download/details.aspx?id=55170)
 - [SQL Server 2012 Express](https://www.microsoft.com/en-us/sqlserver/editions/2012-editions/express.aspx) or later/higher
 - [IIS7](https://www.iis.net/) or later
    - The [URL Rewrite 2.0 extension](https://www.iis.net/downloads/microsoft/url-rewrite) is required for OData endpoint support. If you don't need this, you can comment out the `<rewrite>` section in `web.config`.

### Layout

  - The `App` directory contains the Data Explorer solution
  - The `Migrations` directory contains the database evolution scripts and a batch file for running them 
  - The `Tools` directory contains some precompiled utilities for working with Data Explorer
  - The `Lib` directory contains some 3rd party binaries used in the application
  - The `SeedData` directory contains seed files to populate the Data Explorer schema with some sample data

### Configuration

The database can be brought up to date by running the `migrate.local.bat` file in the `Migrations` directory. This assumes an existing SQL Server database named DataExplorer with integrated security enabled. If your environment is configured differently, you will need to modify the connection string in your batch file and `web.config` file to reflect your setup. 

Once done, you'll need to populate the `Sites` table with a record for each site you intend to query against. You can run the `sites.sql` file in the `SeedData` directory to prepopulate the table with a small list of popular Stack Exchange sites. To actually run queries, you will need to create additional databases that reflect the connection values in the `Sites` table; these can optionally be populated with data from the [Stack Exchange data dumps](https://stackoverflow.blog/2009/06/04/stack-overflow-creative-commons-data-dump/) using a custom import process or one of the data dump import tools available on [Stack Apps](https://stackapps.com). 

## Contributing

### Development

  - Install [git](https://git-scm.com/), or a git client of your choosing (such as [TortoiseGit](https://tortoisegit.org/), [SourceTree](https://www.sourcetreeapp.com/), or [GitHub Desktop](https://desktop.github.com/))
  - Fork this repository on GitHub
  - Commit changes to your fork, preferably in easy-to-merge branches
  - Submit a pull request on GitHub with a description of your changes

### Providing Feedback

  - Submit [bugs](https://meta.stackexchange.com/questions/ask?tags=data-explorer%20bug), [feature requests](https://meta.stackexchange.com/questions/ask?tags=data-explorer%20feature-request), and [support questions](https://meta.stackexchange.com/questions/ask?tags=data-explorer%20support) on [Meta Stack Exchange](https://meta.stackexchange.com/) or [create a new GitHub issue](https://github.com/StackExchange/StackExchange.DataExplorer/issues/new).

## Miscellaneous

### Third Party Components

 - [ASP.NET MVC 5](https://www.asp.net/)
 - [MiniProfiler](https://github.com/MiniProfiler/dotnet)
 - [Dapper](https://github.com/StackExchange/dapper-dot-net)
 - [jQuery](https://jquery.com/)
 - [CodeMirror](https://codemirror.net/)
 - [SlickGrid](https://github.com/mleibman/SlickGrid) (currently using [a fork](https://github.com/tms/SlickGrid) for updates)
 - [Flot](https://www.flotcharts.org/)
 - [StackExchange.Exceptional](https://github.com/NickCraver/StackExchange.Exceptional)
 - [Json.NET](james.newtonking.com/json)
 - [DotNetOpenAuth](http://www.dotnetopenauth.net/)
 - [reCAPTCHA](https://code.google.com/p/recaptcha/)

For more information, see ["Which tools and technologies are used to build Data Explorer?"](https://meta.stackexchange.com/questions/51967/which-tools-and-technologies-are-used-to-build-data-explorer)
