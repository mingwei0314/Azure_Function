var common = require('azure-iot-common');
var iothub = require('azure-iothub');
var DD = require('azure-iothub').DeviceDescription;
var SAS = require('azure-iot-common').SharedAccessSignature;

function GetEnvironmentVariable(name)
{
    return process.env[name];
}

//var connectionString = 'HostName=iothub4l47667r6ogik.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=e0hqeP+EeVb7tbLgISdcL2d5K+cI5wpvVuZseXx90CM=';
//var connectionString = "HostName=device-pool.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=Fuhhsi0bETy/Sx/dydtqObqL4yZKqq8vhkiaK1ncBS0=";
var connectionString = GetEnvironmentVariable("GlobalConnectString");
var registry = iothub.Registry.fromConnectionString(connectionString);
var cs = common.ConnectionString.parse(connectionString, ['HostName']);

function genSasResult(context, hostname, deviceId, key) {
    var timeStampInMs = Math.round(Date.now()/1000);
    var targetName = hostname + "%2Fdevices%2F" + deviceId;
    var sas = SAS.create(targetName, deviceId, key, timeStampInMs+300000000);
    var sasString = "SharedAccessSignature sr=" + targetName + "&sig=" + sas.sig + "&se=" + sas.se;
    return {
        "serviceHost": hostname,
        "paasSolution": "Azure-Paas",
        "credential": {
            "protocols": {
                "mqtt+ssl": {
                    "ssl": true,
                    "port": 8883,
                    "username": hostname + "/" + deviceId,
                    "password": sasString
                },
                "mqtt": {
                    "ssl": true,
                    "port": 8883,
                    "username": hostname + "/" + deviceId,
                    "password": sasString
                }
            }
        }
    };
}

module.exports = function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');
    //context.log(req);
    if (req.body && req.body.deviceId && req.body.auto_enrollment) {
        registry.get(req.body.deviceId, function (err, dev) {
            //context.log('err = ', err);
            if(err != null) {
                var deviceInfo = {
                    deviceId: req.body.deviceId
                };
                registry.create(deviceInfo, (err, dev) => {
                    //context.log(dev);
                    context.res = {
                        body: genSasResult(context,cs.HostName, req.body.deviceId, dev.authentication.symmetricKey.primaryKey)
                    };
                    context.done();
                });
            } else {
                context.log(dev.authentication.symmetricKey);
                context.log(cs.HostName);
                context.log(req.body.deviceId);
                context.res = {
                    body: genSasResult(context,cs.HostName, req.body.deviceId, dev.authentication.symmetricKey.primaryKey)
                };
                context.done();
            }
        });
    }
    else {
        context.res = {
            status: 400,
            body: "Please pass a name on the query string or in the request body"
        };
        context.done();
    }
};