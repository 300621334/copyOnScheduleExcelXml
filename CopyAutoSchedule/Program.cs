using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CopyAutoSchedule
{
    class Program
    {
        static string pathListFile = "";
        static int linesRead = 0, counter = 0, missingFiles = 0;
        static string newFile = "", xmlOriginFile = "", xmlDestinFile = "", folder = Path.Combine(Application.StartupPath, @"Copied_Files");
        static bool copyXml = true;

        static void Main(string[] args)
        {
            string sqlFilePath = Path.Combine(Application.StartupPath, @"sql.sql");
            string sql = File.ReadAllText(sqlFilePath );
            string connStr = connStrGet();
            //string connStr = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=Movies;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=True;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";
            getPathsIntoFile(sql, connStr);
            startCopy();
        }

        
        private static string connStrGet()
        {
            SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder();
            csb.DataSource = @"SE104499.saimaple.saifg.rbc.com";
            csb.InitialCatalog = "CentralContact";
            csb.IntegratedSecurity = true;
            return csb.ConnectionString;
        }

        private static void getPathsIntoFile(string sql, string connStr)
        {
            SqlConnection con = new SqlConnection(connStr);
            SqlCommand cmd = new SqlCommand(sql, con);
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
                Console.WriteLine(e.Message);
                File.AppendAllText("Logs.txt", "[" + DateTime.Now +"] "+ e.Message + Environment.NewLine);
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
                                File.Copy(path, newFile, true);
                                if (copyXml) File.Copy(xmlOriginFile, xmlDestinFile, true);
                                counter++;
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
                File.AppendAllText("Logs.txt", "[" + DateTime.Now + "] " + e.Message + Environment.NewLine);
            }

            File.AppendAllText("Logs.txt", "[" + DateTime.Now + "] Paths:" + linesRead + ", Copied:"+counter + ", Missing:" + missingFiles + Environment.NewLine);
        }

    }
}
