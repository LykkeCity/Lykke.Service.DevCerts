using Lykke.Service.DevCerts.AzureRepositories.User;
using Lykke.Service.DevCerts.Core.Blob;
using Lykke.Service.DevCerts.Core.User;
using Lykke.Service.DevCerts.Services;
using Lykke.Service.DevCerts.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public async Task UpdateDb(bool force = false, IUserEntity userEntity = null)
        {
            try
            {
                long total = 0;

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
                        var devFolder = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, @"../" + "ccd_dev");
                        var testFolder = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, @"../" + "ccd_test");

                        string lineOfText = "";


                        var usersInCloud = await _userRepository.GetUsers();
                        var userEntityList = new List<IUserEntity>();

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
                                    if(userToAdd == null)
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

                        userEntityList = userEntityList.OrderBy(u => u.CertDate).ToList();
                        if (userEntityList.Count > 0)
                        {

                            foreach(var user in userEntityList)
                            {
                                user.HasCert = true;
                                user.DevAccess = File.Exists(Path.Combine(devFolder, user.Email));
                                user.TestAccess = File.Exists(Path.Combine(testFolder, user.Email));
                                user.CertPassword = Crypto.EncryptStringAES(GetCertPass(user.Email), _appSettings.DevCertsService.EncryptionPass);

                                string creds = "";
                                if (user.Email.Contains('@'))
                                {
                                    creds = user.Email.Substring(0, user.Email.IndexOf('@'));
                                }
                                else 
                                {
                                    creds = user.Email;
                                }
                                    
                                var users = usersInCloud.Where(u => u.Email.Contains('@') ? u.Email.Substring(0, u.Email.IndexOf('@')) == creds : u.Email == creds);

                                IUserEntity userInCloud = null;

                                if (users != null)
                                    userInCloud = users.FirstOrDefault();

                                if (userInCloud != null)
                                {
                                    user.CertMD5 = userInCloud.CertMD5;
                                }

                                if (userInCloud != null)
                                {
                                    user.CertMD5 = userInCloud.CertMD5;
                                    user.DevMD5 = userInCloud.DevMD5;
                                    user.TestMD5 = userInCloud.TestMD5;
                                    user.Email = userInCloud.Email;                                  
                                }                                

                                if (!(bool)user.CertIsRevoked && (userInCloud == null || force))
                                {
                                    await UpoadCertToBlob(user, "Lykke.Service.DevCerts", "localhost");
                                }

                                if (!(bool)user.CertIsRevoked)
                                {
                                    var path = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, creds + ".p12");
                                    if(File.Exists(path))
                                        user.CertMD5 = CalculateMD5(path);
                                    path = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, creds + "-dev.ovpn");
                                    if (File.Exists(path))
                                        user.DevMD5 = CalculateMD5(path);
                                    path = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, creds + "-test.ovpn");
                                    if (File.Exists(path))
                                        user.TestMD5 = CalculateMD5(path);
                                }

                                if(userInCloud == null || user.CertIsRevoked != userInCloud.CertIsRevoked || user.CertDate.Value.ToUniversalTime() != userInCloud.CertDate.Value.ToUniversalTime() || (user.RevokeDate.HasValue &&  user.RevokeDate.Value.ToUniversalTime() != userInCloud.RevokeDate.Value.ToUniversalTime()))
                                {

                                    await _userRepository.SaveUser(user);
                                }
                                    
                            }

                            //var sortedlist = userEntityList.OrderByDescending(u => (bool)u.CertIsRevoked ? u.RevokeDate.Value.ToUniversalTime() : u.CertDate.Value.ToUniversalTime());
                            //var userToSave = sortedlist.FirstOrDefault();

                            //if (!(bool)userToSave.CertIsRevoked)
                            //{
                            //    userToSave.DevAccess = File.Exists(Path.Combine(devFolder, userToSave.Email));
                            //    userToSave.TestAccess = File.Exists(Path.Combine(testFolder, userToSave.Email));

                            //    userToSave.HasCert = true;
                            //    userToSave.CertPassword = Crypto.EncryptStringAES(GetCertPass(userToSave.Email), _appSettings.DevCertsService.EncryptionPass);

                            //    await UpoadCertToBlob(userToSave.Email, "Lykke.Service.DevCerts", "localhost");
                            //}

                            //var userInCloud = await _userRepository.GetUserByUserEmail(userToSave.Email);

                            //if (userInCloud != null)
                            //{
                            //    userToSave.Email = userInCloud.Email;
                            //}

                            //await _userRepository.SaveUser(userToSave);
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
            string creds = "";
            if (user.Email.Contains('@'))
                creds = user.Email.Substring(0, user.Email.IndexOf('@'));
            else
                creds = user.Email;

            string[] extrntions = new []{ ".p12", "-dev.ovpn", "-test.ovpn" };
            foreach(var extention in extrntions)
            {
                try
                {
                    
                    var filePath = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, creds + extention);
                    if (File.Exists(filePath))
                    {
                        var fileMd5 = CalculateMD5(filePath);
                        if (fileMd5 == user.CertMD5 && fileMd5 == user.DevMD5 && fileMd5 == user.TestMD5)
                            return;
                        byte[] file;
                    
                        using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            using (var reader = new BinaryReader(stream))
                            {
                                file = reader.ReadBytes((int)stream.Length);
                            }
                        }

                        await _blobDataRepository.UpdateBlobAsync(file, userName, ip, creds + extention);
                    }
                    else
                    {
                        Console.WriteLine($"File {creds + extention} does not exist. Path:" + filePath);
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
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
            string creds = "";
            if (user.Email.Contains('@'))
                creds = user.Email.Substring(0, user.Email.IndexOf('@'));
            else
                creds = user.Email;
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

            var path = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, creds + ".p12");
            if (File.Exists(path))
                user.CertMD5 = CalculateMD5(path);
            path = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, creds + "-dev.ovpn");
            if (File.Exists(path))
                user.DevMD5 = CalculateMD5(path);
            path = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, creds + "-test.ovpn");
            if (File.Exists(path))
                user.TestMD5 = CalculateMD5(path);

            await _userRepository.SaveUser(user);

            //await UpdateDb(false, user);
        }

        public async Task GraintAccess(IUserEntity user, string isDev)
        {
            string creds = "";
            if (user.Email.Contains('@'))
                creds = user.Email.Substring(0, user.Email.IndexOf('@'));
            else
                creds = user.Email;

            var folder = "";
            if (isDev == "dev")
                folder = "ccd_dev";
            else
                folder = "ccd_test";

            var fileFolder = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, @"../" + folder);

            var yesFilePath = Path.Combine(fileFolder, creds);
            var noFilePath = Path.Combine(fileFolder, "no-" + creds);
            bool hasAccess = false;
            if (File.Exists(yesFilePath))
            {
                File.Move(yesFilePath, noFilePath);
                Console.WriteLine($"Deny access file to {isDev} for {creds}");
            }
            else if (File.Exists(noFilePath))
            {
                hasAccess = true;
                File.Move(noFilePath, yesFilePath);
                Console.WriteLine($"Grant access file to {isDev} for {creds}");
            }

            if (isDev == "dev")
                user.DevAccess = hasAccess;
            else
                user.TestAccess = hasAccess;

            await _userRepository.SaveUser(user);

        }

        public async Task GenerateCertAsync(IUserEntity user, string userName, string ip)
        {
            string creds = "";
            if (user.Email.Contains('@'))
                creds = user.Email.Substring(0, user.Email.IndexOf('@'));
            else
                creds = user.Email;
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
            user.TestAccess = false;
            user.DevAccess = false;
            await UpoadCertToBlob(user, userName, ip);

            var path = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, creds + ".p12");
            if (File.Exists(path))
                user.CertMD5 = CalculateMD5(path);
            path = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, creds + "-dev.ovpn");
            if (File.Exists(path))
                user.DevMD5 = CalculateMD5(path);
            path = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, creds + "-test.ovpn");
            if (File.Exists(path))
                user.TestMD5 = CalculateMD5(path);

            await _userRepository.SaveUser(user);

            //await UpdateDb(false, user);
        }

        public async Task RevokeUser(IUserEntity user, string userName, string ip)
        {
            string creds = "";
            if (user.Email.Contains('@'))
                creds = user.Email.Substring(0, user.Email.IndexOf('@'));
            else
             creds = user.Email;
            var shell = "";

            if (!String.IsNullOrWhiteSpace(_appSettings.DevCertsService.PathToScriptFolder))
            {
                shell += "cd " + _appSettings.DevCertsService.PathToScriptFolder + " && ";
            }
            shell += "./revoke.sh "  + creds;

            shell.Bash();
            Console.WriteLine("Revoke user " + creds);

            string[] extrntions = new[] { ".p12", "-dev.ovpn", "-test.ovpn" };
            foreach (var extention in extrntions)
            {
                try
                {
                    await _blobDataRepository.DelBlobAsync(creds + extention);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }


            var folders = new[] { "ccd_dev", "ccd_test" };

            foreach(var folder in folders)
            {
                var fileFolder = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, @"../" + folder);
                var yesFilePath = Path.Combine(fileFolder, creds);
                var noFilePath = Path.Combine(fileFolder, "no-" + creds);

                if (File.Exists(yesFilePath))
                {
                    File.Move(yesFilePath, noFilePath);
                }
            }

            user.TestAccess = false;
            user.DevAccess = false;
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
