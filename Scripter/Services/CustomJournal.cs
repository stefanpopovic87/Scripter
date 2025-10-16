using DbUp.Engine;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;

namespace Scripter.Services
{
	public class CustomJournal : IJournal
	{
		private readonly string schema;
		private readonly string tableName;
		private readonly string baseFolder;
		private readonly string connectionString;

		public CustomJournal(string schema, string tableName, string baseFolder, string connectionString)
		{
			this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
			this.tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
			this.baseFolder = baseFolder ?? string.Empty;
			this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
		}

		/// <summary>
		/// Vraća ključeve izvršenih skripti u formi "fullFolderPath||fileName".
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

				var key = ScriptKey.Make(Path.GetFullPath(folder), file);
				list.Add(key);
			}
			return list.ToArray();
		}

		/// <summary>
		/// Upisuje red u dnevnik (UTC vreme, user/machine popunjava SQL) + puni originalni sadržaj skripte.
		/// </summary>
		public void StoreExecutedScript(SqlScript script, Func<IDbCommand> dbCommandFactory)
		{
			EnsureTableExistsAndIsLatestVersion(dbCommandFactory);

			// script.Name je "<fullFolderPath>||<fileName>"
			var (folderFull, fileName) = ScriptKey.Split(script.Name);
			folderFull = Path.GetFullPath(folderFull);

			// PROČITATI ORIGINALNI FAJL — sa BOM auto-detekcijom — bez ikakve normalizacije
			string content = script.Contents ?? string.Empty;
			try
			{
				var fullPath = Path.Combine(folderFull, fileName);
				if (File.Exists(fullPath))
				{
					// BOM auto-detect + UTF8 fallback (ne menjamo \r\n)
					using var reader = new StreamReader(
						fullPath,
						new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
						detectEncodingFromByteOrderMarks: true);
					content = reader.ReadToEnd();
				}
			}
			catch
			{
				// Fallback ostaje script.Contents; ne dižemo izuzetak da izvršavanje ne stane.
			}

			using var cmd = dbCommandFactory();
			cmd.CommandText =
				$"INSERT INTO [{Q(schema)}].[{Q(tableName)}] " +
				"([ScriptName],[Applied],[Path],[ExecutedBy],[MachineName],[Content]) " +
				"VALUES (@name, @appliedUtc, @path, SUSER_SNAME(), HOST_NAME(), @content);";

			AddParam(cmd, "@name", fileName);
			AddParam(cmd, "@appliedUtc", DateTime.UtcNow); // čuvamo u UTC
			AddParam(cmd, "@path", folderFull);
			AddNVarCharMax(cmd, "@content", content);

			cmd.ExecuteNonQuery();
		}

		/// <summary>
		/// Obavezno kreira/aktualizuje šemu i tabelu (idempotentno).
		/// </summary>
		public void EnsureTableExistsAndIsLatestVersion(Func<IDbCommand> dbCommandFactory)
		{
			using var cmd = dbCommandFactory();
			cmd.CommandText = $@"
				-- 1) Shema
				IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'{schema}')
					EXEC('CREATE SCHEMA [{Q(schema)}] AUTHORIZATION [dbo]');

				-- 2) Tabela
				IF NOT EXISTS (SELECT * FROM sys.objects 
				               WHERE object_id = OBJECT_ID(N'[{Q(schema)}].[{Q(tableName)}]') AND type = N'U')
				BEGIN
					CREATE TABLE [{Q(schema)}].[{Q(tableName)}](
						[Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
						[ScriptName]  NVARCHAR(255) NOT NULL,  -- file name only
						[Applied]     DATETIME2 NOT NULL CONSTRAINT DF_{Q(tableName)}_Applied DEFAULT (SYSUTCDATETIME()), -- UTC
						[Path]        NVARCHAR(2048) NULL,     -- absolute folder path
						[ExecutedBy]  NVARCHAR(128) NULL CONSTRAINT DF_{Q(tableName)}_ExecutedBy DEFAULT (SUSER_SNAME()),
						[MachineName] NVARCHAR(128) NULL CONSTRAINT DF_{Q(tableName)}_Machine   DEFAULT (HOST_NAME()),
						[Content]     NVARCHAR(MAX) NULL       -- FULL original SQL text
					);
				END
				ELSE
				BEGIN
					-- Path kolona
					IF COL_LENGTH('{schema}.{tableName}', 'Path') IS NULL
						ALTER TABLE [{Q(schema)}].[{Q(tableName)}] ADD [Path] NVARCHAR(2048) NULL;

					-- Applied kao DATETIME2
					IF EXISTS (SELECT 1
					           FROM sys.columns c
					           WHERE c.object_id = OBJECT_ID('{schema}.{tableName}')
					             AND c.name = 'Applied'
					             AND c.system_type_id <> TYPE_ID('datetime2'))
					BEGIN
						ALTER TABLE [{Q(schema)}].[{Q(tableName)}] ALTER COLUMN [Applied] DATETIME2 NOT NULL;
					END

					-- Default za UTC Applied
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

					-- Content kolona
					IF COL_LENGTH('{schema}.{tableName}', 'Content') IS NULL
						ALTER TABLE [{Q(schema)}].[{Q(tableName)}] ADD [Content] NVARCHAR(MAX) NULL;
				END";
			cmd.ExecuteNonQuery();
		}

		private IDbCommand CreateCommand()
		{
			var conn = new SqlConnection(connectionString);
			conn.Open();
			return conn.CreateCommand();
		}

		// --- helpers ----------------------------------------------------------

		private static string Q(string identifier) => identifier.Replace("]", "]]");

		private static void AddParam(IDbCommand cmd, string name, object? value)
		{
			var p = cmd.CreateParameter();
			p.ParameterName = name;
			p.Value = value ?? DBNull.Value;
			cmd.Parameters.Add(p);
		}

		private static void AddNVarCharMax(IDbCommand cmd, string name, string value)
		{
			var sp = new SqlParameter(name, SqlDbType.NVarChar)
			{
				Size = -1, // NVARCHAR(MAX)
				Value = (object?)value ?? string.Empty
			};
			cmd.Parameters.Add(sp);
		}
	}
}
