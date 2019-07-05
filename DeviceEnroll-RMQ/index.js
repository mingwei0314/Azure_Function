function GetEnvironmentVariable(name)
{
    return process.env[name];
}

var ServiceHost = GetEnvironmentVariable("ServiceHost");

module.exports = function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');

    if (req.body) {
        context.res = {
            // status: 200, /* Defaults to 200 */
            body: {
                "serviceHost": ServiceHost,
                "credential": {
                    "protocols": {
                        "mqtt+ssl": {
                            "ssl": true,
                            "port": 8883,
                            "username": "admin",
                            "password": "@dvant1cH"
                        },
                        "mqtt": {
                            "ssl": false,
                            "port": 1883,
                            "username": "admin",
                            "password": "@dvant1cH"
                        }
                    }
                }
            }
        };
    }
    else {
        context.res = {
            status: 400,
            body: "Please pass a name on the query string or in the request body"
        };
    }
    context.done();
};