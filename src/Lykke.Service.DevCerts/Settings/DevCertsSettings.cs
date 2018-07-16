using JetBrains.Annotations;
using Lykke.SettingsReader.Attributes;

namespace Lykke.Service.DevCerts.Settings
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class DevCertsSettings
    {
        public DbSettings Db { get; set; }
        public string ApiClientId { get; set; }
        public string AvailableEmailsRegex { get; set; }
        public string PathToScriptFolder { get; set; }
        public string ScriptName { get; set; }
        public static string Salt { get; set; }
        public string EncryptionPass { get; set; }
        public int UserLoginTime { get; set; }
    }
}
