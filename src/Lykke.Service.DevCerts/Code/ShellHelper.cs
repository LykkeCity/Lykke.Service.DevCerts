using System;
using System.Diagnostics;

namespace Lykke.Service.DevCerts.Code
{
    public static class ShellHelper
    {
        public static string Bash(this string cmd)
        {
            var escapedArgs = cmd.Replace("\\", "");
            Console.WriteLine(escapedArgs);
            string result = "";
            using (Process proc = new Process())
            {
                proc.StartInfo.FileName = "/bin/bash";
                proc.StartInfo.Arguments = "-c \" " + escapedArgs + " \"";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.Start();

                result += proc.StandardOutput.ReadLine();
                result += proc.StandardError.ReadLine();
            }
            Console.WriteLine(result);
            return result;
        }
    }
}
