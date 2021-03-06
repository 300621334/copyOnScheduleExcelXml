﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
/*NOTEs:
 * (1) To hide app, r-clk project >> Application >> Output Type >> change from console to Windows_Application.
 * Aft that console doesn't show up & hence NO need to run the other project "CopyAutoSchedule_StartProcess"
 * 
 * (2)Write to an Excel file: http://csharp.net-informations.com/excel/csharp-create-excel.htm
 */
namespace CopyAutoSchedule
{
    class Program
    {
        #region Variables
        static string pathListFile = "", csvFileName = "";
        static int linesRead = 0, counter = 0, missingFiles = 0, maxLogSizeInKBs = 1, keepCallListsForDays = 7;
        static string logFile="Logs.txt", newFile = "", xmlOriginFile = "", xmlDestinFile = "", folder = "Copied_Files" /*folder = @"\\SE104421\h$\Test"*/
            , FQDN = "SE104499.saimaple.saifg.rbc.com", database = "CentralContact", CallsForHowManyDaysBack = "-1", categoryName="",
            addCatColOnlyIfTranscribed="", sqlJOINforInstances = "", sqlWHEREforInstances = "";
        static string[] configTxt;
        static bool copyXml = true, excelNeeded = true;
        static List<int> instanceIdsList = new List<int>();
        #endregion

        static void Main(string[] args)
        {
            configFile();
            trimLogFile();
            deleteOlderPathLists();
            sqlFileCreate();
            string sqlFilePath = Path.Combine(Application.StartupPath, @"sql.sql");
            string sql = File.ReadAllText(sqlFilePath );
            string connStr = connStrGet();
            //string connStr = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=Movies;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=True;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";
            getPathsIntoFile(sql, connStr);
            startCopy();
        }

        private static void configFile()
        {
            if(!File.Exists("_config.txt"))
            {

                string configFileTxt =
                "DB Server FQDN: SE104499.saimaple.saifg.rbc.com" + System.Environment.NewLine
                    //+ "Database Name:	CentralContact" + System.Environment.NewLine
                + "CallsForHowManyDaysBack: 1" + System.Environment.NewLine
                + "Destination Folder: Copied_Files" + System.Environment.NewLine
                + "Want To Copy XML?: no" + System.Environment.NewLine
                + "Max Log Size (KB): 1000" + System.Environment.NewLine
                + "Keep pathsLists For Days: 7" + System.Environment.NewLine
                + "Excel Meta-Data File?: yes" + System.Environment.NewLine
                + "instance_id comma separated list: 871102,871100" + System.Environment.NewLine
                + "category_name must contain: credit" + System.Environment.NewLine;


                File.WriteAllText("_config.txt", configFileTxt, Encoding.UTF8);
                Environment.Exit(0);//close app aft creating config.txt template
            }
            else
            {
                configTxt = File.ReadAllLines("_config.txt");
                foreach(string line in configTxt)
                {
                    if(Regex.Match(line, "DB Server FQDN").Success)
                    {
                        var match = Regex.Match(line, "[:;]");
                        FQDN = line.Substring(match.Index + 1).Trim();
                    }
                    //if (Regex.Match(line, "Database Name").Success)
                    //{
                    //    var match = Regex.Match(line, "[:;]");
                    //    database = line.Substring(match.Index + 1).Trim();
                    //}
                    if (Regex.Match(line, "CallsForHowManyDaysBack").Success)
                    {
                        var match = Regex.Match(line, "[:;]");
                        string daysSelected = line.Substring(match.Index + 1).Trim();
                        //For now I am limiting to 7 days
                        CallsForHowManyDaysBack = "-" + freezeDaysToLastMoStart(daysSelected);  //add minus sign so days get substracted
                    }
                    if (Regex.Match(line, "Destination Folder").Success)
                    {
                        var match = Regex.Match(line, "[:;]");
                        folder = line.Substring(match.Index + 1).Trim();
                        if (!(folder.Substring(0, 2)=="\\\\") && !Directory.Exists(folder))
                        {
                            Directory.CreateDirectory(folder);
                        }
                    }
                    if (Regex.Match(line, "Want To Copy XML?").Success)
                    {
                        var match = Regex.Match(line, "[:;]");
                        copyXml = (line.Substring(match.Index + 1).Trim().ToUpper() =="YES")?true:false;  //add minus sign so days get substracted
                    }
                    if (Regex.Match(line, "Max Log Size (KB)").Success)
                    {
                        var match = Regex.Match(line, "[:;]");
                        maxLogSizeInKBs = Convert.ToInt32(line.Substring(match.Index + 1).Trim());
                    }
                    if (Regex.Match(line, "Keep pathsLists For Days").Success)
                    {
                        var match = Regex.Match(line, "[:;]");
                        keepCallListsForDays = Convert.ToInt32( "-" + line.Substring(match.Index + 1).Trim() );
                    }
                    if (Regex.Match(line, "Excel Meta-Data File?").Success)
                    {
                        var match = Regex.Match(line, "[:;]");
                        excelNeeded = (line.Substring(match.Index + 1).Trim().ToUpper() == "YES") ? true : false;  //add minus sign so days get substracted
                    }
                    if (Regex.Match(line, "instance_id comma separated list").Success)
                    {
                        var match = Regex.Match(line, "[:;]");
                        foreach(string instance in line.Substring(match.Index + 1).Trim().Split(',').Where(a => !string.IsNullOrWhiteSpace(a.Trim())))
                        {
                            instanceIdsList.Add(Convert.ToInt32(instance) );
                        }
                        
                    }
                    if (Regex.Match(line, "category_name must contain").Success)
                    {
                        var match = Regex.Match(line, "[:;]");
                         categoryName = line.Substring(match.Index + 1).Trim();
                    }
                }
            }

        }

