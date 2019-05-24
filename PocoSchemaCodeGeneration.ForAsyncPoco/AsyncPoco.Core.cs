using System;
using System.Data;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/*
 This code is part of the PetaPoco project (http://www.toptensoftware.com/petapoco).
 It is based on the SubSonic T4 templates but has been considerably re-organized and reduced
 
 -----------------------------------------------------------------------------------------

 This template can read minimal schema information from the following databases:

	* SQL Server
	* SQL Server CE
	* MySQL
	* PostGreSQL
	* Oracle

 For connection and provider settings the template will look for the web.config or app.config file of the 
 containing Visual Studio project.  It will not however read DbProvider settings from this file.

 In order to work, the appropriate driver must be registered in the system machine.config file.  If you're
 using Visual Studio 2010 the file you want is here:

	C:\Windows\Microsoft.NET\Framework\v4.0.30319\Config\machine.config

 After making changes to machine.config you will also need to restart Visual Studio.

 Here's a typical set of entries that might help if you're stuck:

	<system.data>
		<DbProviderFactories>
			<add name="Odbc Data Provider" invariant="System.Data.Odbc" description=".Net Framework Data Provider for Odbc" type="System.Data.Odbc.OdbcFactory, System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"/>
			<add name="OleDb Data Provider" invariant="System.Data.OleDb" description=".Net Framework Data Provider for OleDb" type="System.Data.OleDb.OleDbFactory, System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"/>
			<add name="OracleClient Data Provider" invariant="System.Data.OracleClient" description=".Net Framework Data Provider for Oracle" type="System.Data.OracleClient.OracleClientFactory, System.Data.OracleClient, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"/>
			<add name="SqlClient Data Provider" invariant="System.Data.SqlClient" description=".Net Framework Data Provider for SqlServer" type="System.Data.SqlClient.SqlClientFactory, System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"/>
			<add name="MySQL Data Provider" invariant="MySql.Data.MySqlClient" description=".Net Framework Data Provider for MySQL" type="MySql.Data.MySqlClient.MySqlClientFactory, MySqlConnector, Version=0.30.0.0, Culture=neutral, PublicKeyToken=d33d3e53aa5f8c92"/>
			<add name="Microsoft SQL Server Compact Data Provider" invariant="System.Data.SqlServerCe.3.5" description=".NET Framework Data Provider for Microsoft SQL Server Compact" type="System.Data.SqlServerCe.SqlCeProviderFactory, System.Data.SqlServerCe, Version=3.5.1.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91"/><add name="Microsoft SQL Server Compact Data Provider 4.0" invariant="System.Data.SqlServerCe.4.0" description=".NET Framework Data Provider for Microsoft SQL Server Compact" type="System.Data.SqlServerCe.SqlCeProviderFactory, System.Data.SqlServerCe, Version=4.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91"/>
			<add name="Npgsql Data Provider" invariant="Npgsql" support="FF" description=".Net Framework Data Provider for Postgresql Server" type="Npgsql.NpgsqlFactory, Npgsql, Version=2.0.11.91, Culture=neutral, PublicKeyToken=5d8b90d52f46fda7" />
		</DbProviderFactories>
	</system.data>

 Also, the providers and their dependencies need to be installed to GAC.  

 Eg; this is how I installed the drivers for PostgreSQL

	 gacutil /i Npgsql.dll
	 gacutil /i Mono.Security.dll

 -----------------------------------------------------------------------------------------
 
 SubSonic - http://subsonicproject.com
 
 The contents of this file are subject to the New BSD
 License (the "License"); you may not use this file
 except in compliance with the License. You may obtain a copy of
 the License at http://www.opensource.org/licenses/bsd-license.php
 
 Software distributed under the License is distributed on an 
 "AS IS" basis, WITHOUT WARRANTY OF ANY KIND, either express or
 implied. See the License for the specific language governing
 rights and limitations under the License.
*/

public class GeneratorSettings {
	public DbProviderFactory DbProvider;
	public Dialect SqlDialect;
	public string CsFile;
	public string ConnectionString;
	public string Namespace = "";
	public string RepoName = "";
	public string ClassPrefix = "";
	public string ClassSuffix = "";
	public string SchemaName = null;
	public bool IncludeViews = true;
	public bool TrackModifiedColumns = false;
}

public class Table
{
    public List<Column> Columns;	
    public string Name;
	public string Schema;
	public bool IsView;
    public string CleanName;
    public string ClassName;
	public string SequenceName;
	public bool Ignore;

    public Column PK
    {
        get
        {
            return this.Columns.SingleOrDefault(x=>x.IsPK);
        }
    }

	public Column GetColumn(string columnName)
	{
		return Columns.Single(x=>string.Compare(x.Name, columnName, true)==0);
	}

	public Column this[string columnName]
	{
		get
		{
			return GetColumn(columnName);
		}
	}

}

public class Column
{
    public string Name;
    public string PropertyName;
    public string PropertyType;
    public bool IsPK;
    public bool IsNullable;
	public bool IsAutoIncrement;
	public bool Ignore;

	public bool DomainIsNullable =>
		IsNullable && 
		PropertyType !="byte[]" && 
		PropertyType !="string" &&
		PropertyType !="Microsoft.SqlServer.Types.SqlGeography" &&
		PropertyType !="Microsoft.SqlServer.Types.SqlGeometry";
}

public class Tables : List<Table>
{
	public Tables()
	{
	}
	
	public Table GetTable(string tableName)
	{
		return this.Single(x=>string.Compare(x.Name, tableName, true)==0);
	}

	public Table this[string tableName]
	{
		get
		{
			return GetTable(tableName);
		}
	}

}

