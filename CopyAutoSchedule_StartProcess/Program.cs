using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CopyAutoSchedule_StartProcess
{
    class Program
    {
        static void Main(string[] args)
        {
             Process myProcess = new Process();
            try 
            {
                myProcess.StartInfo.UseShellExecute = false;
                myProcess.StartInfo.FileName = Path.Combine(Application.StartupPath , "CopyAutoSchedule.exe");
                myProcess.StartInfo.CreateNoWindow = true;
                myProcess.Start();
            }
            catch(Exception e)
            {
                File.AppendAllText("Error_inStartingProcess.txt", "[" + DateTime.Now + "] " + e.Message +" "+ Path.Combine(Application.StartupPath, "CopyAutoSchedule.exe") + Environment.NewLine);
                //myProcess.Kill();//causing unhandled error itslef when CopyAutoSchedule.exe is not in same folder. Trie to kill a non-existing process!!!
            }
        }
    }
}