        private static string freezeDaysToLastMoStart(string daysSelected)
        {//https://stackoverflow.com/questions/591752/get-the-previous-months-first-and-last-day-dates-in-c-sharp

            //DateTime LastMonthLastDate = DateTime.Today.AddDays(0 - DateTime.Today.Day);
            //DateTime LastMonthFirstDate = LastMonthLastDate.AddDays(1 - LastMonthLastDate.Day);

            //if (DateTime.Today.AddDays(Convert.ToInt32("-" + daysSelected)) < LastMonthFirstDate)
            //{
            //    daysSelected = (DateTime.Today.Day + (LastMonthLastDate.Day - LastMonthFirstDate.Day)).ToString();
            //    return daysSelected; //SQL replace value of daysSelected to chk if freezes at lastMoStart or not [[select convert(nvarchar , DATEADD(DAY, -45 , GETDATE()) , 25)]]
            //}
            //return daysSelected;

            return Convert.ToInt32(daysSelected) > 62 ? "62" : daysSelected ;//in production change this to ~7 etc
        }

        private static void trimLogFile()
        {//https://stackoverflow.com/questions/1515097/how-do-i-delete-the-first-x-lines-of-a-text-file  //https://stackoverflow.com/questions/1380839/how-do-you-get-the-file-size-in-c
            
            if (File.Exists(logFile))
            {
                long logFileSizeInBytes = new FileInfo(logFile).Length;
                if(logFileSizeInBytes > (1000 * maxLogSizeInKBs))
                {
                    long linesToDelete = (logFileSizeInBytes/100);//assuming ea line is ~50bytes on average, so delete half of all lines = 50*2=100
                    string line = null;
                    string tempLogFile = "tempLogs.txt"; //= Path.GetTempFileName();//this creates a temp file in User\...\AppData\Local\Temp\tempfile.tmp
                    
                    using (StreamReader reader = new StreamReader(logFile))
                    using(StreamWriter writer = new StreamWriter(tempLogFile))
                    {
                        while (linesToDelete-- > 0) line = reader.ReadLine();//don't do anything to first few lines so they get lost
                        while ((line = reader.ReadLine()) != null) writer.WriteLine(line);//write remaining lines to tempLogFile
                    }
                    
                    //replace original larger file with new smaller log file
                    if(File.Exists(tempLogFile))
                    {
                        File.Delete(logFile);
                        File.Move(tempLogFile, logFile);
                    }

                }
            }
        }

        private static void deleteOlderPathLists()
        {
            DateTime keepListsTillDate = DateTime.Now.AddDays(keepCallListsForDays);//.AddMinutes(-1);
            string[] pathsListArr = Directory.GetFiles(Directory.GetCurrentDirectory(), "pathsList *.txt");//al files like "pathsList 13-Sep-2017.txt"
            foreach (string pathsList in pathsListArr.Where(x => File.GetCreationTime(x)/*File.GetLastWriteTime(x)*/ < keepListsTillDate))
            {
                File.Delete(pathsList);
            }
        }

