using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.Service.DevCerts.AzureRepositories.Extentions
{
    internal static class DateTimeExtensions
    {
        internal static string StorageString(this DateTime datetime)
        {
            return datetime.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }
    }
}
