using Lykke.Service.DevCerts.AzureRepositories.User;
using Lykke.Service.DevCerts.Core.Blob;
using Lykke.Service.DevCerts.Core.User;
using Lykke.Service.DevCerts.Services;
using Lykke.Service.DevCerts.Settings;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Lykke.Service.DevCerts.Code
{
    public class FilesHelper: IFilesHelper
    {
        public static DateTime LastTimeDbModified = new DateTime();
        private readonly AppSettings _appSettings;
        private readonly IUserRepository _userRepository;
        private readonly IBlobDataRepository _blobDataRepository;

        public FilesHelper(
            AppSettings appSettings,
            IUserRepository userRepository,
            IBlobDataRepository blobDataRepository)
        {
            _appSettings = appSettings;
            _userRepository = userRepository;
            _blobDataRepository = blobDataRepository;

        }

        public async Task UpdateDb()
        {
            try
            {
                var filePath = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, "db\\index.txt");
                if (LastTimeDbModified < File.GetLastWriteTime(filePath))
                {                    
                    string lineOfText = "";

                    using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, 128))
                        {
                            while ((lineOfText = reader.ReadLine()) != null)
                            {
                                var user = new UserEntity();
                                var line = lineOfText.Split("\t ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                                int offset = 0;
                                for (int i = 0; i < line.Length; i++)
                                {
                                    switch (i)
                                    {
                                        case 0:
                                            user.CertIsRevoked = line[i] == "V" ? false : true;                                         
                                            break;
                                        case 1:
                                            line[i] = line[i].Substring(0, line[i].Length - 1);
                                            var dateTill = DateTime.ParseExact(line[i], "yyMMddHHmmss", CultureInfo.InvariantCulture).ToLocalTime();                                                                                       
                                            user.CertDate = dateTill.AddYears(-10);
                                            break;
                                        case 2:
                                            if ((bool)user.CertIsRevoked)
                                            {
                                                line[i] = line[i].Substring(0, line[i].Length - 1);
                                                user.RevokeDate = DateTime.ParseExact(line[i], "yyMMddHHmmss", CultureInfo.InvariantCulture).ToLocalTime();
                                                offset = 1;
                                            }
                                            var serialNum = line[i + offset];
                                            break;
                                        case 4:
                                            var parameters = line[i + offset].Split('/');
                                            for (int j = 0; j < parameters.Length; j++)
                                            {
                                                if (parameters[j].Contains("CN="))
                                                {
                                                    user.Email = parameters[j].Remove(0, parameters[j].IndexOf('=') + 1);
                                                }
                                            }
                                            break;
                                    }
                                }

                                user.HasCert = true;
                                user.CertPassword = Crypto.EncryptStringAES(GetCertPass(user.Email), _appSettings.DevCertsService.EncryptionPass);

                                var userInCloud = await _userRepository.GetUserByUserEmail(user.Email);

                                if (userInCloud==null && !(bool)user.CertIsRevoked)
                                {
                                    await UpoadCertToBlob(user.Email, "Lykke.Service.DevCerts", "localhost");                                    
                                }

                                await _userRepository.SaveUser(user);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task UpoadCertToBlob(string creds, string userName, string ip)
        {
            try
            {
                var filePath = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, creds + ".p12");

                byte[] file;

                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        file = reader.ReadBytes((int)stream.Length);
                    }
                }

                await _blobDataRepository.UpdateBlobAsync(file, userName, ip, creds + ".p12");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }

        public string GetCertPass(string creds)
        {
            string pass = "";
            var shell = "";

            if (!String.IsNullOrWhiteSpace(_appSettings.DevCertsService.PathToScriptFolder))
            {
                shell += "cd " + _appSettings.DevCertsService.PathToScriptFolder + " && ";
            }
            shell += " cat " + creds + ".pass";

            pass = shell.Bash();

            return pass.Substring(0, pass.Length - 1);
        }

        public async Task ChangePass(IUserEntity user, string userName, string ip)
        {
            var creds = user.Email;
            var shell = "";

            if (!String.IsNullOrWhiteSpace(_appSettings.DevCertsService.PathToScriptFolder))
            {
                shell += "cd " + _appSettings.DevCertsService.PathToScriptFolder + " && ";
            }
            shell += " ./changepass.sh" + _appSettings.DevCertsService.ScriptName + " " + creds;

            shell.Bash();

            await UpoadCertToBlob(creds, userName, ip);

            await UpdateDb();
        }

        public async Task GenerateCertAsync(IUserEntity user, string userName, string ip)
        {
            var creds = user.Email;
            var shell = "";

            if (!String.IsNullOrWhiteSpace(_appSettings.DevCertsService.PathToScriptFolder))
            {
                shell += "cd " + _appSettings.DevCertsService.PathToScriptFolder + " && ";
            }
            shell += " ./" + _appSettings.DevCertsService.ScriptName + " " + creds;

            shell.Bash();

            await UpoadCertToBlob(creds, userName, ip);

            await UpdateDb();
        }

        public async Task RevokeUser(IUserEntity user, string userName, string ip)
        {
            var creds = user.Email;
            var shell = "";

            if (!String.IsNullOrWhiteSpace(_appSettings.DevCertsService.PathToScriptFolder))
            {
                shell += "cd " + _appSettings.DevCertsService.PathToScriptFolder + " && ";
            }
            shell += " ./revoke.sh "  + creds;

            Console.WriteLine(shell.Bash());

            await _blobDataRepository.DelBlobAsync(creds + ".p12");

            await UpdateDb();

        }


    }
}