public enum Dialect {
    MySql,
    SqlServerCe,
    Postgres,
    Oracle,
    SqlServer
}

abstract class SchemaReader
{
    static Regex rxCleanUp = new Regex(@"[^\w\d_]", RegexOptions.Compiled);

    static string[] cs_keywords = { "abstract", "event", "new", "struct", "as", "explicit", "null", 
        "switch", "base", "extern", "object", "this", "bool", "false", "operator", "throw", 
        "break", "finally", "out", "true", "byte", "fixed", "override", "try", "case", "float", 
        "params", "typeof", "catch", "for", "private", "uint", "char", "foreach", "protected", 
        "ulong", "checked", "goto", "public", "unchecked", "class", "if", "readonly", "unsafe", 
        "const", "implicit", "ref", "ushort", "continue", "in", "return", "using", "decimal", 
        "int", "sbyte", "virtual", "default", "interface", "sealed", "volatile", "delegate", 
        "internal", "short", "void", "do", "is", "sizeof", "while", "double", "lock", 
        "stackalloc", "else", "long", "static", "enum", "namespace", "string" };

	public abstract Tables ReadSchema(DbConnection connection, DbProviderFactory factory);
	
    protected Func<string, string> CleanUp = (str) =>
    {
	    str = rxCleanUp.Replace(str, "_");

	    if (char.IsDigit(str[0]) || cs_keywords.Contains(str))
		    str = "@" + str;
	    
        return str;
    };
    
    public static Tables LoadTables(GeneratorSettings settings)
    {
	    Tables result;
	    using(var conn=settings.DbProvider.CreateConnection())
	    {
		    conn.ConnectionString = settings.ConnectionString;
		    conn.Open();
	    
		    SchemaReader reader=null;
		    
		    switch(settings.SqlDialect) {
			    case Dialect.MySql:
				    reader=new MySqlSchemaReader();
				    break;

			    case Dialect.SqlServerCe:
				    reader=new SqlServerCeSchemaReader();
				    break;

			    case Dialect.Postgres:
				    reader=new PostGreSqlSchemaReader();
				    break;

			    case Dialect.Oracle:
				    reader=new OracleSchemaReader();
				    break;

			    case Dialect.SqlServer:
				    reader=new SqlServerSchemaReader();
				    break;

			    default: throw new Exception($"unsupported dialect {settings.SqlDialect}");
		    }
			    
		    
		    result=reader.ReadSchema(conn, settings.DbProvider);

		    // Remove unrequired tables/views
		    for (int i=result.Count-1; i>=0; i--)
		    {
			    if (settings.SchemaName!=null && string.Compare(result[i].Schema, settings.SchemaName, true)!=0)
			    {
				    result.RemoveAt(i);
				    continue;
			    }
			    if (!settings.IncludeViews && result[i].IsView)
			    {
				    result.RemoveAt(i);
				    continue;
			    }
		    }

		    conn.Close();


		    var rxClean = new Regex("^(Equals|GetHashCode|GetType|ToString|repo|Save|IsNew|Insert|Update|Delete|Exists|SingleOrDefault|Single|First|FirstOrDefault|Fetch|Page|Query)$");
		    foreach (var t in result)
		    {
			    t.ClassName = settings.ClassPrefix + t.ClassName + settings.ClassSuffix;
			    foreach (var c in t.Columns)
			    {
				    c.PropertyName = rxClean.Replace(c.PropertyName, "_$1");

				    // Make sure property name doesn't clash with class name
				    if (c.PropertyName == t.ClassName)
					    c.PropertyName = "_" + c.PropertyName;
			    }
		    }

		    return result;
	    }	
    }
}

class SqlServerSchemaReader : SchemaReader
{
	// SchemaReader.ReadSchema
	public override Tables ReadSchema(DbConnection connection, DbProviderFactory factory)
	{
		var result=new Tables();
		
		_connection=connection;
		_factory=factory;

		var cmd=_factory.CreateCommand();
		cmd.Connection=connection;
		cmd.CommandText=TABLE_SQL;

		//pull the tables in a reader
		using(cmd)
		{

			using (var rdr=cmd.ExecuteReader())
			{
				while(rdr.Read())
				{
					Table tbl=new Table();
					tbl.Name=rdr["TABLE_NAME"].ToString();
					tbl.Schema=rdr["TABLE_SCHEMA"].ToString();
					tbl.IsView=string.Compare(rdr["TABLE_TYPE"].ToString(), "View", true)==0;
					tbl.CleanName=CleanUp(tbl.Name);
					tbl.ClassName=Inflector.MakeSingular(tbl.CleanName);

					result.Add(tbl);
				}
			}
		}

		foreach (var tbl in result)
		{
			tbl.Columns=LoadColumns(tbl);
		            
			// Mark the primary key
			string PrimaryKey=GetPK(tbl.Name);
			var pkColumn=tbl.Columns.SingleOrDefault(x=>x.Name.ToLower().Trim()==PrimaryKey.ToLower().Trim());
			if(pkColumn!=null)
			{
				pkColumn.IsPK=true;
			}
		}
	    

		return result;
	}
	
	DbConnection _connection;
	DbProviderFactory _factory;
	

