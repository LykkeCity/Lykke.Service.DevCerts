using Common;
using Lykke.Common.Extensions;
using Lykke.Service.DevCerts.AzureRepositories.User;
using Lykke.Service.DevCerts.Code;
using Lykke.Service.DevCerts.Core.Blob;
using Lykke.Service.DevCerts.Core.User;
using Lykke.Service.DevCerts.Extensions;
using Lykke.Service.DevCerts.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lykke.Service.DevCerts.Controllers
{
    public class BaseController : Controller
    {
        private IUserRepository _userRepository;
        private IBlobDataRepository _blobDataRepository;

        private readonly IUserActionHistoryRepository _userActionHistoryRepository;
        private AppSettings _appSettings;

        #region Constatnts
        private const string API_KEY = "BU3Nkbkqg2HOo5sRJ8c";
        #endregion

        protected UserInfo UserInfo { get; private set; }

        public BaseController(IUserActionHistoryRepository userActionHistoryRepository)
        {
            _userActionHistoryRepository = userActionHistoryRepository;
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            UserInfo = new UserInfo
            {
                Ip = Request.HttpContext.GetIp(),
                UserEmail = Request.HttpContext.GetUserEmail() ?? "anonymous",
                UserName = Request.HttpContext.GetUserName(),
                IsAdmin = Request.HttpContext.IsAdmin()
            };

            var isApiRequest = HttpContext.Request.Path.StartsWithSegments(new Microsoft.AspNetCore.Http.PathString("/api"));
            if (isApiRequest)
            {
                var apiKey = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (apiKey == null || (apiKey != null && apiKey != API_KEY))
                {
                    HttpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    filterContext.Result = new JsonResult(new { status = (int)HttpStatusCode.Forbidden, message = "Incorrect Api Key" });
                    return;
                }
            }

            if (!(filterContext.ActionDescriptor is ControllerActionDescriptor actionDescription))
                return;

            if (actionDescription.ControllerTypeInfo.GetCustomAttribute(typeof(IgnoreLogActionAttribute)) != null ||
                actionDescription.MethodInfo.GetCustomAttribute(typeof(IgnoreLogActionAttribute)) != null)
                return;

            Task.Factory.StartNew(async () =>
            {
                Console.WriteLine(DateTime.Now);
                await _userActionHistoryRepository.SaveUserActionHistoryAsync(new UserActionHistoryEntity
                {
                    UserEmail = UserInfo.UserEmail,
                    ActionDate = DateTime.UtcNow,
                    ActionName = actionDescription.ActionName,
                    ControllerName = actionDescription.ControllerName,
                    ETag = "*",
                    IpAddress = UserInfo.Ip,
                    Params = filterContext.ActionArguments.Count > 0 ? JsonConvert.SerializeObject(filterContext.ActionArguments) : string.Empty,
                });
                Console.WriteLine(DateTime.Now);
            });
        }

        protected async Task<string> SelfTest(AppSettings appSettings, IUserRepository userRepository, IBlobDataRepository blobDataRepository)
        {
            _userRepository = userRepository;
            _appSettings = appSettings;
            _blobDataRepository = blobDataRepository;

            var sb = new StringBuilder();
            AddCaption(sb, "Self testing for the system.");
            AddHeader(sb, "Check Settings");

            if (TestSettings(sb))
            {
                AddHeader(sb, "Check DataBase connections");
                if (await TestDataBases(sb))
                {
                    AddSuccess(sb, "Self Testing successfully completed");
                }
            }

            return sb.ToString();
        }

        protected virtual void AddSuccess(StringBuilder sb, string selfTestingSuccessfulCompleted)
        {
            sb.AppendLine($"<div class='sfSuccess'>{selfTestingSuccessfulCompleted}</div>");
        }

        protected virtual void AddError(StringBuilder sb, string selfTestingError)
        {
            sb.AppendLine($"<div class='sfError'>{selfTestingError}</div>");
        }

        protected virtual void AddText(StringBuilder sb, string selfTestingText)
        {
            sb.AppendLine($"<div>{selfTestingText}</div>");
        }

        private async Task<bool> TestDataBases(StringBuilder sb)
        {
            return await TestUserRepository(sb) &&
                   await TestBlobDataRepository(sb);
        }

        private async Task<bool> TestBlobDataRepository(StringBuilder sb)
        {
            try
            {
                await _blobDataRepository.GetDataAsync();
            }
            catch (Exception e)
            {
                AddError(sb, $"The 'AccessDataRepository' throws an error {e}.");
                return true;
            }

            AddText(sb, "The 'AccessDataRepository' checked.");
            return true;
        }

        private async Task<bool> TestUserRepository(StringBuilder sb)
        {
            try
            {
                await _userRepository.GetUsers();
            }
            catch (Exception e)
            {
                AddError(sb, $"The 'UserRepository' throws an error {e}.");
                return true;
            }

            AddText(sb, "The 'UserRepository' checked.");
            return true;
        }

        private bool TestSettings(StringBuilder sb)
        {
            return !TestSettingString(sb, nameof(_appSettings.DevCertsService.Db.UserConnectionString), _appSettings.DevCertsService.Db.UserConnectionString) &&
                   !TestSettingString(sb, nameof(_appSettings.DevCertsService.Db.ConnectionString), _appSettings.DevCertsService.Db.ConnectionString) &&
                   !TestSettingString(sb, nameof(_appSettings.DevCertsService.ApiClientId), _appSettings.DevCertsService.ApiClientId) &&
                   TestSettingRx(sb, nameof(_appSettings.DevCertsService.AvailableEmailsRegex), _appSettings.DevCertsService.AvailableEmailsRegex);
        }

        private bool TestSettingRx(StringBuilder sb, string key, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                AddError(sb, $"Key '{key}' is empty");
                return false;
            }

            try
            {
                new Regex(value);
            }
            catch (Exception e)
            {
                AddError(sb, $"Key '{key}' has inncorrect value '{value}' and throws the following exception: {e}");
                return false;
            }

            return true;
        }

        private bool TestSettingString(StringBuilder sb, string key, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                AddError(sb, $"Key '{key}' is empty");
                return false;
            }

            AddText(sb, $"Setting '{key}' checked.");
            return true;
        }

        protected virtual void AddHeader(StringBuilder sb, string checkSettingsHeader)
        {
            sb.AppendLine($"<div class='sfHeader'>{checkSettingsHeader}</div>");
        }

        protected virtual void AddCaption(StringBuilder sb, string checkSettingsCaption)
        {
            sb.AppendLine($"<div class='sfCaption'>{checkSettingsCaption}</div>");
        }

        protected JsonResult JsonErrorValidationResult(string message, string field)
        {
            return new JsonResult(new { status = "ErrorValidation", msg = message, field = field });
        }

        protected JsonResult JsonErrorMessageResult(string message, string field)
        {
            return new JsonResult(new { status = "ErrorMessage", msg = message, field = field });
        }

        protected JsonResult JsonRequestResult(string div, string url, bool showLoading = false, object model = null)
        {
            if (model == null)
                return new JsonResult(new { div, refreshUrl = url, showLoading = showLoading });

            var modelAsString = model as string ?? model.ToUrlParamString();
            return new JsonResult(new { div, refreshUrl = url, prms = modelAsString, showLoading = showLoading });
        }
    }
}
