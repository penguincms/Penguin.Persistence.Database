--@using ConnectionStrings.Reporting
--@using DefaultConnectionString
CREATE PROCEDURE _GetParamDefault
@Procname varchar(50),
@ProcParamName varchar(50)
as
/*
This procedure will return DEFAULT value for 
the parameter in thestored procedure.
Usage:
declare @Value varchar(30)
exec _GetParamDefault 'random_password','@password_type',
                                            @value OUTPUT
SELECT @VALUE

*****************************************************
Author: Eva Zadoyen
Edited: Rafael Mizrahi
*/

set nocount on
declare @sqlstr nvarchar(4000),
@obj_id int,
@version int,
@text varchar(8000),
@startPos int,
@endPos int,
@ParmDefinition NVARCHAR(500),
@DefaultValue varchar(100)

select @procName = rtrim(ltrim(@procname))
set @startPos= charindex(';',@Procname)

if @startPos<>0
begin
set @version = substring(@procname,@startPos +1,1)
set @procname = left(@procname,len(@procname)-2)
end
else
set @version = 1

SET @sqlstr =N'SELECT @text_OUT = (SELECT text FROM syscomments
WHERE id = object_id(@p_name) and colid=1 and number = @vers)'
SET @ParmDefinition = N'@p_name varchar(50),
@ParamName varchar (50),
@vers int,
@text_OUT varchar(4000) OUTPUT'

EXEC sp_executesql
@SQLStr,
@ParmDefinition,
@p_name = @procname,
@ParamName = @ProcParamName,
@vers = @version,
@text_OUT =@text OUTPUT

--select @TEXT
select @startPos = PATINDEX( '%' + @ProcParamName +'%',@text)
if @startPos<>0
begin
select @text = RIGHT ( @text, len(@text)-(@startPos +1))
select @endPos= CHARINDEX(char(10),@text) -- find the end of a line
select @text = LEFT(@text,@endPos-1)
-- check if there is a default assigned and 
-- parse the value to theoutput
select @startPos= PATINDEX('%=%',@text)
if @startPos <>0
begin
select @DefaultValue = 
     ltrim(rtrim(right(@text,len(@text)-(@startPos))))
select @endPos= CHARINDEX('--',@DefaultValue)
if @endPos <> 0
select @DefaultValue = rtrim(left(@DefaultValue,@endPos-1))

select @endPos= CHARINDEX(',',@DefaultValue)
if @endPos <> 0
select @DefaultValue = rtrim(left(@DefaultValue,@endPos-1))
end
ELSE
select @DefaultValue = 'NO DEFAULT SPECIFIED'
end
else
SET @DefaultValue = 'INVALID PARAM NAME'

select @DefaultValue

set nocount off
return
GO