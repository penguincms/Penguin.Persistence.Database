--@using ConnectionStrings.Reporting
--@using DefaultConnectionString
CREATE PROCEDURE _GetAllProcedures 
AS 
SET NOCOUNT ON 
select sysobjects.name,syscolumns.name from sysobjects, syscolumns 
where 
sysobjects.xtype='P' and 
sysobjects.id = syscolumns.id 
RETURN 
GO