	List<Column> LoadColumns(Table tbl)
	{
	
		using (var cmd=_factory.CreateCommand())
		{
			cmd.Connection=_connection;
			cmd.CommandText=COLUMN_SQL;

			var p = cmd.CreateParameter();
			p.ParameterName = "@tableName";
			p.Value=tbl.Name;
			cmd.Parameters.Add(p);

			p = cmd.CreateParameter();
			p.ParameterName = "@schemaName";
			p.Value=tbl.Schema;
			cmd.Parameters.Add(p);

			var result=new List<Column>();
			using (IDataReader rdr=cmd.ExecuteReader())
			{
				while(rdr.Read())
				{
					Column col=new Column();
					col.Name=rdr["ColumnName"].ToString();
					col.PropertyName=CleanUp(col.Name);
					col.PropertyType=GetPropertyType(rdr["DataType"].ToString());
					col.IsNullable=rdr["IsNullable"].ToString()=="YES";
					col.IsAutoIncrement=((int)rdr["IsIdentity"])==1;
					result.Add(col);
				}
			}

			return result;
		}
	}

	string GetPK(string table){
		
		string sql=@"SELECT c.name AS ColumnName
                FROM sys.indexes AS i 
                INNER JOIN sys.index_columns AS ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id 
                INNER JOIN sys.objects AS o ON i.object_id = o.object_id 
                LEFT OUTER JOIN sys.columns AS c ON ic.object_id = c.object_id AND c.column_id = ic.column_id
                WHERE (i.is_primary_key = 1) AND (o.name = @tableName)";

		using (var cmd=_factory.CreateCommand())
		{
			cmd.Connection=_connection;
			cmd.CommandText=sql;

			var p = cmd.CreateParameter();
			p.ParameterName = "@tableName";
			p.Value=table;
			cmd.Parameters.Add(p);

			var result=cmd.ExecuteScalar();

			if(result!=null)
				return result.ToString();    
		}	         
		
		return "";
	}
	
	string GetPropertyType(string sqlType)
	{
		string sysType="string";
		switch (sqlType) 
		{
			case "bigint":
				sysType = "long";
				break;
			case "smallint":
				sysType= "short";
				break;
			case "int":
				sysType= "int";
				break;
			case "uniqueidentifier":
				sysType=  "Guid";
				 break;
			case "smalldatetime":
			case "datetime":
			case "datetime2":
			case "date":
			case "time":
				sysType=  "DateTime";
				  break;
			case "datetimeoffset":
				sysType = "DateTimeOffset";
				break;
  			case "float":
				sysType="double";
				break;
			case "real":
				sysType="float";
				break;
			case "numeric":
			case "smallmoney":
			case "decimal":
			case "money":
				sysType=  "decimal";
				 break;
			case "tinyint":
				sysType = "byte";
				break;
			case "bit":
				sysType=  "bool";
				   break;
			case "image":
			case "binary":
			case "varbinary":
			case "timestamp":
				sysType=  "byte[]";
				 break;
			case "geography":
				sysType = "Microsoft.SqlServer.Types.SqlGeography";
				break;
			case "geometry":
				sysType = "Microsoft.SqlServer.Types.SqlGeometry";
				break;
		}
		return sysType;
	}

	const string TABLE_SQL=@"SELECT *
		FROM  INFORMATION_SCHEMA.TABLES
		WHERE TABLE_TYPE='BASE TABLE' OR TABLE_TYPE='VIEW'";

	const string COLUMN_SQL=@"SELECT 
			TABLE_CATALOG AS [Database],
			TABLE_SCHEMA AS Owner, 
			TABLE_NAME AS TableName, 
			COLUMN_NAME AS ColumnName, 
			ORDINAL_POSITION AS OrdinalPosition, 
			COLUMN_DEFAULT AS DefaultSetting, 
			IS_NULLABLE AS IsNullable, DATA_TYPE AS DataType, 
			CHARACTER_MAXIMUM_LENGTH AS MaxLength, 
			DATETIME_PRECISION AS DatePrecision,
			COLUMNPROPERTY(object_id('[' + TABLE_SCHEMA + '].[' + TABLE_NAME + ']'), COLUMN_NAME, 'IsIdentity') AS IsIdentity,
			COLUMNPROPERTY(object_id('[' + TABLE_SCHEMA + '].[' + TABLE_NAME + ']'), COLUMN_NAME, 'IsComputed') as IsComputed
		FROM  INFORMATION_SCHEMA.COLUMNS
		WHERE TABLE_NAME=@tableName AND TABLE_SCHEMA=@schemaName
		ORDER BY OrdinalPosition ASC";
	  
}

class SqlServerCeSchemaReader : SchemaReader
{
	// SchemaReader.ReadSchema
	public override Tables ReadSchema(DbConnection connection, DbProviderFactory factory)
	{
		var result=new Tables();
		
		_connection=connection;
		_factory=factory;

		var cmd=_factory.CreateCommand();
		cmd.Connection=connection;
		cmd.CommandText=TABLE_SQL;

		//pull the tables in a reader
		using(cmd)
		{
			using (var rdr=cmd.ExecuteReader())
			{
				while(rdr.Read())
				{
					Table tbl=new Table();
					tbl.Name=rdr["TABLE_NAME"].ToString();
					tbl.CleanName=CleanUp(tbl.Name);
					tbl.ClassName=Inflector.MakeSingular(tbl.CleanName);
					tbl.Schema=null;
					tbl.IsView=false;
					result.Add(tbl);
				}
			}
		}

		foreach (var tbl in result)
		{
			tbl.Columns=LoadColumns(tbl);
		            
			// Mark the primary key
			string PrimaryKey=GetPK(tbl.Name);
			var pkColumn=tbl.Columns.SingleOrDefault(x=>x.Name.ToLower().Trim()==PrimaryKey.ToLower().Trim());
			if(pkColumn!=null)
				pkColumn.IsPK=true;
		}
	    

		return result;
	}
	
	DbConnection _connection;
	DbProviderFactory _factory;
	

