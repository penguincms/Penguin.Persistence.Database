declare @sql varchar(max) = ''

SET NOCOUNT ON
DECLARE @table TABLE(
RowId INT PRIMARY KEY IDENTITY(1, 1),
ForeignKeyConstraintName NVARCHAR(200),
ForeignKeyConstraintTableSchema NVARCHAR(200),
ForeignKeyConstraintTableName NVARCHAR(200),
ForeignKeyConstraintColumnName NVARCHAR(200),
PrimaryKeyConstraintName NVARCHAR(200),
PrimaryKeyConstraintTableSchema NVARCHAR(200),
PrimaryKeyConstraintTableName NVARCHAR(200),
PrimaryKeyConstraintColumnName NVARCHAR(200)
)
--------------------------------------------

DropKeys:

	INSERT INTO @table(ForeignKeyConstraintName, ForeignKeyConstraintTableSchema, ForeignKeyConstraintTableName, ForeignKeyConstraintColumnName)
	SELECT
	U.CONSTRAINT_NAME,
	U.TABLE_SCHEMA,
	U.TABLE_NAME,
	U.COLUMN_NAME
	FROM
	INFORMATION_SCHEMA.KEY_COLUMN_USAGE U
	INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS C
	ON U.CONSTRAINT_NAME = C.CONSTRAINT_NAME
	WHERE
	C.CONSTRAINT_TYPE = 'FOREIGN KEY'
	UPDATE @table SET
	PrimaryKeyConstraintName = UNIQUE_CONSTRAINT_NAME
	FROM
	@table T
	INNER JOIN INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS R
	ON T.ForeignKeyConstraintName = R.CONSTRAINT_NAME
	UPDATE @table SET
	PrimaryKeyConstraintTableSchema = TABLE_SCHEMA,
	PrimaryKeyConstraintTableName = TABLE_NAME
	FROM @table T
	INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS C
	ON T.PrimaryKeyConstraintName = C.CONSTRAINT_NAME
	UPDATE @table SET
	PrimaryKeyConstraintColumnName = COLUMN_NAME
	FROM @table T
	INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE U
	ON T.PrimaryKeyConstraintName = U.CONSTRAINT_NAME
	--SELECT * FROM @table
	--DROP CONSTRAINT:
	SELECT @sql = @sql + 
	'
	ALTER TABLE [' + ForeignKeyConstraintTableSchema + '].[' + ForeignKeyConstraintTableName + ']
	DROP CONSTRAINT [' + ForeignKeyConstraintName + ']' + char(13)
	FROM
	@table

	Exec (@sql)
	select @sql

	

	delete from @table

if (len(@sql) > 0)
BEGIN
set @sql = ''
	Print 'Dropping...'
	GOTO DropKeys
END
set @sql = ''




SELECT @sql += ' Drop table ' + QUOTENAME(TABLE_SCHEMA) + '.'+ QUOTENAME(TABLE_NAME) + '; '
FROM   INFORMATION_SCHEMA.TABLES
WHERE  TABLE_TYPE = 'BASE TABLE'


Exec (@sql)

DECLARE @name VARCHAR(128), @sqlCommand NVARCHAR(1000), @Rows INT = 0, @i INT = 1;
DECLARE @t TABLE(RowID INT IDENTITY(1,1), ObjectName VARCHAR(128));
 
INSERT INTO @t(ObjectName)
SELECT s.[SCHEMA_NAME] FROM INFORMATION_SCHEMA.SCHEMATA s
WHERE s.[SCHEMA_NAME] NOT IN('dbo', 'guest', 'INFORMATION_SCHEMA', 'sys', 'db_owner', 'db_accessadmin', 'db_securityadmin', 'db_ddladmin', 'db_backupoperator', 'db_datareader', 'db_datawriter', 'db_denydatareader', 'db_denydatawriter')
 
SELECT @Rows = (SELECT COUNT(RowID) FROM @t), @i = 1;
 
WHILE (@i <= @Rows) 
BEGIN
    SELECT @sqlCommand = 'DROP SCHEMA [' + t.ObjectName + '];', @name = t.ObjectName FROM @t t WHERE RowID = @i;
    EXEC sp_executesql @sqlCommand;        
    PRINT 'Dropped SCHEMA: [' + @name + ']';    
    SET @i = @i + 1;
END