using System.Collections;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Smpp.Server.Interfaces;

namespace Smpp.Server.Services;

public class ConfigurationCache
{
    public X509Certificate2 ServerCertificate { get; set; }
    
    public IList<PostmanCampaignApiKeyMapping> PostmanCampaignApiKeyMappings { get; set; }
    
    private readonly IKeyVaultService  _keyVaultService;
    private readonly ILogger<ConfigurationCache> _logger;
    
    public ConfigurationCache(IKeyVaultService  keyVaultService, ILogger<ConfigurationCache> logger)
    {
        _keyVaultService = keyVaultService;
        _logger = logger;
        
        this.PostmanCampaignApiKeyMappings = new List<PostmanCampaignApiKeyMapping>();
        this.PostmanCampaignApiKeyMappings = LoadCampaignApiKeyMappings();
        this.ServerCertificate = LoadSslCertificate() ?? throw new Exception("unable to load server certificate");
    }


    private X509Certificate2? LoadSslCertificate()
    {
        //var certificate = _keyVaultService.GetCertificate("smpp-server-ssl", "cpfb1234");
        var serverCertificate = _keyVaultService.GetCertificateFromSecret("smpp-server-ssl-cert", "cpfb1234");
        
        _logger.LogInformation("Loading SSL certificate: Subject:{Subject}", serverCertificate!.Subject);
        
        return serverCertificate;
    }

    private IList<PostmanCampaignApiKeyMapping> LoadCampaignApiKeyMappings()
    {
        var mappingJson = _keyVaultService.GetSecret("smpp-postman-campaign-apikey-mappings");
        
        _logger.LogInformation("Loading postman campaign mappings: {MappingJson}", mappingJson);
        
        return JsonConvert.DeserializeObject<IList<PostmanCampaignApiKeyMapping>>(mappingJson, new JsonSerializerSettings() { ContractResolver = new CamelCasePropertyNamesContractResolver() }) ?? [];
    }
}

public record PostmanCampaignApiKeyMapping(string CampaignId, string ApiKey, string Scheme);