	List<Column> LoadColumns(Table tbl)
	{
	
		using (var cmd=_factory.CreateCommand())
		{
			cmd.Connection=_connection;
			cmd.CommandText=COLUMN_SQL;

			var p = cmd.CreateParameter();
			p.ParameterName = "@tableName";
			p.Value=tbl.Name;
			cmd.Parameters.Add(p);

			var result=new List<Column>();
			using (IDataReader rdr=cmd.ExecuteReader())
			{
				while(rdr.Read())
				{
					Column col=new Column();
					col.Name=rdr["ColumnName"].ToString();
					col.PropertyName=CleanUp(col.Name);
					col.PropertyType=GetPropertyType(rdr["DataType"].ToString());
					col.IsNullable=rdr["IsNullable"].ToString()=="YES";
					col.IsAutoIncrement=rdr["AUTOINC_INCREMENT"]!=DBNull.Value;
					result.Add(col);
				}
			}

			return result;
		}
	}

	string GetPK(string table){
		
		string sql=@"SELECT KCU.COLUMN_NAME 
			FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU
			JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
			ON KCU.CONSTRAINT_NAME=TC.CONSTRAINT_NAME
			WHERE TC.CONSTRAINT_TYPE='PRIMARY KEY'
			AND KCU.TABLE_NAME=@tableName";

		using (var cmd=_factory.CreateCommand())
		{
			cmd.Connection=_connection;
			cmd.CommandText=sql;

			var p = cmd.CreateParameter();
			p.ParameterName = "@tableName";
			p.Value=table;
			cmd.Parameters.Add(p);

			var result=cmd.ExecuteScalar();

			if(result!=null)
				return result.ToString();    
		}	         
		
		return "";
	}
	
	string GetPropertyType(string sqlType)
	{
		string sysType="string";
		switch (sqlType) 
		{
			case "bigint":
				sysType = "long";
				break;
			case "smallint":
				sysType= "short";
				break;
			case "int":
				sysType= "int";
				break;
			case "uniqueidentifier":
				sysType=  "Guid";
				 break;
			case "smalldatetime":
			case "datetime":
			case "date":
			case "time":
				sysType=  "DateTime";
				  break;
			case "float":
				sysType="double";
				break;
			case "real":
				sysType="float";
				break;
			case "numeric":
			case "smallmoney":
			case "decimal":
			case "money":
				sysType=  "decimal";
				 break;
			case "tinyint":
				sysType = "byte";
				break;
			case "bit":
				sysType=  "bool";
				   break;
			case "image":
			case "binary":
			case "varbinary":
			case "timestamp":
				sysType=  "byte[]";
				 break;
		}
		return sysType;
	}

	const string TABLE_SQL=@"SELECT *
		FROM  INFORMATION_SCHEMA.TABLES
		WHERE TABLE_TYPE='TABLE'";

	const string COLUMN_SQL=@"SELECT 
			TABLE_CATALOG AS [Database],
			TABLE_SCHEMA AS Owner, 
			TABLE_NAME AS TableName, 
			COLUMN_NAME AS ColumnName, 
			ORDINAL_POSITION AS OrdinalPosition, 
			COLUMN_DEFAULT AS DefaultSetting, 
			IS_NULLABLE AS IsNullable, DATA_TYPE AS DataType, 
			AUTOINC_INCREMENT,
			CHARACTER_MAXIMUM_LENGTH AS MaxLength, 
			DATETIME_PRECISION AS DatePrecision
		FROM  INFORMATION_SCHEMA.COLUMNS
		WHERE TABLE_NAME=@tableName
		ORDER BY OrdinalPosition ASC";
	  
}


class PostGreSqlSchemaReader : SchemaReader
{
	// SchemaReader.ReadSchema
	public override Tables ReadSchema(DbConnection connection, DbProviderFactory factory)
	{
		var result=new Tables();
		
		_connection=connection;
		_factory=factory;

		var cmd=_factory.CreateCommand();
		cmd.Connection=connection;
		cmd.CommandText=TABLE_SQL;

		//pull the tables in a reader
		using(cmd)
		{
			using (var rdr=cmd.ExecuteReader())
			{
				while(rdr.Read())
				{
					Table tbl=new Table();
					tbl.Name=rdr["table_name"].ToString();
					tbl.Schema=rdr["table_schema"].ToString();
					tbl.IsView=string.Compare(rdr["table_type"].ToString(), "View", true)==0;
					tbl.CleanName=CleanUp(tbl.Name);
					tbl.ClassName=Inflector.MakeSingular(tbl.CleanName);
					result.Add(tbl);
				}
			}
		}

		foreach (var tbl in result)
		{
			tbl.Columns=LoadColumns(tbl);
		            
			// Mark the primary key
			string PrimaryKey=GetPK(tbl.Name);
			var pkColumn=tbl.Columns.SingleOrDefault(x=>x.Name.ToLower().Trim()==PrimaryKey.ToLower().Trim());
			if(pkColumn!=null)
				pkColumn.IsPK=true;
		}
	    

		return result;
	}
	
	DbConnection _connection;
	DbProviderFactory _factory;
	

	List<Column> LoadColumns(Table tbl)
	{
	
		using (var cmd=_factory.CreateCommand())
		{
			cmd.Connection=_connection;
			cmd.CommandText=COLUMN_SQL;

			var p = cmd.CreateParameter();
			p.ParameterName = "@tableName";
			p.Value=tbl.Name;
			cmd.Parameters.Add(p);

			var result=new List<Column>();
			using (IDataReader rdr=cmd.ExecuteReader())
			{
				while(rdr.Read())
				{
					Column col=new Column();
					col.Name=rdr["column_name"].ToString();
					col.PropertyName=CleanUp(col.Name);
					col.PropertyType=GetPropertyType(rdr["udt_name"].ToString());
					col.IsNullable=rdr["is_nullable"].ToString()=="YES";
					col.IsAutoIncrement = rdr["column_default"].ToString().StartsWith("nextval(") || rdr["is_identity"].ToString()=="YES";
					result.Add(col);
				}
			}

			return result;
		}
	}

