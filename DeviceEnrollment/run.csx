using System.Net;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Common.Security;
//----------------------------------------------------------//
public class SAStoken
{
    public string SAS { get; set; }
}

public class Mqtt
{
    public bool ssl { get; set; }
    public int port { get; set; }
    public string username { get; set; }
    public string password { get; set; }
}

public class Protocols
{
    public Mqtt mqtt { get; set; }
    [JsonProperty("mqtt+ssl")]
    public Mqtt mqttssl { get; set; }
}

public class Credential
{
    public Protocols protocols { get; set; }
}

public class CredentialRoot
{
    public string serviceHost { get; set; }
    public string paasSolution { get; set; } = "Azure-Paas";
    public Credential credential { get; set; }
}

public class RequestRoot
{
    public string deviceId { get; set; } = null;
    public bool auto_enrollment { get; set; } = false;
    public int ttl { get; set; } = 4000;
}
//----------------------------------------------------------//
public static string GetEnvironmentVariable(string name)
{
    return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
}

public static string GetServiceHostUrl() {

    return GetEnvironmentVariable("GlobalConnectString").Split(Convert.ToChar(";"))[0].Substring(9);
}

public static string GetKeyUrl() {
    return "https://" + GetEnvironmentVariable("WEBSITE_HOSTNAME") + "/api/" + GetEnvironmentVariable("WEBSITE_SITE_NAME")+"/";
}
//==========================================================//
public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info("C# HTTP trigger function processed a request.");
    log.Info(GetKeyUrl());
    // parse query parameter
    string deviceId = null;
    bool auto_enrollment = false;
    int ttl = 4000;

    try
    {
        dynamic requestBody = await req.Content.ReadAsStringAsync();
        var rr = JsonConvert.DeserializeObject<RequestRoot>(requestBody as string);
        deviceId = rr.deviceId;
        auto_enrollment = rr.auto_enrollment;
        ttl = rr.ttl;
        log.Info($"deviceId = {deviceId}");
        log.Info($"auto_enrollment = {auto_enrollment}");
        log.Info($"ttl = {ttl}");
    }
    catch (Exception er) {
        log.Info($"Error: {er.Message}");
        return req.CreateResponse(HttpStatusCode.BadRequest, "Error: Please pass a correct payload in the request body.");
    }

    if(deviceId == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, "Error: Please pass a correct payload in the request body.");

    /*SAStoken root = new SAStoken(); // just a sample object
    root.SAS = getSASToken(deviceId, log).Result;
    */

    CredentialRoot root = new CredentialRoot();
    root.credential = new Credential();
    root.credential.protocols = new Protocols();
    root.credential.protocols.mqtt = new Mqtt();
    root.credential.protocols.mqttssl = new Mqtt();

    root.serviceHost = GetServiceHostUrl();
    root.credential.protocols.mqtt.ssl = false;
    root.credential.protocols.mqtt.port = 1883;
    root.credential.protocols.mqtt.username = "N/A";
    root.credential.protocols.mqtt.password = "N/A";

    root.credential.protocols.mqttssl.ssl = true;
    root.credential.protocols.mqttssl.port = 8883;
    root.credential.protocols.mqttssl.password = getSASToken(deviceId, auto_enrollment, log, ttl).Result;
    root.credential.protocols.mqttssl.username = root.serviceHost + "/" + deviceId;
    
    var json = JsonConvert.SerializeObject(root, Formatting.Indented);
 
    if(deviceId == null) {
        return req.CreateResponse(HttpStatusCode.BadRequest, "Error: No deviceId on the query string.");
    } else {
        return new HttpResponseMessage(HttpStatusCode.OK) 
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

}

private static async Task<String> getSASToken(string deviceId, bool auto_enrollment, TraceWriter log,  int ttl = 4000)
{
    string sasString = "N/A";
    string key = null;
    string iotHub = null;
    try
    {

        ////string connectionString = "HostName=iothub4l47667r6ogik.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=wYVV17HyyETCWvDNVAmaJyOKkPJPcC+UbUD9kZ9IeJM=";
        //string connectionString = "HostName=iothub4l47667r6ogik.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=e0hqeP+EeVb7tbLgISdcL2d5K+cI5wpvVuZseXx90CM=";
        //string connectionString = "HostName=device-pool.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=Fuhhsi0bETy/Sx/dydtqObqL4yZKqq8vhkiaK1ncBS0=";
        string connectionString = GetEnvironmentVariable("GlobalConnectString");
        RegistryManager registryManager = RegistryManager.CreateFromConnectionString(connectionString);
        Device myDevice = await registryManager.GetDeviceAsync(deviceId);
        
        //Create Devcie
        if(myDevice == null && auto_enrollment) {
            log.Info("Device is null");
            
            Device device = new Device(deviceId);
            log.Info($"device is {device}");
            try
            {
                myDevice = await registryManager.AddDeviceAsync(device);
            }
            catch (Exception er)
            {
                log.Info($"Error: {er.Message}");
                myDevice = await registryManager.GetDeviceAsync(deviceId);
                if(myDevice == null) {
                    log.Info("Device is null again");
                    return $"Error: {er.Message}";
                }
            }
            log.Info($"Generated device key: {myDevice.Authentication.SymmetricKey.PrimaryKey}");
        }

        if(myDevice != null) {
            key = myDevice.Authentication.SymmetricKey.PrimaryKey;
            iotHub = connectionString.Split(Convert.ToChar(";"))[0].Substring(9);
            log.Info($"Generated SAS for: {deviceId}.  Key not provided, using connectionString.");
        }

        if(key != null) {
            SharedAccessSignatureBuilder sasBuilder = new SharedAccessSignatureBuilder()
            {
                Key = key,
                Target = String.Format("{0}/devices/{1}", iotHub, System.Net.WebUtility.UrlEncode(deviceId)),
                TimeToLive = TimeSpan.FromDays(ttl)
            };
            sasString = sasBuilder.ToSignature();
        }
    }
    catch (Exception er) {
        log.Info($"Error: {er.Message}");
    }
    return sasString;
}