        private static void sqlFileCreate()
        {
            #region SQL for instance_id added
            //if only transcribed calls needed, then MUST specify instance_id(s) and optionallt a category_name
            //otherwise ALL calls will be copied, whether transcribed or not
            //if at least one instance_id is specified, then add following to SQL query
            if (instanceIdsList.Any())//if list of instance_id is NOT empty, add more to SQL
            {
                addCatColOnlyIfTranscribed = " , cat.category_name ";

                sqlJOINforInstances = "JOIN [CentralDWH].[dbo].[Sessions_categories] sc on sc.sid=sm3.sid " //+ Environment.NewLine
                    + " JOIN [CentralDWH].[dbo].[Categories] cat ON sc.category_id=cat.category_id";

                sqlWHEREforInstances = "AND sc.instance_id IN (" + string.Join(",", instanceIdsList) + ")" //+ Environment.NewLine //https://stackoverflow.com/questions/799446/creating-a-comma-separated-list-from-iliststring-or-ienumerablestring
                    + " AND cat.category_name LIKE  '%" + categoryName + "%'";
            }
            #endregion

            //if sql.sql does NOT xists, generate one as a template
            if(!File.Exists("sql.sql"))
            {
#region SQL Query
 string sqlQuery = 
 "--Cannot declare a @var and assign tbl name to it then use that @var in FROM clause"+Environment.NewLine
 +@"--must have WHOLE query as a string and assign to a var, then EXEC(@var); brackets must"+Environment.NewLine
 +Environment.NewLine
  
 +@"Use CentralDWH;"+Environment.NewLine
 +@"SET NOCOUNT ON;"+Environment.NewLine

 + @"declare @now datetime, @sql nvarchar(max), @currentMo nvarchar(2), @ifMonthChanged nvarchar(max), @oneDayAgo nvarchar(max), @addCatColOnlyIfTranscribed_var nvarchar(max), @sqlJOINforInstances_var nvarchar(max), @sqlWHEREforInstances_var nvarchar(max) ;" + Environment.NewLine
 
 + @"--declare @CallsForHowManyDaysBack nvarchar(3) = -45; --if run sql directly in SSMS then use this" + Environment.NewLine
 + @"set @now = GETDATE();" + Environment.NewLine
 +@"set @currentMo = DATEPART(M, @now);"+Environment.NewLine

 + @"set @addCatColOnlyIfTranscribed_var = @addCatColOnlyIfTranscribed;" + Environment.NewLine
 + @"set @sqlJOINforInstances_var = @sqlJOINforInstances;" + Environment.NewLine
 + @"set @sqlWHEREforInstances_var = @sqlWHEREforInstances;" + Environment.NewLine

  
 +@"set @oneDayAgo = convert(nvarchar , DATEADD(DAY, convert(int, @CallsForHowManyDaysBack), @now) , 25) --format 25 is '2017-01-01 00:00:00:000'"+Environment.NewLine
  
  
 +@"set @ifMonthChanged ="+Environment.NewLine
 +@"case"+Environment.NewLine
 +@"	when month(DATEADD(DAY, convert(int, @CallsForHowManyDaysBack) , @now)) = month(@now)"+Environment.NewLine
 +@"	then ' '"+Environment.NewLine
 +@"	else ' union all  select * from dbo.sessions_month_' + cast(month(DATEADD(DAY, convert(int, @CallsForHowManyDaysBack) , @now)) as nvarchar)"+Environment.NewLine
 +@"end"+Environment.NewLine
  
 +@"--print @currentMo"+Environment.NewLine
 +@"--print @oneDayAgo"+Environment.NewLine
 +@"--print @ifMonthChanged"+Environment.NewLine
  
  
 +@"--if serial#_to_ServerHostName mapping change in future, correct the CASE statement below e.g. (871001 <=> SE104421)"+Environment.NewLine
 +@"--For production replace CASE statement with following:"+Environment.NewLine
 +@"/*+CASE"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471002 THEN 'se441600\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471001 THEN 'se441601\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471003 THEN 'se441602\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471004 THEN 'se441603\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471005 THEN 'se441604\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471006 THEN 'se441605\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471007 THEN 'se441606\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471008 THEN 'se441607\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471009 THEN 'se441608\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471010 THEN 'se441902\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471011 THEN 'se441903\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471012 THEN 'se441904\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471013 THEN 'se441905\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471014 THEN 'se441906\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471015 THEN 'se441907\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471016 THEN 'se441908\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471017 THEN 'se441909\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.unit_num = 471018 THEN 'se441910\h$\Calls\'"+Environment.NewLine
 +@"	  END*/"+Environment.NewLine
  
  
 +@"set @sql = '"+Environment.NewLine
 +@"select distinct [Paths] = ''\\''"+Environment.NewLine
 +@"     +CASE"+Environment.NewLine
 +@"          WHEN sm3.unit_num = 871001 THEN ''SE104421\h$\Calls\''"+Environment.NewLine
 +@"          WHEN sm3.unit_num = 871002 THEN ''SE104422\h$\Calls\''"+Environment.NewLine
 +@"          WHEN sm3.unit_num = 871003 THEN ''SE104426\h$\Calls\''"+Environment.NewLine
 +@"          WHEN sm3.unit_num = 871004 THEN ''SE104427\h$\Calls\''"+Environment.NewLine
 +@"          END+"+Environment.NewLine
  
 +@"        CAST(sm3.unit_num AS nvarchar)"+Environment.NewLine
 +@"        + ''\''"+Environment.NewLine
 +@"        +SUBSTRING(REPLICATE(''0'', 9-LEN(sm3.channel_num))+ CAST(sm3.channel_num AS varchar) ,1, 3)"+Environment.NewLine
 +@"        + ''\''"+Environment.NewLine
 +@"        +SUBSTRING(REPLICATE(''0'', 9-LEN(sm3.channel_num))+ CAST(sm3.channel_num AS varchar) ,4, 2)"+Environment.NewLine
 +@"        + ''\''"+Environment.NewLine
 +@"        +SUBSTRING(REPLICATE(''0'', 9-LEN(sm3.channel_num))+ CAST(sm3.channel_num AS varchar) ,6, 2)"+Environment.NewLine
 +@"        + ''\''"+Environment.NewLine
 +@"        + CAST(sm3.unit_num AS nvarchar) +REPLICATE(''0'', 9-LEN(sm3.channel_num))+ CAST(sm3.channel_num AS varchar)"+Environment.NewLine
 +@"        + ''.wav''"+Environment.NewLine
 +@"        --,RTRIM(sm3.start_time) as start_time"+Environment.NewLine
 +@"        --,sm3.start_time"+Environment.NewLine

  + @"  , sm3.p6_value AS [CONNID]" + Environment.NewLine
  + @"	, sm3.local_start_time AS [Local Start Time]" + Environment.NewLine
  + @"	, sm3.local_end_time [Local End Time]" + Environment.NewLine
  + @"	, sm3.PBX_id" + Environment.NewLine
  + @"	, sm3.p2_value AS SRF" + Environment.NewLine
  //+ @"	, cat.category_name" + Environment.NewLine
  + "'+@addCatColOnlyIfTranscribed_var+'" + Environment.NewLine

 +@"from (select * from dbo.Sessions_month_'+@currentMo+@ifMonthChanged+') sm3"+Environment.NewLine
 + "'+@sqlJOINforInstances_var+'" + Environment.NewLine


 + @"where sm3.local_start_time > '''+@oneDayAgo+'''" + Environment.NewLine
 + "'+@sqlWHEREforInstances_var + ';'" + Environment.NewLine
  
 +@"exec (@sql);"+Environment.NewLine;
#endregion
                
                File.WriteAllText("sql.sql", sqlQuery);
            }
        }
        