	string GetPK(string table){
		
		string sql=@"SELECT kcu.column_name 
			FROM information_schema.key_column_usage kcu
			JOIN information_schema.table_constraints tc
			ON kcu.constraint_name=tc.constraint_name
			WHERE lower(tc.constraint_type)='primary key'
			AND kcu.table_name=@tablename";

		using (var cmd=_factory.CreateCommand())
		{
			cmd.Connection=_connection;
			cmd.CommandText=sql;

			var p = cmd.CreateParameter();
			p.ParameterName = "@tableName";
			p.Value=table;
			cmd.Parameters.Add(p);

			var result=cmd.ExecuteScalar();

			if(result!=null)
				return result.ToString();    
		}	         
		
		return "";
	}
	
	string GetPropertyType(string sqlType)
	{
		switch (sqlType)
		{
			case "int8":
			case "serial8":	
				return "long";

			case "bool":	
				return "bool";

			case "bytea	":	
				return "byte[]";

			case "float8":	
				return "double";

			case "int4":	
			case "serial4":	
				return "int";

			case "money	":	
				return "decimal";

			case "numeric":	
				return "decimal";

			case "float4":	
				return "float";

			case "int2":	
				return "short";

			case "time":
			case "timetz":
			case "timestamp":
			case "timestamptz":	
			case "date":	
				return "DateTime";

			default:
				return "string";
		}
	}



	const string TABLE_SQL=@"
			SELECT table_name, table_schema, table_type
			FROM information_schema.tables 
			WHERE (table_type='BASE TABLE' OR table_type='VIEW')
				AND table_schema NOT IN ('pg_catalog', 'information_schema');
			";

	const string COLUMN_SQL=@"
			SELECT column_name, is_nullable, udt_name, column_default, is_identity
			FROM information_schema.columns 
			WHERE table_name=@tableName;
			";
	
}

class MySqlSchemaReader : SchemaReader
{
	// SchemaReader.ReadSchema
	public override Tables ReadSchema(DbConnection connection, DbProviderFactory factory)
	{
		var result=new Tables();
	

		var cmd=factory.CreateCommand();
		cmd.Connection=connection;
		cmd.CommandText=TABLE_SQL;

		//pull the tables in a reader
		using(cmd)
		{
			using (var rdr=cmd.ExecuteReader())
			{
				while(rdr.Read())
				{
					Table tbl=new Table();
					tbl.Name=rdr["TABLE_NAME"].ToString();
					tbl.Schema=rdr["TABLE_SCHEMA"].ToString();
					tbl.IsView=string.Compare(rdr["TABLE_TYPE"].ToString(), "View", true)==0;
					tbl.CleanName=CleanUp(tbl.Name);
					tbl.ClassName=Inflector.MakeSingular(tbl.CleanName);
					result.Add(tbl);
				}
			}
		}


        //this will return everything for the DB
        var schema  = connection.GetSchema("COLUMNS");

        //loop again - but this time pull by table name
        foreach (var item in result) 
        {
            item.Columns=new List<Column>();

            //pull the columns from the schema
            var columns = schema.Select("TABLE_NAME='" + item.Name + "'");
            foreach (var row in columns) 
            {
                Column col=new Column();
                col.Name=row["COLUMN_NAME"].ToString();
                col.PropertyName=CleanUp(col.Name);
                col.PropertyType=GetPropertyType(row);
                col.IsNullable=row["IS_NULLABLE"].ToString()=="YES";
                col.IsPK=row["COLUMN_KEY"].ToString()=="PRI";
				col.IsAutoIncrement=row["extra"].ToString().ToLower().IndexOf("auto_increment")>=0;

                item.Columns.Add(col);
            }
        }
        
        return result;
	
	}

	static string GetPropertyType(DataRow row)
	{
		bool bUnsigned = row["COLUMN_TYPE"].ToString().IndexOf("unsigned")>=0;
		string propType="string";
		switch (row["DATA_TYPE"].ToString()) 
		{
			case "bigint":
				propType= bUnsigned ? "ulong" : "long";
				break;
			case "int":
				propType= bUnsigned ? "uint" : "int";
				break;
			case "smallint":
				propType= bUnsigned ? "ushort" : "short";
				break;
			case "guid":
				propType=  "Guid";
				 break;
			case "smalldatetime":
			case "date":
			case "datetime":
			case "timestamp":
				propType=  "DateTime";
				  break;
			case "float":
				propType="float";
				break;
			case "double":
				propType="double";
				break;
			case "numeric":
			case "smallmoney":
			case "decimal":
			case "money":
				propType=  "decimal";
				 break;
			case "bit":
			case "bool":
			case "boolean":
				propType=  "bool";
				break;
			case "tinyint":
				propType =  bUnsigned ? "byte" : "sbyte";
				break;
			case "image":
			case "binary":
			case "blob":
			case "mediumblob":
			case "longblob":
			case "varbinary":
				propType=  "byte[]";
				 break;
				 
		}
		return propType;
	}

	const string TABLE_SQL=@"
			SELECT * 
			FROM information_schema.tables 
			WHERE (table_type='BASE TABLE' OR table_type='VIEW')
			";

}

