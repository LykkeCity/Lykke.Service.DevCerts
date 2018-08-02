﻿using Lykke.Service.DevCerts.AzureRepositories.User;
using Lykke.Service.DevCerts.Core.Blob;
using Lykke.Service.DevCerts.Core.User;
using Lykke.Service.DevCerts.Services;
using Lykke.Service.DevCerts.Settings;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
            UpdateDb();
        }

        public async Task UpdateDb(bool force = false, IUserEntity userEntity = null)
        {
            try
            {
                var filePath = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, "db");
                filePath = Path.Combine(filePath, "index.txt");

                var devFolder = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, @"..\" + "ccd_dev"); 
                var testFolder = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, @"..\" + "ccd_test");

                if (force || LastTimeDbModified.ToUniversalTime() <= File.GetCreationTimeUtc(filePath) && File.Exists(filePath))
                {

                    string lineOfText = "";

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
                                string creds = "";
                                if (userEntity != null && userEntity.Email.Contains('@'))
                                {
                                    creds = userEntity.Email.Substring(0, userEntity.Email.IndexOf('@'));
                                }
                                else if(userEntity != null)
                                {
                                    creds = userEntity.Email;
                                }

                                if (userEntity!=null && user.Email == creds)
                                {
                                    userEntityList.Add(user);
                                }
                                else
                                {

                                    user.HasCert = true;
                                    user.DevAccess = File.Exists(Path.Combine(devFolder, user.Email));
                                    user.TestAccess = File.Exists(Path.Combine(testFolder, user.Email));
                                    user.CertPassword = Crypto.EncryptStringAES(GetCertPass(user.Email), _appSettings.DevCertsService.EncryptionPass);

                                    var userInCloud = await _userRepository.GetUserByUserEmail(user.Email);

                                    if (userInCloud == null || force )
                                    {

                                        await UpoadCertToBlob(user.Email, "Lykke.Service.DevCerts", "localhost");                                        
                                    }

                                    if (userInCloud != null)
                                    {
                                        user.Email = userInCloud.Email;
                                    }

                                    await _userRepository.SaveUser(user);
                                }

                            }
                        }
                    }

                    if (userEntityList.Count > 0)
                    {
                        var sortedlist = userEntityList.OrderByDescending(u => (bool)u.CertIsRevoked ? u.RevokeDate.Value.ToUniversalTime() : u.CertDate.Value.ToUniversalTime());
                        var userToSave = sortedlist.FirstOrDefault();                                              

                        if (!(bool)userToSave.CertIsRevoked)
                        {
                            userToSave.DevAccess = File.Exists(Path.Combine(devFolder, userToSave.Email));
                            userToSave.TestAccess = File.Exists(Path.Combine(testFolder, userToSave.Email));

                            userToSave.HasCert = true;
                            userToSave.CertPassword = Crypto.EncryptStringAES(GetCertPass(userToSave.Email), _appSettings.DevCertsService.EncryptionPass);

                            await UpoadCertToBlob(userToSave.Email, "Lykke.Service.DevCerts", "localhost");
                        }

                        var userInCloud = await _userRepository.GetUserByUserEmail(userToSave.Email);

                        if (userInCloud != null)
                        {
                            userToSave.Email = userInCloud.Email;
                        }

                        await _userRepository.SaveUser(userToSave);
                    }

                    LastTimeDbModified = File.GetCreationTimeUtc(filePath);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task UpoadCertToBlob(string creds, string userName, string ip)
        {
            string[] extrntions = new []{ ".p12", "-dev.ovpn", "-test.ovpn" };
            foreach(var extention in extrntions)
            {
                try
                {
                    var filePath = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, creds + extention);

                    byte[] file;
                    if (File.Exists(filePath))
                    {
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

            await UpdateDb(false, user);
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

            var fileFolder = Path.Combine(_appSettings.DevCertsService.PathToScriptFolder, @"..\" + folder);

            var yesFilePath = Path.Combine(fileFolder, creds);
            var noFilePath = Path.Combine(fileFolder, "no-" + creds);            

            if (File.Exists(yesFilePath))
            {
                File.Move(yesFilePath, noFilePath);
                Console.WriteLine($"Deny access file to {isDev} for {creds}");
            }
            else if (File.Exists(noFilePath))
            {
                File.Move(noFilePath, yesFilePath);
                Console.WriteLine($"Grant access file to {isDev} for {creds}");
            }
            else
            {
                var shell = "";

                if (!String.IsNullOrWhiteSpace(_appSettings.DevCertsService.PathToScriptFolder))
                {
                    shell += "cd " + _appSettings.DevCertsService.PathToScriptFolder + " && ";
                }

                shell += "./ip-addr.sh " + creds;
                Console.WriteLine($"Creating access file to {isDev} for {creds}");
                shell.Bash();
            }

            await UpdateDb(false, user);
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

            await UpdateDb(false, user);
        }

        public async Task RevokeUser(IUserEntity user, string userName, string ip)
        {
            await _userRepository.SaveUser(user);
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



            await UpdateDb(false, user);

        }


    }
}
