using Lykke.Service.DevCerts.Models;
using Lykke.Service.DevCerts.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lykke.Service.DevCerts.AzureRepositories.User;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Lykke.Service.DevCerts.Code;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Lykke.Service.DevCerts.Extensions;
using Lykke.Service.DevCerts.Core.User;
using System.IO;
using Lykke.Service.DevCerts.Core.Blob;

namespace Lykke.Service.DevCerts.Controllers
{
    [Authorize]
    [IgnoreLogAction]
    public class AccountController : BaseController
    {
        private string HomeUrl => Url.Action("Index", "Home");
        private readonly AppSettings _appSettings;
        private readonly IUserRepository _userRepository;
        private readonly IBlobDataRepository _blobDataRepository;

        private string ApiClientId { get; }
        private string AvailableEmailsRegex { get; }

        public AccountController(
            AppSettings appSettings,
            IUserRepository userRepository,
            IBlobDataRepository blobDataRepository,
            IUserActionHistoryRepository userActionHistoryRepository
            ) : base(userActionHistoryRepository)
        {
            _appSettings = appSettings;
            _userRepository = userRepository;
            _blobDataRepository = blobDataRepository;

            ApiClientId = _appSettings.DevCertsService.ApiClientId;
            AvailableEmailsRegex = _appSettings.DevCertsService.AvailableEmailsRegex;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> SignIn(string returnUrl)
        {           
            ViewData["ReturnUrl"] = returnUrl;

            return View(new SignInModel { GoogleApiClientId = ApiClientId });
        }

       

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SignOut()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect(HomeUrl);
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Authenticate(string googleSignInIdToken, string returnUrl)
        {
            try
            {
                var webSignature = await GoogleJsonWebSignatureEx.ValidateAsync(googleSignInIdToken);

                if (!webSignature.Audience.Equals(ApiClientId) ||
                    string.IsNullOrWhiteSpace(webSignature.Email) ||
                    !Regex.IsMatch(webSignature.Email, AvailableEmailsRegex) || !webSignature.IsEmailValidated)
                {
                    return Content(string.Empty);
                }
                
                var user = await _userRepository.GetUserByUserEmail(webSignature.Email);
                
                if (user == null)
                {
                    user = new UserEntity() { Email = webSignature.Email, Admin = false };
                    await _userRepository.SaveUser(user);
                }

                if (!user.HasCert.HasValue || !(bool)user.HasCert)
                {
                    await GenerateCertAsync(user);
                }

                //if (user.Active.HasValue && user.Active.Value)
                //{
                var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Sid, webSignature.Email),
                        new Claim("Admin",  user.Admin.ToString()),
                        new Claim(ClaimTypes.Name, webSignature.Email.Trim())
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, "password");
                    var claimsPrinciple = new ClaimsPrincipal(claimsIdentity);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrinciple);
                    //await _userHistoryRepository.SaveUserLoginHistoryAsync(user, UserInfo.Ip);
                    return Content(Url.IsLocalUrl(returnUrl) ? returnUrl : "~/");
                //}
            }
            catch (Exception ex)
            {

                return Content(ex.ToString());
            }

            return Content(string.Empty);
        }

        private async Task GenerateCertAsync(IUserEntity user)
        {
            var creds = user.Email.Substring(0, user.Email.IndexOf('@'));
            Console.WriteLine(creds);

            var shell = "";

            if (!String.IsNullOrWhiteSpace(_appSettings.DevCertsService.PathToScriptFolder))
            {
                shell += "cd " + _appSettings.DevCertsService.PathToScriptFolder + " && ";
            }
            shell += " ./" + _appSettings.DevCertsService.ScriptName + " " + creds;

            shell.Bash();

            await UpoadCertToBlob(creds);

            user.HasCert = true;
            user.CertDate = DateTime.Now;
            user.CertPassword = GetCertPass(creds);

            await _userRepository.SaveUser(user);

        }

        private async Task UpoadCertToBlob(string creds)
        {
            try
            {
                var home = "pwd".Bash();
                var filePath = home[5] + ":\\" + home.Substring(7, home.Length - 8).Replace("/", "\\");
                if (!String.IsNullOrWhiteSpace(_appSettings.DevCertsService.PathToScriptFolder))
                {
                    filePath +=_appSettings.DevCertsService.PathToScriptFolder.Replace("/", "\\") + "\\";
                }
                filePath = Path.Combine(filePath, creds + ".p12");
                Console.WriteLine(filePath);
                byte[] file;

                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        file = reader.ReadBytes((int)stream.Length);
                    }
                }

                await _blobDataRepository.UpdateBlobAsync(file, UserInfo.UserName, UserInfo.Ip, creds + ".p12");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
        }

        private string GetCertPass(string creds)
        {
            string pass = "";
            var shell = "";

            if (!String.IsNullOrWhiteSpace(_appSettings.DevCertsService.PathToScriptFolder))
            {
                shell += "cd " + _appSettings.DevCertsService.PathToScriptFolder + " && ";
            }
            shell += " cat "  + creds + ".pass";

            pass = shell.Bash();

            return pass.Substring(0,pass.Length-1);
        }

        
    }
}