class OracleSchemaReader : SchemaReader
{
	// SchemaReader.ReadSchema
	public override Tables ReadSchema(DbConnection connection, DbProviderFactory factory)
	{
		var result=new Tables();
		
		_connection=connection;
		_factory=factory;

		var cmd=_factory.CreateCommand();
		cmd.Connection=connection;
		cmd.CommandText=TABLE_SQL;
		cmd.GetType().GetProperty("BindByName").SetValue(cmd, true, null);

		//pull the tables in a reader
		using(cmd)
		{

			using (var rdr=cmd.ExecuteReader())
			{
				while(rdr.Read())
				{
					Table tbl=new Table();
					tbl.Name=rdr["TABLE_NAME"].ToString();
					tbl.Schema = rdr["TABLE_SCHEMA"].ToString();
					tbl.IsView=string.Compare(rdr["TABLE_TYPE"].ToString(), "View", true)==0;
					tbl.CleanName=CleanUp(tbl.Name);
					tbl.ClassName=Inflector.MakeSingular(tbl.CleanName);
					result.Add(tbl);
				}
			}
		}

		foreach (var tbl in result)
		{
			tbl.Columns=LoadColumns(tbl);
		            
			// Mark the primary key
			string PrimaryKey=GetPK(tbl.Name);
			var pkColumn=tbl.Columns.SingleOrDefault(x=>x.Name.ToLower().Trim()==PrimaryKey.ToLower().Trim());
			if(pkColumn!=null)
				pkColumn.IsPK=true;
		}
	    

		return result;
	}
	
	DbConnection _connection;
	DbProviderFactory _factory;
	

	List<Column> LoadColumns(Table tbl)
	{
	
		using (var cmd=_factory.CreateCommand())
		{
			cmd.Connection=_connection;
			cmd.CommandText=COLUMN_SQL;
			cmd.GetType().GetProperty("BindByName").SetValue(cmd, true, null);

			var p = cmd.CreateParameter();
			p.ParameterName = ":tableName";
			p.Value=tbl.Name;
			cmd.Parameters.Add(p);

			var result=new List<Column>();
			using (IDataReader rdr=cmd.ExecuteReader())
			{
				while(rdr.Read())
				{
					Column col=new Column();
					col.Name=rdr["ColumnName"].ToString();
					col.PropertyName=CleanUp(col.Name);
					col.PropertyType=GetPropertyType(rdr["DataType"].ToString(), (rdr["DataType"] == DBNull.Value ? null : rdr["DataType"].ToString()));
					col.IsNullable=rdr["IsNullable"].ToString()=="YES";
					col.IsAutoIncrement=true;
					result.Add(col);
				}
			}

			return result;
		}
	}

	string GetPK(string table){
		
		string sql=@"select column_name from USER_CONSTRAINTS uc
  inner join USER_CONS_COLUMNS ucc on uc.constraint_name = ucc.constraint_name
where uc.constraint_type = 'P'
and uc.table_name = upper(:tableName)
and ucc.position = 1";

		using (var cmd=_factory.CreateCommand())
		{
			cmd.Connection=_connection;
			cmd.CommandText=sql;
			cmd.GetType().GetProperty("BindByName").SetValue(cmd, true, null);

			var p = cmd.CreateParameter();
			p.ParameterName = ":tableName";
			p.Value=table;
			cmd.Parameters.Add(p);

			var result=cmd.ExecuteScalar();

			if(result!=null)
				return result.ToString();    
		}	         
		
		return "";
	}
	
	string GetPropertyType(string sqlType, string dataScale)
	{
		string sysType="string";
		switch (sqlType.ToLower()) 
		{
			case "bigint":
				sysType = "long";
				break;
			case "smallint":
				sysType= "short";
				break;
			case "int":
				sysType= "int";
				break;
			case "uniqueidentifier":
				sysType=  "Guid";
				 break;
			case "smalldatetime":
			case "datetime":
			case "date":
				sysType=  "DateTime";
				  break;
			case "float":
				sysType="double";
				break;
			case "real":
			case "numeric":
			case "smallmoney":
			case "decimal":
			case "money":
			case "number":
				sysType=  "decimal";
				 break;
			case "tinyint":
				sysType = "byte";
				break;
			case "bit":
				sysType=  "bool";
				   break;
			case "image":
			case "binary":
			case "varbinary":
			case "timestamp":
				sysType=  "byte[]";
				 break;
		}
		
		if (sqlType == "number" && dataScale == "0")
			return "long";
		
		return sysType;
	}



	const string TABLE_SQL=@"select TABLE_NAME, 'Table' TABLE_TYPE, USER TABLE_SCHEMA
from USER_TABLES
union all
select VIEW_NAME, 'View', USER
from USER_VIEWS";


	const string COLUMN_SQL=@"select table_name TableName, 
 column_name ColumnName, 
 data_type DataType, 
 data_scale DataScale,
 nullable IsNullable
 from USER_TAB_COLS utc 
 where table_name = :tableName
 order by column_id";
	  
}




/// <summary>
/// Summary for the Inflector class
/// </summary>
public static class Inflector {
    private static readonly List<InflectorRule> _plurals = new List<InflectorRule>();
    private static readonly List<InflectorRule> _singulars = new List<InflectorRule>();
    private static readonly List<string> _uncountables = new List<string>();

