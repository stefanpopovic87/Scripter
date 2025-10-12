using DbUp.Engine;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Scripter
{
	public class CustomJournal : IJournal
	{
		private readonly string schema;
		private readonly string tableName;
		private readonly string baseFolder;
		private readonly string connectionString;

		public CustomJournal(string schema, string tableName, string baseFolder, string connectionString)
		{
			// e.g. schema = "scripts", tableName = "SchemaVersions"
			this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
			this.tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
			this.baseFolder = baseFolder ?? string.Empty;
			this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
		}

		/// <summary>
		/// Returns keys of executed scripts in the form "fullFolderPath||fileName".
		/// </summary>
		public string[] GetExecutedScripts()
		{
			EnsureTableExistsAndIsLatestVersion(CreateCommand);

			var list = new List<string>();
			using var cmd = CreateCommand();
			cmd.CommandText = $"SELECT [Path],[ScriptName] FROM [{Q(schema)}].[{Q(tableName)}]";

			using var r = cmd.ExecuteReader();
			while (r.Read())
			{
				var folder = r.IsDBNull(0) ? string.Empty : (string)r[0];
				var file = r.IsDBNull(1) ? string.Empty : (string)r[1];

				// Build the ScriptKey as "folder||filename"
				var key = ScriptKey.Make(Path.GetFullPath(folder), file);
				list.Add(key);
			}
			return list.ToArray();
		}

		/// <summary>
		/// Persists a script row as executed (UTC time, user/machine filled by SQL).
		/// </summary>
		public void StoreExecutedScript(SqlScript script, Func<IDbCommand> dbCommandFactory)
		{
			EnsureTableExistsAndIsLatestVersion(dbCommandFactory);

			// script.Name is "<fullFolderPath>||<fileName>"
			var (folderFull, fileName) = ScriptKey.Split(script.Name);
			folderFull = Path.GetFullPath(folderFull);

			using var cmd = dbCommandFactory();
			cmd.CommandText =
				$"INSERT INTO [{Q(schema)}].[{Q(tableName)}] " +
				"([ScriptName],[Applied],[Path],[ExecutedBy],[MachineName]) " +
				"VALUES (@name, @appliedUtc, @path, SUSER_SNAME(), HOST_NAME());";

			AddParam(cmd, "@name", fileName);
			AddParam(cmd, "@appliedUtc", DateTime.UtcNow); // store in UTC
			AddParam(cmd, "@path", folderFull);

			cmd.ExecuteNonQuery();
		}

		/// <summary>
		/// Ensures the journal table exists and is in the expected shape (idempotent).
		/// - Creates schema if missing
		/// - Creates table if missing
		/// - Adds/normalizes columns and defaults (Path, Applied as DATETIME2 with UTC default)
		/// </summary>
		public void EnsureTableExistsAndIsLatestVersion(Func<IDbCommand> dbCommandFactory)
		{
			using var cmd = dbCommandFactory();
			cmd.CommandText = $@"
				-- 1) Create schema if it does not exist (e.g. [scripts])
				IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'{schema}')
					EXEC('CREATE SCHEMA [{Q(schema)}] AUTHORIZATION [dbo]');

				-- 2) Create table if it does not exist
				IF NOT EXISTS (SELECT * FROM sys.objects 
				              WHERE object_id = OBJECT_ID(N'[{Q(schema)}].[{Q(tableName)}]') AND type = N'U')
				BEGIN
					CREATE TABLE [{Q(schema)}].[{Q(tableName)}](
						[Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
						[ScriptName]  NVARCHAR(255) NOT NULL,  -- file name only
						[Applied]     DATETIME2 NOT NULL CONSTRAINT DF_{Q(tableName)}_Applied DEFAULT (SYSUTCDATETIME()), -- UTC
						[Path]        NVARCHAR(2048) NULL,     -- absolute folder path
						[ExecutedBy]  NVARCHAR(128) NULL CONSTRAINT DF_{Q(tableName)}_ExecutedBy DEFAULT (SUSER_SNAME()),
						[MachineName] NVARCHAR(128) NULL CONSTRAINT DF_{Q(tableName)}_Machine DEFAULT (HOST_NAME())
					);
				END
				ELSE
				BEGIN
					-- Ensure 'Path' column exists
					IF COL_LENGTH('{schema}.{tableName}', 'Path') IS NULL
						ALTER TABLE [{Q(schema)}].[{Q(tableName)}] ADD [Path] NVARCHAR(2048) NULL;

					-- Normalize 'Applied' to DATETIME2 (UTC-friendly)
					IF EXISTS (SELECT 1
							   FROM sys.columns c
							   WHERE c.object_id = OBJECT_ID('{schema}.{tableName}')
								 AND c.name = 'Applied'
								 AND c.system_type_id <> TYPE_ID('datetime2'))
					BEGIN
						ALTER TABLE [{Q(schema)}].[{Q(tableName)}] ALTER COLUMN [Applied] DATETIME2 NOT NULL;
					END

					-- Ensure UTC default for 'Applied' exists
					IF NOT EXISTS (
						SELECT 1
						FROM sys.default_constraints dc
						JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
						WHERE dc.parent_object_id = OBJECT_ID('{schema}.{tableName}')
						  AND c.name = 'Applied'
					)
					BEGIN
						ALTER TABLE [{Q(schema)}].[{Q(tableName)}] 
						ADD CONSTRAINT DF_{Q(tableName)}_Applied DEFAULT (SYSUTCDATETIME()) FOR [Applied];
					END
				END";
			cmd.ExecuteNonQuery();
		}

		/// <summary>
		/// Creates and opens a new SQL command bound to a fresh SqlConnection.
		/// Caller is responsible for disposing the returned command.
		/// </summary>
		private IDbCommand CreateCommand()
		{
			var conn = new SqlConnection(connectionString);
			conn.Open();
			return conn.CreateCommand();
		}

		// --- helpers ----------------------------------------------------------

		// Brackets an identifier safely: foo]bar -> foo]]bar, then wraps in []
		private static string Q(string identifier) => identifier.Replace("]", "]]");

		private static void AddParam(IDbCommand cmd, string name, object? value)
		{
			var p = cmd.CreateParameter();
			p.ParameterName = name;
			p.Value = value ?? DBNull.Value;
			cmd.Parameters.Add(p);
		}
	}
}
