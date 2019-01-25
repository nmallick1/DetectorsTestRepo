[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "observerexamples", Name = "Sample Observer Detector", Author = "hawfor", Description = "Show examples of how to use Observer as a data provider")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    try{
        var siteInfo = await WawsObserver_GetSite(cxt, dp);
        var serverFarmName = (string)siteInfo.server_farm.server_farm_name;
        var sitesInAppServicePlan = await SupportObserver_GetSitesInServerFarm(dp, cxt.Resource.SubscriptionId, serverFarmName);
        foreach(var siteData in sitesInAppServicePlan){
            res.AddMarkdownView((string)siteData.SiteName);
        }
    }catch(Exception e){
        res.AddMarkdownView(e.Message);
    }
    return res;
}

//This uses dp.Observer.GetResource() which takes in a string path to the WawsObserver URL and returns a JSON
//wrapped in a dynamic type
private async static Task<dynamic> WawsObserver_GetSite(OperationContext<App> cxt, DataProviders dp){
    var sites = await dp.Observer.GetResource($"https://wawsobserver.azurewebsites.windows.net/sites/{cxt.Resource.Name}");
    return sites[0];
}

private async static Task<IEnumerable<dynamic>> SupportObserver_GetSitesInServerFarm(DataProviders dp, string subscriptionId, string serverFarmName){
    var sitesInAppServicePlan = await dp.Observer.GetSitesInServerFarm(subscriptionId, serverFarmName);
    return sitesInAppServicePlan;
}