    /// <summary>
    /// Initializes the <see cref="Inflector"/> class.
    /// </summary>
    static Inflector() {
        AddPluralRule("$", "s");
        AddPluralRule("s$", "s");
        AddPluralRule("(ax|test)is$", "$1es");
        AddPluralRule("(octop|vir)us$", "$1i");
        AddPluralRule("(alias|status)$", "$1es");
        AddPluralRule("(bu)s$", "$1ses");
        AddPluralRule("(buffal|tomat)o$", "$1oes");
        AddPluralRule("([ti])um$", "$1a");
        AddPluralRule("sis$", "ses");
        AddPluralRule("(?:([^f])fe|([lr])f)$", "$1$2ves");
        AddPluralRule("(hive)$", "$1s");
        AddPluralRule("([^aeiouy]|qu)y$", "$1ies");
        AddPluralRule("(x|ch|ss|sh)$", "$1es");
        AddPluralRule("(matr|vert|ind)ix|ex$", "$1ices");
        AddPluralRule("([m|l])ouse$", "$1ice");
        AddPluralRule("^(ox)$", "$1en");
        AddPluralRule("(quiz)$", "$1zes");

        AddSingularRule("s$", String.Empty);
        AddSingularRule("ss$", "ss");
        AddSingularRule("(n)ews$", "$1ews");
        AddSingularRule("([ti])a$", "$1um");
        AddSingularRule("((a)naly|(b)a|(d)iagno|(p)arenthe|(p)rogno|(s)ynop|(t)he)ses$", "$1$2sis");
        AddSingularRule("(^analy)ses$", "$1sis");
        AddSingularRule("([^f])ves$", "$1fe");
        AddSingularRule("(hive)s$", "$1");
        AddSingularRule("(tive)s$", "$1");
        AddSingularRule("([lr])ves$", "$1f");
        AddSingularRule("([^aeiouy]|qu)ies$", "$1y");
        AddSingularRule("(s)eries$", "$1eries");
        AddSingularRule("(m)ovies$", "$1ovie");
        AddSingularRule("(x|ch|ss|sh)es$", "$1");
        AddSingularRule("([m|l])ice$", "$1ouse");
        AddSingularRule("(bus)es$", "$1");
        AddSingularRule("(o)es$", "$1");
        AddSingularRule("(shoe)s$", "$1");
        AddSingularRule("(cris|ax|test)es$", "$1is");
        AddSingularRule("(octop|vir)i$", "$1us");
        AddSingularRule("(alias|status)$", "$1");
        AddSingularRule("(alias|status)es$", "$1");
        AddSingularRule("^(ox)en", "$1");
        AddSingularRule("(vert|ind)ices$", "$1ex");
        AddSingularRule("(matr)ices$", "$1ix");
        AddSingularRule("(quiz)zes$", "$1");

        AddIrregularRule("person", "people");
        AddIrregularRule("man", "men");
        AddIrregularRule("child", "children");
        AddIrregularRule("sex", "sexes");
        AddIrregularRule("tax", "taxes");
        AddIrregularRule("move", "moves");

        AddUnknownCountRule("equipment");
        AddUnknownCountRule("information");
        AddUnknownCountRule("rice");
        AddUnknownCountRule("money");
        AddUnknownCountRule("species");
        AddUnknownCountRule("series");
        AddUnknownCountRule("fish");
        AddUnknownCountRule("sheep");
    }

    /// <summary>
    /// Adds the irregular rule.
    /// </summary>
    /// <param name="singular">The singular.</param>
    /// <param name="plural">The plural.</param>
    private static void AddIrregularRule(string singular, string plural) {
        AddPluralRule(String.Concat("(", singular[0], ")", singular.Substring(1), "$"), String.Concat("$1", plural.Substring(1)));
        AddSingularRule(String.Concat("(", plural[0], ")", plural.Substring(1), "$"), String.Concat("$1", singular.Substring(1)));
    }

    /// <summary>
    /// Adds the unknown count rule.
    /// </summary>
    /// <param name="word">The word.</param>
    private static void AddUnknownCountRule(string word) {
        _uncountables.Add(word.ToLower());
    }

    /// <summary>
    /// Adds the plural rule.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="replacement">The replacement.</param>
    private static void AddPluralRule(string rule, string replacement) {
        _plurals.Add(new InflectorRule(rule, replacement));
    }

    /// <summary>
    /// Adds the singular rule.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="replacement">The replacement.</param>
    private static void AddSingularRule(string rule, string replacement) {
        _singulars.Add(new InflectorRule(rule, replacement));
    }

    /// <summary>
    /// Makes the plural.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns></returns>
    public static string MakePlural(string word) {
        return ApplyRules(_plurals, word);
    }

    /// <summary>
    /// Makes the singular.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns></returns>
    public static string MakeSingular(string word) {
        return ApplyRules(_singulars, word);
    }