        private static string connStrGet()
        {
            SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder();
            csb.DataSource = FQDN;
            //csb.InitialCatalog = database;
            csb.IntegratedSecurity = true;
            return csb.ConnectionString;
        }

        private static void getPathsIntoFile(string sql, string connStr)
        {
            SqlConnection con = new SqlConnection(connStr);
            SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@CallsForHowManyDaysBack", CallsForHowManyDaysBack);
            cmd.Parameters.AddWithValue("@addCatColOnlyIfTranscribed", addCatColOnlyIfTranscribed);//
            cmd.Parameters.AddWithValue("@sqlJOINforInstances", sqlJOINforInstances);
            cmd.Parameters.AddWithValue("@sqlWHEREforInstances",sqlWHEREforInstances);

            SqlDataReader reader;
            List<string> pathsList = new List<string>();
            List<string> metaDataList = new List<string>();
            string colNames = "", metaDataRow="";
            int noOfCols = 0;
            DateTime now = DateTime.Now;
            pathListFile = string.Format("pathsList {0:dd-MMM-yyyy}.txt", now);
            csvFileName = string.Format("metaData {0:dd-MMM-yyyy}.csv", now);


            try
            {
                con.Open();
                reader = cmd.ExecuteReader();
                noOfCols = reader.FieldCount;

                for (int i = 1; i < noOfCols; i++)
                {
                    colNames += reader.GetName(i) + ","; //https://stackoverflow.com/questions/681653/can-you-get-the-column-names-from-a-sqldatareader
                }
                colNames += Environment.NewLine;
                //colNames = reader.GetName(1) + "," + reader.GetName(2) + "," + reader.GetName(3) + "," + reader.GetName(4) + "," + reader.GetName(5) + "," + (excelNeeded ? reader.GetName(6) : "") + Environment.NewLine;//col Headers
                metaDataList.Add(colNames);
                colNames = "";

                while(reader.Read())
                {
                    pathsList.Add(reader.GetValue(0).ToString());
                    
                    if(excelNeeded)
                    {
                        for (int i = 1; i < noOfCols; i++)
                        {
                            metaDataRow += reader.GetValue(i).ToString() + ",";
                        }
                        //metaDataRow = reader.GetValue(1).ToString() + "," + reader.GetValue(2).ToString() + "," + reader.GetValue(3).ToString() + "," + reader.GetValue(4).ToString() + "," + reader.GetValue(5).ToString() + "," + (excelNeeded ? reader.GetValue(6).ToString() : "") + Environment.NewLine;//col Headers
                        //metaDataRow += "\n"; //List<> already provides a newLine when passed to WriteAllLines() so \n or Env.NewLine adds unwanted blank lines
                        metaDataList.Add(metaDataRow);
                        metaDataRow = ""; //MUST clear this var else resultSet is multiplied many times over into CSV file
                        File.WriteAllLines(csvFileName, metaDataList, Encoding.UTF8);
                    }
                }
                
                
                File.WriteAllLines(pathListFile, pathsList, Encoding.UTF8);
                
               
            }
            catch (Exception e)
            {
                //Console.WriteLine(e.Message);
                File.AppendAllText(logFile, "[" + DateTime.Now +"] "+ e.Message + Environment.NewLine);
            }
            finally
            {
                con.Close();
            }
        }

