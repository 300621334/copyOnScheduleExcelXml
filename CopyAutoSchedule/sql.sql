--cannot declare a @var and assign tbl name to it then use that @var in FROM clause
--must have WHOLE query as a string and assign to a var, then EXEC(@var); brackets must
--

Use CentralContact;
SET NOCOUNT ON;

declare @sql nvarchar(max), @currentMo nvarchar(2), @ifMonthChanged nvarchar(max), @oneDayAgo nvarchar(max);

set @currentMo = DATEPART(M, GETDATE());

set @oneDayAgo = convert(nvarchar , DATEADD(DAY, -30, GETDATE()) , 25) --format 25 is '2017-01-01 00:00:00:000'


set @ifMonthChanged =
case
	when month(DATEADD(DAY, -13 , GETDATE())) = month(GETDATE())
	then ' '
	else ' union all  select * from dbo.sessions_month_' + cast(month(DATEADD(DAY, -13 , GETDATE())) as nvarchar)
end

--print @currentMo
--print @oneDayAgo
--print @ifMonthChanged


set @sql = '
select distinct "Paths" = ''\\''
     +CASE
          WHEN sm3.audio_module_no = 871001 THEN ''SE104421\h$\Calls\''
          WHEN sm3.audio_module_no = 871002 THEN ''SE104422\h$\Calls\''
          WHEN sm3.audio_module_no = 871003 THEN ''SE104426\h$\Calls\''
          WHEN sm3.audio_module_no = 871004 THEN ''SE104427\h$\Calls\''
          END+

        CAST(sm3.audio_module_no AS nvarchar)
        + ''\''
        +SUBSTRING(REPLICATE(''0'', 9-LEN(sm3.audio_ch_no))+ CAST(sm3.audio_ch_no AS varchar) ,1, 3)
        + ''\''
        +SUBSTRING(REPLICATE(''0'', 9-LEN(sm3.audio_ch_no))+ CAST(sm3.audio_ch_no AS varchar) ,4, 2)
        + ''\''
        +SUBSTRING(REPLICATE(''0'', 9-LEN(sm3.audio_ch_no))+ CAST(sm3.audio_ch_no AS varchar) ,6, 2)
        + ''\''
        + CAST(sm3.audio_module_no AS nvarchar) +REPLICATE(''0'', 9-LEN(sm3.audio_ch_no))+ CAST(sm3.audio_ch_no AS varchar)
        + ''.wav''
        --,RTRIM(sm3.start_time) as start_time
        --,sm3.start_time

       
from (select * from dbo.Sessions_month_'+@currentMo+@ifMonthChanged+') sm3  
     
     

where sm3.start_time > '''+@oneDayAgo+'''
';

exec (@sql);
