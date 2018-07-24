using Lykke.Service.DevCerts.AzureRepositories.User;
using Lykke.Service.DevCerts.Code;
using Lykke.Service.DevCerts.Core.Blob;
using Lykke.Service.DevCerts.Core.User;
using Lykke.Service.DevCerts.Models;
using Lykke.Service.DevCerts.Services;
using Lykke.Service.DevCerts.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Lykke.Service.DevCerts.Controllers
{
    [Authorize]
    public class HomeController : BaseController
    {
        private readonly AppSettings _appSettings;
        private readonly IUserRepository _userRepository;
        private readonly IBlobDataRepository _blobDataRepository;
        private readonly IFilesHelper _filesHelper;

        public HomeController(
            AppSettings appSettings,
            IUserRepository userRepository,
            IBlobDataRepository blobDataRepository,
            IUserActionHistoryRepository userActionHistoryRepository,
            IFilesHelper filesHelper
            ) : base(userActionHistoryRepository)
        {
            _appSettings = appSettings;
            _userRepository = userRepository;
            _blobDataRepository = blobDataRepository;
            _filesHelper = filesHelper;

        }

        [HttpGet]
        [Route("Home/Cert")]
        public async Task<IActionResult> Cert()
        {
            var user = await _userRepository.GetUserByUserEmail(HttpContext.User.Identity.Name);
            Console.WriteLine(HttpContext.User.Identity.Name);

            var pass = "";

            try
            {
                pass = Crypto.DecryptStringAES(user.CertPassword, _appSettings.DevCertsService.EncryptionPass);
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
                pass = "No password file";
            }


            return View(new UserModel {
                RowKey = user.RowKey,
                Email = user.Email,
                CertIsRevoked = user.CertIsRevoked.HasValue ? (bool)user.CertIsRevoked : false,
                CertPassword = pass
            });
        }
                

        [HttpGet]
        [Route("Home/ManageUsers")]
        public async Task<IActionResult> ManageUsers()
        {
            var users = await GetAllUsers();

            return View(new UsersModel
            {
                Users = users,
            });
        }


        [HttpGet]
        [Route("Home/GetCert")]
        public async Task<FileStreamResult> GetCert()
        {
            try
            {
                var creds = HttpContext.User.Identity.Name.Substring(0, HttpContext.User.Identity.Name.IndexOf('@'));
                var fileName = creds + ".p12";
                var blob = await _blobDataRepository.GetDataAsync(fileName);
                Stream blobStream = blob.AsStream();
                return File(blobStream, System.Net.Mime.MediaTypeNames.Application.Octet, fileName);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }

        }

        [HttpGet]
        [Route("Home/GetCertificates/{rowKey}")]
        public async Task<FileStreamResult> GetCertificates(string rowKey)
        {
            var user = await _userRepository.GetUserByUserEmail(HttpContext.User.Identity.Name);

            if ((bool)user.Admin)
            {
                var userData = await _userRepository.GetUserByRowKey(rowKey);
                try
                {
                    var creds = userData.Email.Substring(0, userData.Email.IndexOf('@'));
                    var fileName = creds + ".p12";
                    var blob = await _blobDataRepository.GetDataAsync(fileName);
                    Stream blobStream = blob.AsStream();
                    return File(blobStream, System.Net.Mime.MediaTypeNames.Application.Octet, fileName);

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return null;
                }
            }
            return null;
        }

        [HttpPost]
        [Route("Home/RevokeCert/{rowKey}")]
        public async Task<IActionResult> RevokeCert(string rowKey)
        {
            var user = await _userRepository.GetUserByUserEmail(HttpContext.User.Identity.Name);

            if ((bool)user.Admin)
            {
                var userData = await _userRepository.GetUserByRowKey(rowKey);
                await _filesHelper.RevokeUser(userData, UserInfo.UserName,UserInfo.Ip);               
            }
            var users = await GetAllUsers();
            return new JsonResult(new { Json = JsonConvert.SerializeObject(users) });

        }

        [HttpPost]
        [Route("Home/ChangePass/{rowKey}")]
        public async Task<IActionResult> ChangePass(string rowKey)
        {
            var user = await _userRepository.GetUserByUserEmail(HttpContext.User.Identity.Name);

            if ((bool)user.Admin)
            {
                var userData = await _userRepository.GetUserByRowKey(rowKey);
                await _filesHelper.ChangePass(userData, UserInfo.UserName,UserInfo.Ip);               
            }
            var users = await GetAllUsers();
            return new JsonResult(new { Json = JsonConvert.SerializeObject(users) });

        }

        [HttpPost]
        [Route("Home/ChangePassForUser/{rowKey}")]
        public async Task<IActionResult> ChangePassForUser(string rowKey)
        {
            var userData = await _userRepository.GetUserByRowKey(rowKey);
            await _filesHelper.ChangePass(userData, UserInfo.UserName, UserInfo.Ip);

            userData = await _userRepository.GetUserByRowKey(rowKey);
            var pass = "";

            try
            {
                pass = Crypto.DecryptStringAES(userData.CertPassword, _appSettings.DevCertsService.EncryptionPass);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                pass = "No password file";
            }

            return new JsonResult(new { Json = JsonConvert.SerializeObject(pass) });

        }

        [HttpPost]
        [Route("Home/Reparse")]
        public async Task<IActionResult> Reparse(string rowKey)
        {
            var user = await _userRepository.GetUserByUserEmail(HttpContext.User.Identity.Name);

            if ((bool)user.Admin)
            {
                await _filesHelper.UpdateDb(true);
            }
            var users = await GetAllUsers();
            return new JsonResult(new { Json = JsonConvert.SerializeObject(users) });
        }

        [HttpPost]
        [Route("Home/GenerateNew/{rowKey}")]
        public async Task<IActionResult> GenerateNew(string rowKey)
        {
            var user = await _userRepository.GetUserByUserEmail(HttpContext.User.Identity.Name);

            if ((bool)user.Admin)
            {
                var userData = await _userRepository.GetUserByRowKey(rowKey);
                await _filesHelper.GenerateCertAsync(userData, UserInfo.UserName, UserInfo.Ip);
            }
            var users = await GetAllUsers();
            return new JsonResult(new { Json = JsonConvert.SerializeObject(users) });

        }

        private async Task<List<UserModel>> GetAllUsers()
        {
            var result = await _userRepository.GetUsers();

            var users = (from u in result
                         let uc = u as UserEntity
                         orderby uc.Email
                         select new UserModel
                         {
                             RowKey = uc.RowKey,
                             Email = uc.Email,
                             CertDate = (uc.CertDate ?? DateTime.MinValue).ToString("dd/MM/yyyy HH:mm:ss"),
                             CertIsRevoked = uc.CertIsRevoked ?? false,
                             Admin = uc.Admin ?? false,
                             CertPassword =  String.IsNullOrWhiteSpace(uc.CertPassword) ? "No password file" : Crypto.DecryptStringAES(uc.CertPassword, _appSettings.DevCertsService.EncryptionPass),
                             HasCert = uc.HasCert ?? false,
                             RevokeDate = (uc.RevokeDate ?? DateTime.MinValue).ToString("dd/MM/yyyy HH:mm:ss"),
                         }).ToList();

            return users;
        }
    }
}