        private static void startCopy()
        {
            try
            {
                string[] allPathsArray = File.ReadAllLines(/*pathListFile*/string.Format("pathsList {0:dd-MMM-yyyy}.txt", DateTime.Now));

                if (allPathsArray != null)
                {
                    foreach (string path in allPathsArray)
                    {
                        linesRead++;
                        //if(!string.IsNullOrWhiteSpace(path))
                        if (File.Exists(path))
                        {
                            newFile = folder + "\\" + path.Substring(path.LastIndexOf('\\') + 1);
                            xmlOriginFile = path.Replace(".wav", ".xml");
                            xmlDestinFile = newFile.Replace(".wav", ".xml");
                            try
                            {
                                if (!File.Exists(newFile)) //if BOTH .wav & .xml already there in destination folder 
                                {
                                    File.Copy(path, newFile, true);
                                    counter++;
                                }
                                if (copyXml && !File.Exists(xmlDestinFile)) File.Copy(xmlOriginFile, xmlDestinFile, true);
                            }
                            catch (FileNotFoundException ex)
                            {
                                missingFiles++;
                                continue;
                            }
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(path)) missingFiles++;
                        }
                    }
                }
            }
            catch(Exception e)
            {
                File.AppendAllText(logFile, "[" + DateTime.Now + "] " + e.Message + Environment.NewLine);
            }

            File.AppendAllText(logFile, "[" + DateTime.Now + "] Paths:" + linesRead + ", Copied:"+counter + ", Missing:" + missingFiles + Environment.NewLine);
        }

    }
}
