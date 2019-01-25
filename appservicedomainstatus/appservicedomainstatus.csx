
[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "AppServiceDomainStatus", Name = "App Service Domain Status", Description = "Status summary of the domains purchased in this subscription")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    var domainsForSub = await dp.Observer.GetResource($"https://wawsobserver.azurewebsites.windows.net/Subscriptions/{cxt.Resource.SubscriptionId}/Domains");
    int warning = 0;
    int critical = 0;
    int info = 0;
    int success = 0;
    Response criticalResponse = new Response();
    Response warningResponse = new Response();
    Response successResponse = new Response();
    Response infoResponse = new Response();

    foreach(var domain in domainsForSub){

        var domainName = ((string) domain.name);
        var status = ((string) domain.registration_status);
        if(status == "Active"){
            successResponse.AddInsight(InsightStatus.Success, $"{domainName} -- {status}");
            success++;
        } else if (status == "Transferred") {
            infoResponse.AddInsight(InsightStatus.Info,  $"{domainName} -- {status}");
            var body = new Dictionary<string,string>();
            body["Scenario"] = "Domain has been transferred away from Azure.";
            infoResponse.AddInsight(InsightStatus.Info, "Domain is not in an Active functioning state.", body);
            info++;

        } else if (status == "Cancelled") { 
            var body = new Dictionary<string,string>();
            body["Scenario"] = "Domain has been cancelled and is not usable.";
            warningResponse.AddInsight(InsightStatus.Warning,  $"{domainName} -- {status}", body);
            warning++;            
        } else if (status == "Expired") {
            var body = new Dictionary<string,string>();
            body["Scenario"] = "Domain has expired and is not usable.";
            warningResponse.AddInsight(InsightStatus.Warning,  $"{domainName} -- {status}", body);
            warning++;            
        } else if (status == "Pending") {
            var body = new Dictionary<string,string>();
            body["Scenario"] = "Domain registration is pending (setup, renewal, or removal)";
            criticalResponse.AddInsight(InsightStatus.Critical,  $"{domainName} -- {status}", body);
            critical++;            
        } else if (status == "Awaiting") {
            var body = new Dictionary<string,string>();
            body["Scenario"] = "Domain registrar is awaiting validation. Possibly related to incorrect authorization code provided by customer, GoDaddy awaiting email verification. Please see 'Advanced management' on the Domain page inside the Azure portal.";
            criticalResponse.AddInsight(InsightStatus.Critical,  $"{domainName} -- {status}", body);
            critical++;            
        } else if (status == "Held") {
            var body = new Dictionary<string,string>();
            body["Scenario"] = "The customer cancelled the domain or the domain is interally locked due to a legal dispute or the domain has expired and is in the redemption period.";
            criticalResponse.AddInsight(InsightStatus.Critical,  $"{domainName} -- {status}", body);
            critical++;            
        } else {
            criticalResponse.AddInsight(InsightStatus.Critical,  $"{domainName} -- {status}");
            critical++;
        }
    }

    DataSummary successSummary = new DataSummary("Success", success.ToString(), "#007300");
    DataSummary infoSummary = new DataSummary("Info", info.ToString(), "gray");
    DataSummary warningSummary = new DataSummary("Warning", warning.ToString(), "#ff9104");
    DataSummary criticalSummary = new DataSummary("Critical", critical.ToString(), "red");

    res.AddDataSummary(new List<DataSummary>() { successSummary, infoSummary, warningSummary, criticalSummary }); 

    res.Dataset.AddRange(criticalResponse.Dataset);
    res.Dataset.AddRange(warningResponse.Dataset);
    res.Dataset.AddRange(infoResponse.Dataset);
    res.Dataset.AddRange(successResponse.Dataset);
    
    return res;
}

