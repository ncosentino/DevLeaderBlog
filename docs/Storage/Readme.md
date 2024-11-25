## Storage Provider

Currently, there are 5 Storage-Provider:

-   RavenDb - As the name suggests for RavenDb. RavenDb automatically creates all the documents, if a database name is provided.
-   MongoDB - Based on the official MongoDB driver. The database and collection are automatically created.
-   Sqlite - Based on EF Core, it can be easily adapted for other Sql Dialects. The tables are automatically created.
-   SqlServer - Based on EF Core, it can be easily adapted for other Sql Dialects. The tables are automatically created.
-   MySql - Based on EF Core - also supports MariaDB.

The default (when you clone the repository) is the `Sqlite` option with an in-memory database.
That means every time you restart the service, all posts and related objects are gone. This is useful for testing. 
If you want to persist the data with Sqlite, you can change the `appsettings.json` file to:

```json
{
	"PersistenceProvider": "Sqlite",
	"ConnectionString": "Data Source=blog.db",
```

Note the ConnectionString format of SQL Server needs to be consistent:

```
"ConnectionString": "Data Source=sql;Initial Catalog=master;User ID=sa;Password=<YOURPASSWORD>;TrustServerCertificate=True;MultiSubnetFailover=True"
```

For MySql use the following:

```
"PersistenceProvider": "MySql"
"ConnectionString": "Server=YOURSERVER;User ID=YOURUSERID;Password=YOURPASSWORD;Database=YOURDATABASE"
```

## Considerations
For most people a Sqlite database might be the best choice between convienence and ease of setup. As it runs "in-process" there are no additional dependencies or setup required (and therefore no additional cost). As the blog tries to cache many things, the load onto the database is not that big (performance considerations). The advantages of a "real" database like SqlServer or MySql are more in the realm of backups, replication, and other enterprise features (which are not needed often times for a simple blog).