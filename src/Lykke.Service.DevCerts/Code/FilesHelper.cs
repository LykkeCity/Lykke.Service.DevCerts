using Lykke.Service.DevCerts.AzureRepositories.User;
using Lykke.Service.DevCerts.Core.Blob;
using Lykke.Service.DevCerts.Core.User;
using Lykke.Service.DevCerts.Services;
using Lykke.Service.DevCerts.Settings;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Lykke.Service.DevCerts.Code
{
    public class FilesHelper: IFilesHelper
    {
        public static string LastMD5Hash = "";
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
            UpdateDb();
        }

        public async Task UpdateDb(bool force = false)
        {
            try
            {
                var filePath = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, "db");
                filePath = Path.Combine(filePath, "index.txt");
                if (!File.Exists(filePath))
                {
                    var shell = "";
                    if (!String.IsNullOrWhiteSpace(_appSettings.DevCertsService.PathToScriptFolder))
                    {
                        shell += "cd " + _appSettings.DevCertsService.PathToScriptFolder + " && ";
                    }
                    shell += "./create.sh ";

                    shell.Bash();
                }
                else
                {
                    var NowMD5Hash = CalculateMD5(filePath);
                    if (LastMD5Hash != NowMD5Hash)
                    {
                        var usersInCloud = await _userRepository.GetUsers();
                        var userEntityList = new List<IUserEntity>();

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
                                                dateTill = dateTill.AddYears(-10);
                                                dateTill = dateTill.AddDays(3);
                                                user.CertDate = dateTill;
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

                                    var userToAdd = userEntityList.Where(u => u.Email == user.Email).FirstOrDefault();
                                    if (userToAdd == null)
                                    {
                                        userEntityList.Add(user);
                                    }
                                    else if (user.CertDate.Value.ToUniversalTime() > userToAdd.CertDate.Value.ToUniversalTime())
                                    {
                                        userEntityList.Remove(userToAdd);
                                        userEntityList.Add(user);
                                    }

                                }
                            }
                        }

                        if (userEntityList.Count > 0)
                        {
                            foreach (var user in userEntityList)
                            {
                                user.HasCert = true;
                                user.CertPassword = Crypto.EncryptStringAES(GetCertPass(user.Email), _appSettings.DevCertsService.EncryptionPass);

                                var users = usersInCloud.Where(u => u.Email == user.Email);

                                IUserEntity userInCloud = null;

                                if(users != null)
                                    userInCloud = users.FirstOrDefault();

                                if (userInCloud != null)
                                {
                                    user.CertMD5 = userInCloud.CertMD5;
                                }

                                if (!(bool)user.CertIsRevoked && (userInCloud == null || force))
                                {
                                    await UpoadCertToBlob(user, "Lykke.Service.DevCerts", "localhost");
                                }
                                var path = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, user.Email + ".p12");
                                if (!(bool)user.CertIsRevoked && File.Exists(path))
                                {
                                    user.CertMD5 = CalculateMD5(path);
                                }

                                if (userInCloud == null || user.CertIsRevoked != userInCloud.CertIsRevoked || user.CertDate.Value.ToUniversalTime() != userInCloud.CertDate.Value.ToUniversalTime() || (user.RevokeDate.HasValue && user.RevokeDate.Value.ToUniversalTime() != userInCloud.RevokeDate.Value.ToUniversalTime()))
                                {
                                    await _userRepository.SaveUser(user);
                                };
                            }
                        }

                        LastMD5Hash = NowMD5Hash;
                    }
                }
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task UpoadCertToBlob(IUserEntity user, string userName, string ip)
        {
            try
            {
                var filePath = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, user.Email + ".p12");
                if (File.Exists(filePath))
                {
                    var fileMd5 = CalculateMD5(filePath);
                    if (fileMd5 == user.CertMD5)
                        return;

                    byte[] file;
                
                    using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        using (var reader = new BinaryReader(stream))
                        {
                            file = reader.ReadBytes((int)stream.Length);
                        }
                    }

                    await _blobDataRepository.UpdateBlobAsync(file, userName, ip, user.Email + ".p12");
                }
                else
                {
                    Console.WriteLine($"File {user.Email}.p12 does not exist. Path:" + filePath);
                }
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

            var filePath = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, creds + ".pass");

            if (File.Exists(filePath))
            {
                if (!String.IsNullOrWhiteSpace(_appSettings.DevCertsService.PathToScriptFolder))
                {
                    shell += "cd " + _appSettings.DevCertsService.PathToScriptFolder + " && ";
                }
                shell += "cat " + creds + ".pass";

                pass = shell.Bash();
            }
            else
            {
                pass = "No password file.";
            }

            if (String.IsNullOrWhiteSpace(pass))
            {
                pass = "No password file.";
            }

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
            shell += "./changepass.sh " + creds;

            shell.Bash();

            Console.WriteLine("Change pass for " + creds);
            user.CertPassword = Crypto.EncryptStringAES(GetCertPass(creds), _appSettings.DevCertsService.EncryptionPass);
            user.CertDate = DateTime.Now.ToUniversalTime();

            await UpoadCertToBlob(user, userName, ip);

            var filePath = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, creds + ".p12");
            if (File.Exists(filePath))
                user.CertMD5 = CalculateMD5(filePath);

            await _userRepository.SaveUser(user);
            //await UpdateDb(false, user);
        }

        public async Task GenerateCertAsync(IUserEntity user, string userName, string ip)
        {
            var creds = user.Email;
            var shell = "";

            if (!String.IsNullOrWhiteSpace(_appSettings.DevCertsService.PathToScriptFolder))
            {
                shell += "cd " + _appSettings.DevCertsService.PathToScriptFolder + " && ";
            }
            shell += "./" + _appSettings.DevCertsService.ScriptName + " " + creds;

            shell.Bash();

            Console.WriteLine("Generate cert for " + creds);
            user.Visible = true;
            user.Admin = false;
            user.HasCert = true;
            user.CertIsRevoked = false;
            user.CertPassword = Crypto.EncryptStringAES(GetCertPass(creds), _appSettings.DevCertsService.EncryptionPass);
            user.CertDate = DateTime.Now.ToUniversalTime();

            await UpoadCertToBlob(user, userName, ip);

            var filePath = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, creds + ".p12");
            if (File.Exists(filePath))
                user.CertMD5 = CalculateMD5(filePath);

            await _userRepository.SaveUser(user);
            //await UpdateDb(false, user);
        }

        public async Task RevokeUser(IUserEntity user, string userName, string ip)
        {
            var creds = user.Email;
            var shell = "";

            if (!String.IsNullOrWhiteSpace(_appSettings.DevCertsService.PathToScriptFolder))
            {
                shell += "cd " + _appSettings.DevCertsService.PathToScriptFolder + " && ";
            }
            shell += "./revoke.sh "  + creds;

            shell.Bash();
            Console.WriteLine("Revoke user " + creds);

            try
            {
                await _blobDataRepository.DelBlobAsync(creds + ".p12");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            user.CertIsRevoked = true;
            user.RevokeDate = DateTime.Now.ToUniversalTime();
            await _userRepository.SaveUser(user);
            //await UpdateDb(false, user);

        }

        private static string CalculateMD5(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
    }
}
