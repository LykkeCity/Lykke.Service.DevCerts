using Lykke.HttpClientGenerator;

namespace Lykke.Service.DevCerts.Client
{
    public class DevCertsClient : IDevCertsClient
    {
        //public IControllerApi Controller { get; }
        
        public DevCertsClient(IHttpClientGenerator httpClientGenerator)
        {
            //Controller = httpClientGenerator.Generate<IControllerApi>();
        }
        
    }
}