    /// <summary>
    /// Applies the rules.
    /// </summary>
    /// <param name="rules">The rules.</param>
    /// <param name="word">The word.</param>
    /// <returns></returns>
    private static string ApplyRules(IList<InflectorRule> rules, string word) {
        string result = word;
        if (!_uncountables.Contains(word.ToLower())) {
            for (int i = rules.Count - 1; i >= 0; i--) {
                string currentPass = rules[i].Apply(word);
                if (currentPass != null) {
                    result = currentPass;
                    break;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Converts the string to title case.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns></returns>
    public static string ToTitleCase(string word) {
        return Regex.Replace(ToHumanCase(AddUnderscores(word)), @"\b([a-z])",
            delegate(Match match) { return match.Captures[0].Value.ToUpper(); });
    }

    /// <summary>
    /// Converts the string to human case.
    /// </summary>
    /// <param name="lowercaseAndUnderscoredWord">The lowercase and underscored word.</param>
    /// <returns></returns>
    public static string ToHumanCase(string lowercaseAndUnderscoredWord) {
        return MakeInitialCaps(Regex.Replace(lowercaseAndUnderscoredWord, @"_", " "));
    }


    /// <summary>
    /// Adds the underscores.
    /// </summary>
    /// <param name="pascalCasedWord">The pascal cased word.</param>
    /// <returns></returns>
    public static string AddUnderscores(string pascalCasedWord) {
        return Regex.Replace(Regex.Replace(Regex.Replace(pascalCasedWord, @"([A-Z]+)([A-Z][a-z])", "$1_$2"), @"([a-z\d])([A-Z])", "$1_$2"), @"[-\s]", "_").ToLower();
    }

    /// <summary>
    /// Makes the initial caps.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns></returns>
    public static string MakeInitialCaps(string word) {
        return String.Concat(word.Substring(0, 1).ToUpper(), word.Substring(1).ToLower());
    }

    /// <summary>
    /// Makes the initial lower case.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns></returns>
    public static string MakeInitialLowerCase(string word) {
        return String.Concat(word.Substring(0, 1).ToLower(), word.Substring(1));
    }


    /// <summary>
    /// Determine whether the passed string is numeric, by attempting to parse it to a double
    /// </summary>
    /// <param name="str">The string to evaluated for numeric conversion</param>
    /// <returns>
    /// 	<c>true</c> if the string can be converted to a number; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsStringNumeric(string str) {
        double result;
        return (double.TryParse(str, NumberStyles.Float, NumberFormatInfo.CurrentInfo, out result));
    }

    /// <summary>
    /// Adds the ordinal suffix.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <returns></returns>
    public static string AddOrdinalSuffix(string number) {
        if (IsStringNumeric(number)) {
            int n = int.Parse(number);
            int nMod100 = n % 100;

            if (nMod100 >= 11 && nMod100 <= 13)
                return String.Concat(number, "th");

            switch (n % 10) {
                case 1:
                    return String.Concat(number, "st");
                case 2:
                    return String.Concat(number, "nd");
                case 3:
                    return String.Concat(number, "rd");
                default:
                    return String.Concat(number, "th");
            }
        }
        return number;
    }

    /// <summary>
    /// Converts the underscores to dashes.
    /// </summary>
    /// <param name="underscoredWord">The underscored word.</param>
    /// <returns></returns>
    public static string ConvertUnderscoresToDashes(string underscoredWord) {
        return underscoredWord.Replace('_', '-');
    }


    #region Nested type: InflectorRule

    /// <summary>
    /// Summary for the InflectorRule class
    /// </summary>
    private class InflectorRule {
        /// <summary>
        /// 
        /// </summary>
        public readonly Regex regex;

        /// <summary>
        /// 
        /// </summary>
        public readonly string replacement;

        /// <summary>
        /// Initializes a new instance of the <see cref="InflectorRule"/> class.
        /// </summary>
        /// <param name="regexPattern">The regex pattern.</param>
        /// <param name="replacementText">The replacement text.</param>
        public InflectorRule(string regexPattern, string replacementText) {
            regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
            replacement = replacementText;
        }

        /// <summary>
        /// Applies the specified word.
        /// </summary>
        /// <param name="word">The word.</param>
        /// <returns></returns>
        public string Apply(string word) {
            if (!regex.IsMatch(word))
                return null;

            string replace = regex.Replace(word, replacement);
            if (word == word.ToUpper())
                replace = replace.ToUpper();

            return replace;
        }
    }

    #endregion
}

public class Generator {
    private string GenerateTableHeader(Table tbl) {
	    var result = new StringBuilder();
	    result.AppendLine($"\t[TableName(\"{tbl.Name}\")]");

	    if (tbl.PK != null) {
		    result.Append($"\t[PrimaryKey(\"{tbl.PK.Name}\")");
		    result.Append(
			    tbl.PK!=null && tbl.PK.IsAutoIncrement && tbl.SequenceName!=null ?
					    $", sequenceName=\"{tbl.SequenceName}\""
				    :
					    "");
		    result.Append(
			    tbl.PK!=null && !tbl.PK.IsAutoIncrement ?
					    ", autoIncrement=false"
				    :
					    "");
		    result.AppendLine("]");
	    }
	    
	    return result.ToString();
    }

    private string GenerateTableFooter(Table tbl) {
	    return "\n\t}\n";
    }

    public string GenerateTableColumns(Table tbl, GeneratorSettings settings) {
	    return
		    $@"	[ExplicitColumns]
	    public partial class {tbl.ClassName} 
	    {{
    " +
		    string.Join("\n", 
			    tbl.Columns
				    .Where(c => !c.Ignore)
				    .Select(col => {
					    var name = col.Name!=col.PropertyName ? $"(\"{col.Name}\")" : "";
					    var common = $@"		[Column{name}] public {col.PropertyType}{(col.DomainIsNullable ? "?" : "")} {col.PropertyName}";

					    if (!settings.TrackModifiedColumns) {
						    return common + " { get; set; }";
					    }
					    
					    return common + $@"
		    {{ 
			    get
			    {{
				    return _{col.PropertyName};
			    }}
			    set
			    {{
				    _{col.PropertyName} = value;
				    MarkColumnModified(""{col.Name}"");
			    }}
		    }}
		    {col.PropertyType}{(col.DomainIsNullable ? "?" : "")} _{col.PropertyName};
    "; 
				    }));
    }

    public void GenerateCode(GeneratorSettings settings) {
	    File.WriteAllText(
		    settings.CsFile, 
		    $@"
    // This file was automatically generated by the PocoSchemaCodeGeneration.ForAsyncPoco
    // Do not make changes directly to this file - edit the template instead
    // The following connection settings were used to generate this file

    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web;
    using AsyncPoco;

    namespace {settings.Namespace}
    {{
    " +
	    string.Join("\n", 
            SchemaReader.LoadTables(settings)
			    .OrderBy(x => x.Name)
			    .Where(x => !x.Ignore)
			    .Select(tbl => GenerateTableHeader(tbl) + GenerateTableColumns(tbl, settings) + GenerateTableFooter(tbl) )	) +
	    @"}
    ");
    }
}
