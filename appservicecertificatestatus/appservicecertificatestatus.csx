
[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "AppServiceCertificateStatus", Name = "Certificate KV Status", Description = "KeyVault status summary of all certificates configured for App Service Apps in the subscription")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    var certsForSub = await dp.Observer.GetResource($"https://wawsobserver.azurewebsites.windows.net/Subscriptions/{cxt.Resource.SubscriptionId}/Certificates");

    int warning = 0;
    int critical = 0;
    int success = 0;
    int info = 0;

    Response criticalResponse = new Response();
    Response warningResponse = new Response();
    Response successResponse = new Response();
    Response infoResponse = new Response();

    foreach(var cert in certsForSub){

        var certName = ((string) cert.thumbprint);
        var keyVaultStatus = ((string) cert.key_vault_secret_status);

        if(keyVaultStatus == "Initialized" || keyVaultStatus == "Succeeded"){
            successResponse.AddInsight(InsightStatus.Success, $"{certName} -- In Sync");
            success++;
        } else if (keyVaultStatus == "KeyVaultDoesNotExist") {
            var body = new Dictionary<string,string>();
            body["Details"] = "Key Vault has been either been deleted or moved, please reconfigure the certificate to point to a valid KeyVault";
            criticalResponse.AddInsight(InsightStatus.Critical, $"{certName} - KeyVault is missing.", body);
            critical++;
        } else if (keyVaultStatus == "OperationNotPermittedOnKeyVault" || keyVaultStatus == "AzureServiceUnauthorizedToAccessKeyVault") {
            var body = new Dictionary<string,string>();
            body["Details"] = "App Service needs proper access to the Key Vault to store and retrive certificate from the secret. Make sure your KeyVault has GET and SET permissions configured for Service Principal : Microsoft.Azure.WebSites (abfa0a7c-a6b6-4736-8310-5855508787cd)";
            criticalResponse.AddInsight(InsightStatus.Critical, $"{certName} - KeyVault permissions not set properly.", body);
            critical++;
        } else if (keyVaultStatus == "KeyVaultSecretDoesNotExist") {
            var body = new Dictionary<string,string>();
            body["Details"] = "Key Vault secret that contains the SSL certificate has been either been deleted or moved, please reconfigure the certificate to point to a valid KeyVault.";
            criticalResponse.AddInsight(InsightStatus.Critical, $"{certName} - KeyVault secret is missing.", body);
            critical++;
        } else if (keyVaultStatus == "UnknownError") {
            var body = new Dictionary<string,string>();
            body["Details"] = "Key Vault cannot be reached with the configured permissions. Check if there is an outage for the KeyVault service. Please contact support if the error persists .";
            criticalResponse.AddInsight(InsightStatus.Critical, $"{certName} - KeyVault is unreachable.", body);
            critical++;
        } else if (string.IsNullOrEmpty(keyVaultStatus)) {
            var body = new Dictionary<string,string>();
            body["Details"] = "Certificate is manually uploaded, customer is responsible to maintaining the life cycle of the certificate";
            infoResponse.AddInsight(InsightStatus.Info, $"{certName} - Uploaded Certificate", body);
            info++;
        }
    }

    DataSummary successSummary = new DataSummary("Success", success.ToString(), "#007300");
    DataSummary warningSummary = new DataSummary("Warning", warning.ToString(), "#ff9104");
    DataSummary criticalSummary = new DataSummary("Critical", critical.ToString(), "red");
    DataSummary infoSummary = new DataSummary("Info", info.ToString(), "gray");

    res.AddDataSummary(new List<DataSummary>() { successSummary, warningSummary, criticalSummary, infoSummary }); 

    res.Dataset.AddRange(criticalResponse.Dataset);
    res.Dataset.AddRange(warningResponse.Dataset);
    res.Dataset.AddRange(successResponse.Dataset);
    res.Dataset.AddRange(infoResponse.Dataset);
    
    return res;
}

