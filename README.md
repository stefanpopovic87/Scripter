Scripter is a lightweight Windows Forms (.NET) application designed to manage and execute SQL migration scripts in a controlled, visual, and repeatable way.
It simplifies database versioning by allowing developers to easily load, review, and run pending .sql files directly from a selected folder all while keeping a reliable record of executed migrations in the database.

**Features**

Automatic script discovery
Scans the selected folder (and its subfolders) for .sql scripts and lists them in a structured view.

Execution tracking
Uses a DbMigrationHistory table to keep a record of all executed migrations, ensuring each script runs only once.

Run pending scripts only
Executes all new or modified scripts in chronological order (based on creation time or file name).

Non-intrusive design
Does not modify existing scripts or folders — simply reads and executes.

Simple, intuitive UI
A clean interface with immediate feedback through icons and status messages (Pending, Executed, Error).

Built-in logging
Logs every operation in real-time directly within the application interface.

No external dependencies
Uses DbUp internally for database migrations, without requiring any additional setup.

**Technology Stack**

C# (.NET 8.0)
WinForms for the desktop interface
DbUp for script execution and migration tracking
Microsoft.Data.SqlClient for SQL Server connectivity

**How It Works**

Connection
Provide your SQL Server connection string (e.g.
Data Source=localhost; Initial Catalog=MyDatabase; User Id=sa; Password=Password1*; TrustServerCertificate=True).

Script Folder
Select the directory containing your .sql files. The app will automatically list all found scripts.

Load Scripts
Click “Load Scripts” — executed and pending scripts will be detected and displayed in the grid.

Run Pending
Click “Run Pending” to execute all unexecuted scripts in order.
The results are immediately reflected in both the table and the database tracking table.
