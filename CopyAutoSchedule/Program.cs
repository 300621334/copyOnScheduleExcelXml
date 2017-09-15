using System;
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
 */
namespace CopyAutoSchedule
{
    class Program
    {
        #region Variables
        static string pathListFile = "";
        static int linesRead = 0, counter = 0, missingFiles = 0, maxLogSizeInKBs = 1;
        static string logFile="Logs.txt", newFile = "", xmlOriginFile = "", xmlDestinFile = "", folder = "Copied_Files" /*folder = @"\\SE104421\h$\Test"*/
            , FQDN = "SE104499.saimaple.saifg.rbc.com", database = "CentralContact", CallsForHowManyDaysBack = "-1";
        static string[] configTxt;
        static bool copyXml = true;
        #endregion

        static void Main(string[] args)
        {
            configFile();
            trimLogFile();
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
                + "Max Log Size (KB): 1000" + System.Environment.NewLine;

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
                        CallsForHowManyDaysBack = "-" + line.Substring(match.Index + 1).Trim();  //add minus sign so days get substracted
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
                }
            }

        }

        private static void trimLogFile()
        {//https://stackoverflow.com/questions/1515097/how-do-i-delete-the-first-x-lines-of-a-text-file  //https://stackoverflow.com/questions/1380839/how-do-you-get-the-file-size-in-c
            
            if (File.Exists(logFile))
            {
                long logFileSizeInBytes = new FileInfo(logFile).Length;
                if(true/*logFileSizeInBytes > (1000 * maxLogSizeInKBs)*/)
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

        private static void sqlFileCreate()
        {
            if(!File.Exists("sql.sql"))
            {
#region SQL Query
 string sqlQuery = 
 "--Cannot declare a @var and assign tbl name to it then use that @var in FROM clause"+Environment.NewLine
 +@"--must have WHOLE query as a string and assign to a var, then EXEC(@var); brackets must"+Environment.NewLine
 +Environment.NewLine
  
 +@"Use CentralContact;"+Environment.NewLine
 +@"SET NOCOUNT ON;"+Environment.NewLine
  
 +@"declare @sql nvarchar(max), @currentMo nvarchar(2), @ifMonthChanged nvarchar(max), @oneDayAgo nvarchar(max);"+Environment.NewLine
  
 +@"set @currentMo = DATEPART(M, GETDATE());"+Environment.NewLine
  
 +@"set @oneDayAgo = convert(nvarchar , DATEADD(DAY, convert(int, @CallsForHowManyDaysBack), GETDATE()) , 25) --format 25 is '2017-01-01 00:00:00:000'"+Environment.NewLine
  
  
 +@"set @ifMonthChanged ="+Environment.NewLine
 +@"case"+Environment.NewLine
 +@"	when month(DATEADD(DAY, convert(int, @CallsForHowManyDaysBack) , GETDATE())) = month(GETDATE())"+Environment.NewLine
 +@"	then ' '"+Environment.NewLine
 +@"	else ' union all  select * from dbo.sessions_month_' + cast(month(DATEADD(DAY, convert(int, @CallsForHowManyDaysBack) , GETDATE())) as nvarchar)"+Environment.NewLine
 +@"end"+Environment.NewLine
  
 +@"--print @currentMo"+Environment.NewLine
 +@"--print @oneDayAgo"+Environment.NewLine
 +@"--print @ifMonthChanged"+Environment.NewLine
  
  
 +@"--if serial#_to_ServerHostName mapping change in future, correct the CASE statement below e.g. (871001 <=> SE104421)"+Environment.NewLine
 +@"--For production replace CASE statement with following:"+Environment.NewLine
 +@"/*+CASE"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471002 THEN 'se441600\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471001 THEN 'se441601\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471003 THEN 'se441602\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471004 THEN 'se441603\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471005 THEN 'se441604\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471006 THEN 'se441605\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471007 THEN 'se441606\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471008 THEN 'se441607\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471009 THEN 'se441608\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471010 THEN 'se441902\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471011 THEN 'se441903\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471012 THEN 'se441904\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471013 THEN 'se441905\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471014 THEN 'se441906\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471015 THEN 'se441907\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471016 THEN 'se441908\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471017 THEN 'se441909\h$\Calls\'"+Environment.NewLine
 +@"	  WHEN sm3.audio_module_no = 471018 THEN 'se441910\h$\Calls\'"+Environment.NewLine
 +@"	  END*/"+Environment.NewLine
  
  
 +@"set @sql = '"+Environment.NewLine
 +@"select distinct [Paths] = ''\\''"+Environment.NewLine
 +@"     +CASE"+Environment.NewLine
 +@"          WHEN sm3.audio_module_no = 871001 THEN ''SE104421\h$\Calls\''"+Environment.NewLine
 +@"          WHEN sm3.audio_module_no = 871002 THEN ''SE104422\h$\Calls\''"+Environment.NewLine
 +@"          WHEN sm3.audio_module_no = 871003 THEN ''SE104426\h$\Calls\''"+Environment.NewLine
 +@"          WHEN sm3.audio_module_no = 871004 THEN ''SE104427\h$\Calls\''"+Environment.NewLine
 +@"          END+"+Environment.NewLine
  
 +@"        CAST(sm3.audio_module_no AS nvarchar)"+Environment.NewLine
 +@"        + ''\''"+Environment.NewLine
 +@"        +SUBSTRING(REPLICATE(''0'', 9-LEN(sm3.audio_ch_no))+ CAST(sm3.audio_ch_no AS varchar) ,1, 3)"+Environment.NewLine
 +@"        + ''\''"+Environment.NewLine
 +@"        +SUBSTRING(REPLICATE(''0'', 9-LEN(sm3.audio_ch_no))+ CAST(sm3.audio_ch_no AS varchar) ,4, 2)"+Environment.NewLine
 +@"        + ''\''"+Environment.NewLine
 +@"        +SUBSTRING(REPLICATE(''0'', 9-LEN(sm3.audio_ch_no))+ CAST(sm3.audio_ch_no AS varchar) ,6, 2)"+Environment.NewLine
 +@"        + ''\''"+Environment.NewLine
 +@"        + CAST(sm3.audio_module_no AS nvarchar) +REPLICATE(''0'', 9-LEN(sm3.audio_ch_no))+ CAST(sm3.audio_ch_no AS varchar)"+Environment.NewLine
 +@"        + ''.wav''"+Environment.NewLine
 +@"        --,RTRIM(sm3.start_time) as start_time"+Environment.NewLine
 +@"        --,sm3.start_time"+Environment.NewLine
  
  
 +@"from (select * from dbo.Sessions_month_'+@currentMo+@ifMonthChanged+') sm3"+Environment.NewLine
  
  
  
 +@"where sm3.start_time > '''+@oneDayAgo+'''';"+Environment.NewLine
  
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

            SqlDataReader reader;
            List<string> pathsList = new List<string>();

            try
            {
                con.Open();
                reader = cmd.ExecuteReader();

                while(reader.Read())
                {
                    pathsList.Add(reader.GetValue(0).ToString());
                }
                pathListFile = string.Format("pathsList {0:dd-MMM-yyyy}.txt", DateTime.Now);
                File.WriteAllLines(pathListFile, pathsList, Encoding.UTF8);
               
            }
            catch (Exception e)
            {
                //Console.WriteLine(e.Message);
                File.AppendAllText(logFile, "[" + DateTime.Now +"] "+ e.Message + Environment.NewLine);
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
