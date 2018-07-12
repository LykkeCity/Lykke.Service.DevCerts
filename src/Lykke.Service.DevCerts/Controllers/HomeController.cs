using Lykke.Service.DevCerts.AzureRepositories.User;
using Lykke.Service.DevCerts.Core.Blob;
using Lykke.Service.DevCerts.Core.User;
using Lykke.Service.DevCerts.Models;
using Lykke.Service.DevCerts.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Lykke.Service.DevCerts.Controllers
{
    [Authorize]
    public class HomeController : BaseController
    {
        private readonly AppSettings _appSettings;
        private readonly IUserRepository _userRepository;
        private readonly IBlobDataRepository _blobDataRepository;

        public HomeController(
            AppSettings appSettings,
            IUserRepository userRepository,
            IBlobDataRepository blobDataRepository,
            IUserActionHistoryRepository userActionHistoryRepository
            ) : base(userActionHistoryRepository)
        {
            _appSettings = appSettings;
            _userRepository = userRepository;
            _blobDataRepository = blobDataRepository;

        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userRepository.GetUserByUserEmail(HttpContext.User.Identity.Name);
            Console.WriteLine(HttpContext.User.Identity.Name);
            return View(new UserModel { Email = user.Email, CertPassword = user.CertPassword });
        }

        [HttpGet]
        public async Task<IActionResult> ManageUsers()
        {
            var users = await GetAllUsers();

            return View(new UsersModel
            {
                Users = users,
            });
        }


        [HttpGet]
        public async Task<FileStreamResult> GetCert()
        {
            try
            {
                var fileName = HttpContext.User.Identity.Name + ".p12";
                var blob = await _blobDataRepository.GetDataAsync(fileName);
                Stream blobStream = blob.AsStream();
                //HttpResponseMessage message = new HttpResponseMessage(HttpStatusCode.OK);
                

                //message.Content = new StreamContent(blobStream);
                //message.Content.Headers.ContentLength = blobStream.Length;
                //message.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                //message.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                //{
                //    FileName = "{blobname.txt}",
                //    Size = blobStream.Length
                //};
                return File(blobStream, System.Net.Mime.MediaTypeNames.Application.Octet, fileName);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }

        }

        private async Task<List<UserModel>> GetAllUsers()
        {
            var result = await _userRepository.GetUsers();

            var users = (from u in result
                         let uc = u as UserEntity
                         orderby uc.RowKey
                         select new UserModel
                         {
                             Email = uc.Email,
                             CertDate = uc.CertDate ?? DateTime.MinValue,
                             CertIsRevoked = uc.CertIsRevoked ?? false,
                             Admin = uc.Admin ?? false,
                             CertPassword = uc.CertPassword,
                             HasCert = uc.HasCert ?? false,
                             RevokeDate = uc.RevokeDate ?? DateTime.MinValue,
                         }).ToList();


            return users;
        }
    }
}
