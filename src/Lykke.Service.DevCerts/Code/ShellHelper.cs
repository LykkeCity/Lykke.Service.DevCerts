using System;
using System.Diagnostics;

namespace Lykke.Service.DevCerts.Code
{
    public static class ShellHelper
    {
        public static string Bash(this string cmd)
        {
            var escapedArgs = cmd.Replace("\\", "");
            if (!escapedArgs.Contains(" cat "))
                Console.WriteLine(escapedArgs);
            string result = "";
            try
            {
                using (Process proc = new Process())
                {
                    proc.StartInfo.FileName = "/bin/bash";
                    proc.StartInfo.Arguments = $"-c \"{escapedArgs}\"";
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.RedirectStandardOutput = true;
                    proc.StartInfo.RedirectStandardError = true;
                    proc.Start();

                    result += proc.StandardOutput.ReadToEnd();
                    result += proc.StandardError.ReadToEnd();

                    proc.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            if (!escapedArgs.Contains(" cat "))
                Console.WriteLine(result);
            return result;
        }
    }
}
