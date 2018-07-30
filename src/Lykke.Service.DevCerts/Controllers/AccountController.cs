using Lykke.Service.DevCerts.AzureRepositories.User;
using Lykke.Service.DevCerts.Code;
using Lykke.Service.DevCerts.Core.User;
using Lykke.Service.DevCerts.Models;
using Lykke.Service.DevCerts.Services;
using Lykke.Service.DevCerts.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lykke.Service.DevCerts.Controllers
{
    [Authorize]
    [IgnoreLogAction]
    public class AccountController : BaseController
    {
        private string HomeUrl => Url.Action("Cert", "Home");
        private readonly AppSettings _appSettings;
        private readonly IUserRepository _userRepository;
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly IFilesHelper _filesHelper;

        private string ApiClientId { get; }
        private string AvailableEmailsRegex { get; }

        public AccountController(
            AppSettings appSettings,
            IUserRepository userRepository,
            IFilesHelper filesHelper,
            IUserActionHistoryRepository userActionHistoryRepository,
            IHostingEnvironment hostingEnvironment
            ) : base(userActionHistoryRepository)
        {
            _appSettings = appSettings;
            _userRepository = userRepository;
            _hostingEnvironment = hostingEnvironment;
            _filesHelper = filesHelper;

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
        [Route("Account/SignOut")]
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
                    user = new UserEntity() { Email = webSignature.Email, Admin = false, Visible = true };
                    await _userRepository.SaveUser(user);
                }

                if (!user.HasCert.HasValue || !(bool)user.HasCert)
                {
                    await _filesHelper.GenerateCertAsync(user, UserInfo.UserName, UserInfo.Ip);
                }

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
                return Content(Url.IsLocalUrl(returnUrl) ? returnUrl : HomeUrl);
            }
            catch (Exception ex)
            {
                return Content(ex.ToString());
            }
        }       
    }
}
