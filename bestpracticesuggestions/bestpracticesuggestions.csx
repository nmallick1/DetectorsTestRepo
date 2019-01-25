using System.Linq;

private static string GetAutoScaleProfileQuery(OperationContext<App> cxt, dynamic observerSite)
{
    var subscriptionId = cxt.Resource.SubscriptionId;    
    //var resourceGroup = cxt.Resource.ResourceGroup;
    //var siteName = cxt.Resource.Name;
    var serverFarm = (string)observerSite.server_farm.server_farm_name;

    return $@"cluster('azureinsights').database('Insights').JobTraces 
    | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)} 
    | where jobPartition  =~ '{subscriptionId}' and operationName =~ 'GetAutoscaleProfile' 
    | parse message with * ""Profile: '"" Profile ""'"" * 
    | where message contains '{serverFarm}'
    | summarize count() by Profile 
    | project Profile";
}


private static string Get500_121CountForPastDayQuery(OperationContext<App> cxt)
{
    string siteFilter = Utilities.HostNamesFilterQuery(cxt.Resource.Hostnames);
    return 
    $@" StatsDWASWorkerProcessTenMinuteTable | where TIMESTAMP >= ago(1h)
    | where ApplicationPool == '{ cxt.Resource.Name }' or ApplicationPool startswith '{ cxt.Resource.Name }__' 
    | summarize by RoleInstance, Tenant
    | join kind= inner (
        AntaresIISLogWorkerTable | where TIMESTAMP >= ago(1h)
        | where {siteFilter}
        | where Sc_status == 500 and Sc_substatus == 121   
    ) on RoleInstance, Tenant
    | summarize ['Count'] =  count() by bin(PreciseTimeStamp, 5m)
    | order by PreciseTimeStamp asc";
}

private static string GetSimultaneousSitesHostedCountQuery(OperationContext<App> cxt, string appPoolFilter)
{
    return 
    $@"
    StatsDWASWorkerProcessTenMinuteTable | where TIMESTAMP >= ago(30m)
    | where {appPoolFilter}
    | where ApplicationPool !startswith '~1' and ApplicationPool !startswith 'mawscanary'
    | summarize by ApplicationPool
    | summarize count()
    | where count_ > 15
    ";
}

private static string GetAlwaysOnResponseCodeQuery(OperationContext<App> cxt)
{
    string siteFilter = Utilities.HostNamesFilterQuery(cxt.Resource.Hostnames);
    return 
    $@"AntaresIISLogFrontEndTable | where PreciseTimeStamp >= ago(10m)
    | where { siteFilter }
    | where User_agent == 'AlwaysOn'
    | where Sc_status != 200
    | summarize count() by Sc_status
    | order by Sc_status asc";
}



    public static int warning = 0;
    public static int critical = 0;
    public static int success = 0;


[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows | PlatformType.Linux, StackType = StackType.All, InternalOnly = false)]
[Definition(Id = "bestpracticesuggestions", Name = "Check Best Practices for Prod Apps",  Author = "nmallick", Description = "Evaluating this web application to determine if it follows the best practices for enhanced stability and reliability.")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
	//Initializing again since everytime the browser is refreshed the old value is remembered and the count just keeps incrementing displaying incorrect value
	warning = 0;
	critical = 0;
	success = 0;
//try{

    var siteData = (await dp.Observer.GetSite(cxt.Resource.Stamp.Name, cxt.Resource.Name))[0];
    var appServicePlan = siteData.server_farm.virtual_farm;

    Response criticalResponse = new Response();
    Response warningResponse = new Response();
    Response successResponse = new Response();

    #region Check if the site is running on a Standard tier
    bool isFree = (siteData.sku == "Free" || siteData.sku == "Shared" || siteData.sku == "Basic"); 
    if(isFree)
    {
        var body = new Dictionary<string,string>();
        body["Insight"] = "Consider running a production application on a Standard, Premium, or Isolated App Service Plan for better performance and isolation. These SKUs are best for production workloads, but there are other SKU options (Free, Shared, Basic) if you are still in testing mode.";
        body["Learn more"] = @"<br/><a target=""_blank"" href=""https://azure.microsoft.com/en-us/pricing/details/app-service/"">Azure App Service Plan Pricing Information</a>";

        successResponse.AddInsight(InsightStatus.Critical, "Running on an App Service Plan for Production Workloads", body);
        critical++;
    }else{   
        var body = new Dictionary<string,string>();
        body["Insight"] = "Your web app is running on a SKU that is optimized for production workloads. Good choice!";
        body["Learn more"] = @"<a target=""_blank"" href=""https://azure.microsoft.com/en-us/pricing/details/app-service/"">Azure App Service Plan Pricing Information</a>";

        successResponse.AddInsight(InsightStatus.Success, "Running on an App Service Plan for Production Workloads", body);
        success++;
    }
    #endregion
    

    #region Always Deploy to a Slot
    //Check if the site has a slot, if it does then we are assuming that the deployment was is being against that slot and not the production slot
    if(siteData.slots.Count > 1){
        //Can we check if the code is being deployed against the production slot in the last 10 days and flag it out?
        //Do we check AdminSubscriptionAuditEvents / Kudu / MSDeploy / Publisher table ? Anything else ?
        var body = new Dictionary<string,string>();
        body["Insight"] = @"<span style=""font-weight: 700;color: #60ab1d;"">Great job</span>. This site has slots associated with it. Always deploy new code to a slot. Swap the code into production only once you confirm that this new code works the way that you expected.";
        body["Learn more"] = @"<br/><a target=""_blank"" href=""https://docs.microsoft.com/en-us/azure/app-service/web-sites-staged-publishing"">Setup staging environments in Azure App Service</a>";

        successResponse.AddInsight(InsightStatus.Success, "Using Deployment Slots", body);
        success++;
    }else{
        var body = new Dictionary<string,string>();
        body["Scenario"] = "It is recommended to have at least one slot configured for your site. You can validate app changes in a staging deployment slot before swapping it with the production slot. This will help prevent introduction of breaking changes in your production web app. Also, deploying to a slot protects your web app against undesired deployment downtimes.";
        body["Solution"] = "Create an additional slot by going to Deployment Slots in the left-hand menu. For example, create a staging slot, where you will deploy new code for testing. When everything looks production-ready, swap your production slot with the staging slot. This will help make sure that the code only goes to production once you confirm that this new code works on the way that you expected.";
        body["Learn more"] = @"<br/><a target=""_blank"" href=""https://docs.microsoft.com/en-us/azure/app-service/web-sites-staged-publishing"">Setup staging environments in Azure App Service</a>";

        criticalResponse.AddInsight(InsightStatus.Critical, "No Deployment Slots", body);
        critical++;
    }
    #endregion

    #region Make sure that the site has more than one workers assigned to it
     if(siteData.web_workers.Count > 1){

        var body = new Dictionary<string,string>();
        body["Scenario"] = "Great, your web app is running on at least two instances. This is optimal because instances in different upgrade domains will not be upgraded at the same time. While one worker instance is getting upgraded the other is still active to serve web requests.";
        body["Learn more"] =  @"<br/><a target =""_blank"" href = ""https://docs.microsoft.com/en-us/azure/monitoring-and-diagnostics/insights-how-to-scale"">Scale instance count manually or automatically</a>";

        successResponse.AddInsight(InsightStatus.Success, "Distributing Your Web App Across Multiple Instances", body);
        success++;
    }else{
        var body = new Dictionary<string,string>();
        body["Scenario"] = "Since you have only one instance you can expect downtime because when the App Service platform is upgraded, the instance on which your web app is running on will be upgraded. Therefore, your web app process will be restarted and may experience some downtime.";
        body["Solution"] = "Scale out to at least two instances. Both of these instances are in two different upgrade domains and hence will not be upgraded at the same time. While one worker instance is getting upgraded the other is still active to serve web requests.";
        body["Learn more"] =  @"<br/><a target =""_blank"" href = ""https://docs.microsoft.com/en-us/azure/monitoring-and-diagnostics/insights-how-to-scale"">Scale instance count manually or automatically</a>";

        warningResponse.AddInsight(InsightStatus.Warning, "Distributing Your Web App Across Multiple Instances", body);
        warning++;
    }
    #endregion

    #region Check if Traffic Manager is enabled
    bool trafficManagerEnabled = false;
    foreach(dynamic h in siteData.hostnames){
        if(((string)h.hostname).Contains("trafficmanager.net")){
            trafficManagerEnabled = true;
            break;
        }
    }

    if(trafficManagerEnabled){
        var body = new Dictionary<string,string>();
        body["Scenario"] = "You have configured Azure Traffic Manager. This is great because you have deployed your web app in multiple geo locations for better performance and reliability.";
        body["Learn more"] =  @"<br/><li/><a target=""_blank"" href = ""https://docs.microsoft.com/en-us/azure/traffic-manager/traffic-manager-overview"">Overview of Traffic Manager</a>
        <li/><a target =""_blank"" href = ""https://docs.microsoft.com/en-us/azure/app-service/web-sites-traffic-manager"">Controlling Azure App Service traffic with Azure Traffic Manager</a>";

        successResponse.AddInsight(InsightStatus.Success, "Azure Traffic Manager Configured", body);
        success++;
    }else{
        var body = new Dictionary<string,string>();
        body["Scenario"] = "If your users browse the site from a geographic locations other than where the website is hosted, they may experience delay accessing the site. Also, if the site is deployed only in one region, it is susceptible to downtime in the case of a region-wide outage.";
        body["Solution"] = "Deploy the site in multiple geo locations and configure Traffic Manager for better performance and reliability.";
        body["Learn more"] =  @"<br/><li/><a target=""_blank"" href = ""https://docs.microsoft.com/en-us/azure/traffic-manager/traffic-manager-overview"">Overview of Traffic Manager</a>
        <li/><a target =""_blank"" href = ""https://docs.microsoft.com/en-us/azure/app-service/web-sites-traffic-manager"">Controlling Azure App Service traffic with Azure Traffic Manager</a>";

        warningResponse.AddInsight(InsightStatus.Warning, "Azure Traffic Manager Not Configured", body);
        warning++;
    }
    #endregion

    #region ARRAffinity
    if(siteData.client_affinity_enabled == false)
    {
        var body = new Dictionary<string,string>();
        body["Scenario"] = "Good job. With ARR Affinity disabled, there is more equal distribution of traffic across various worker instances. If your application needs the client to be tied to a worker, then please re-enable ARR Affinity.";
        body["Learn more"] =  @"<br/><a target =""_blank"" href = ""https://blogs.msdn.microsoft.com/benjaminperkins/2016/06/03/setting-application-request-routing-arr-affinity-for-your-azure-app-service/"">Setting Application Request Routing – ARR Affinity for your Azure App Service</a>";

        successResponse.AddInsight(InsightStatus.Success, "ARR Affinity is Disabled", body);
        success++;
    }else{
        var body = new Dictionary<string,string>();
        body["Scenario"] = "With ARR Affinity enabled, a client is tied to a specific web worker resulting in unequal distribution of traffic across various worker instances.";
        body["Solution"] = "Some applications need the client to be tied to a worker for them to work e.g.. Applications using In-Process session. If this is not the case, disable ARR Affinity to achieve a more even load distribution.";
        body["Learn more"] =  @"<br/><a target =""_blank"" href = ""https://blogs.msdn.microsoft.com/benjaminperkins/2016/06/03/setting-application-request-routing-arr-affinity-for-your-azure-app-service/"">Setting Application Request Routing – ARR Affinity for your Azure App Service</a>";

        warningResponse.AddInsight(InsightStatus.Warning, "ARR Affinity is Enabled", body);
        warning++;
    }
    #endregion

    #region Check for Always ON
    if(siteData.always_on == true)
    {
        var alwaysOnResponseCodes = await dp.Kusto.ExecuteQuery(GetAlwaysOnResponseCodeQuery(cxt), cxt.Resource.Stamp.Name);
        if(alwaysOnResponseCodes.Rows.Count > 0)
        {
            
            var body = new Dictionary<string,string>();
            body["Insight"] = "AlwaysOn is not returning with a 200 OK as a result it may not be actually hitting your application code. Although the process hosting the application will remain active, the actual application may not. Implement an endpoint that listens to '/' endpoint";

            warningResponse.AddInsight(InsightStatus.Warning, "AlwaysOn Enabled but may not be completely effective", body);
            warningResponse.Dataset.Add(new DiagnosticData()
            {
                Table = alwaysOnResponseCodes,
                RenderingProperties = new Rendering(RenderingType.Table){Title = "Summary of HTTP codes for AlwaysOn requests in the past 10 mins", Description = "AlwaysOn requests to the application completed with the following response codes"}
            });
            warning++;

        }
        else
        {            
            successResponse.AddInsight(InsightStatus.Success, "AlwaysOn Enabled");
            success++;
        }

        #region Check to see if the site has https_only set to true
        if(siteData.https_only == true)
        {
            var body = new Dictionary<string,string>();
            body["Scenario"] = "With HTTPS enabled with AlwaysOn, AlwaysOn requests too will be be forced redirected to use HTTPS and hence will fail to achieve their desired purpose.";
            body["Solution"] = "Disable HTTPS via the portal and implement a URL Rewrite rule to force HTTP to HTTPS redirection for all requests except where UserAgent is AlwaysOn.";
            body["Learn more"] = @"<br/><li/><a href=""_blank"" href=""https://blogs.msdn.microsoft.com/benjaminperkins/2014/01/07/https-only-on-windows-azure-web-sites/"">HTTP to HTTPS redirect via URLRewrite</a>
            <li/><a href=""_blank"" href=""https://blogs.msdn.microsoft.com/jpsanders/2017/04/28/iis-azure-web-app-force-https-alwayson-ping-should-return-200-for-fastcgi-sites/"">Exclude AlwaysOn request from URLRewrite rules</a>";

            criticalResponse.AddInsight(InsightStatus.Critical, "HTTP to HTTPS redirection Enabled with AlwaysOn", body);
            critical++;
        }
        else
        {
            var body = new Dictionary<string,string>();
            body["Scenario"] = "Good job! With HTTPS disabled with AlwaysOn, AlwaysOn requests will not be forced to redirect to use HTTPS.";
            body["Learn more"] = @"<br/><li/><a href=""_blank"" href=""https://blogs.msdn.microsoft.com/benjaminperkins/2014/01/07/https-only-on-windows-azure-web-sites/"">HTTP to HTTPS redirect via URLRewrite</a>
            <li/><a href=""_blank"" href=""https://blogs.msdn.microsoft.com/jpsanders/2017/04/28/iis-azure-web-app-force-https-alwayson-ping-should-return-200-for-fastcgi-sites/"">Exclude AlwaysOn request from URLRewrite rules</a>";

            successResponse.AddInsight(InsightStatus.Success, "HTTP to HTTPS redirection Disabled with AlwaysOn", body);
            success++;
        }
        #endregion
    }
    else
    {
        var body = new Dictionary<string,string>();
        body["Scenario"] = "If the site is inactive for an extended period of time, the process assiciated with it is shut down to conserve resources. Subsequent requests following a long idle time, may take longer to respond as the process has to be re-started and the site re-initialized.";
        body["Solution"] = "For applications that are accessed infrequently and are sensitive to start-up delay's, please enable AlwaysOn.";

        criticalResponse.AddInsight(InsightStatus.Critical, "AlwaysOn Disabled", body);
        critical++;
    }
    #endregion

    #region Is the site hosted on an AppService Plan that hosts more than 15 active sites ?
    //Observer has this data too, however relying on Kusto to look for sites that are active
    //Sites may be added to a given ASP but may be idle and hence they do not negetively impact a sites performance at that point in time
    //Check for Observer data would be if ((appServicePlan.websites.Count -1)>15) --> Do a -1 for the count since one of the sites is always Canary
	
	if(!isFree)
    {

		string appPoolFilter = "";

		foreach(dynamic site in appServicePlan.websites){
			appPoolFilter = appPoolFilter + "or ApplicationPool == '" + (string)site.name + "' " ;
		}
		appPoolFilter = "where " + appPoolFilter;
		appPoolFilter = appPoolFilter.Replace("where or", "");
		
		var simultaneousSitesHostedCount = await dp.Kusto.ExecuteQuery(GetSimultaneousSitesHostedCountQuery(cxt, appPoolFilter), cxt.Resource.Stamp.Name);
		
		if(simultaneousSitesHostedCount.Rows.Count > 0)
		{
			var body = new Dictionary<string,string>();
			body["Scenario"] = "Sites that are a part of the same App Service Plan share their resources. If the site is hosted on an App Service Plan that hosts more than 15 sites, given that all those sites will compete for resources it may result in poor performance for your application.";
			body["Solution"] = "For production applications, it is recommended that an App Service plan does not exceed more than 15 sites. The number may actually be lower depending on how resource intensive the hosted applications are.";
			body["Learn more"] = @"<br/><a target= ""_blank"" href=""https://docs.microsoft.com/en-us/azure/app-service/azure-web-sites-web-hosting-plans-in-depth-overview"">Azure App Service plan overview</a>";

			criticalResponse.AddInsight(InsightStatus.Critical, "App Service Plan hosts more than 15 active sites", body);
			critical++;
		}
		else
		{
			var body = new Dictionary<string,string>();
			body["Scenario"] = "For production applications, it is recommended that an App Service plan does not exceed more than 15 sites. The number may actually be lower depending on how resource intensive the hosted applications are.";
			body["Learn more"] = @"<br/><a target= ""_blank"" href=""https://docs.microsoft.com/en-us/azure/app-service/azure-web-sites-web-hosting-plans-in-depth-overview"">Azure App Service plan overview</a>";

			successResponse.AddInsight(InsightStatus.Success, "App Service Plan hosts less than 15 active sites", body);
			success++;        
		} 
    }
    #endregion


    #region Is App Service Plan running the max number of workers for its tier ?
    
    int maxAllowedWorkersForSku = 0;
    string currentAppSKU = siteData.sku;

    switch(currentAppSKU)
    {
        case "Basic" : 
            maxAllowedWorkersForSku = 3;
            break;
        case "Standard":
            maxAllowedWorkersForSku = 10;
            break;
        case "Premium" :
            maxAllowedWorkersForSku = 20;
            break;
        default : 
            //This will be true for ASE where sku = Isolated
            //TODO Need to figure out what value should be set here.            
            maxAllowedWorkersForSku = -1;
            break;
    }

    if (maxAllowedWorkersForSku<0)
    {
        if(currentAppSKU == "Free")
        {
            //NOOP. Currently assuming that the detector can be fired even for a FREE webapp. This check is really invalid for a Free tier webapp
        }
        else
        {
            //res.AddInsight(InsightStatus.Critical, "Unrecognized SKU detected. Please report this error");
            if(currentAppSKU == "Isolated")
            {
                //This is an ASE.. Need to figure out how to perform a check for ASE to see if the ASP can scale more or not on an ASE
                //TODO here.. Currently NOOP
            }
            else
            {
                criticalResponse.AddInsight(InsightStatus.Critical, "Unrecognized SKU detected. Please report this error");
                critical++;
            }
        }
    }
    else
    {
        //This will not get called for ASE since maxAllowedWorkersForSku will be evaluated as -1 for an ASE
        int scaleOutCapacity = maxAllowedWorkersForSku - ((int)appServicePlan.current_number_of_workers) ;
        if(scaleOutCapacity == 0)
        {

            //ASP is running at the max number of workers available available for the current pricing tier. Seek out optimizations
            //Anything lower than Premium can be upgraded to the next higher tier to apps from ASP can be segregated into their own individual ASP's (provided the ASP is serving more than one app)
            //For Premium, the option would be to consider app optimizations or segregating the app's into their own ASP's (if the ASP is hosting more than one app)

            var body = new Dictionary<string,string>();
            body["Scenario"] = "App Service Plan hosting this webapp is running on maximum number of worker instances allowed for this tier. This leaves no more room to scale out should there be a need to do so.";
            
            if(currentAppSKU == "Premium")
            {
                if((appServicePlan.websites.Count -1)>1)
                {
                    //ASP hosts more than one application so there is a possiblity to segregate the app's into their own ASP's
                    body["Solution"] = "The App Service Plan hosting this webapp hosts <strong>" + (appServicePlan.websites.Count -1).ToString() + "</strong> web applications. It might be possible to segregate some of the applications into their own App Service Plans and scale down the number of workers for this App Service Plan creating room for scale out should the need arise. Also, assess the applications for possible optimization prospects.";                    
                }
                else
                {
                    //ASP hosts only one webapp so app optimizations is the only alternative
                    body["Solution"] = "Please assess the application for possible optimization prospects that can enable it to perform at the same standards, but on fewer instances. ";
                }
            }
            else
            {
                //SKU is either Basic / Standard
                body["Solution"] = "It might be possible to scale up to the next higher tier but with fewer instances and achieve the same performance. This can help create room for additional scale out should the need arise. Also, assess the applications for possible optimization prospects.";
            }
            body["Learn more"] = @"<br/><li/><a target= ""_blank"" href=""https://docs.microsoft.com/en-us/azure/app-service/azure-web-sites-web-hosting-plans-in-depth-overview"">Azure App Service plan overview</a>
                    <li/><a target=""_blank"" href=""https://docs.microsoft.com/en-us/azure/app-service/app-service-diagnostics"">Azure App Service diagnostics overview</a>";

            criticalResponse.AddInsight(InsightStatus.Critical, "App Service Plan running on max number of allowed instances for this tier", body);
            critical++;
        }
        else
        {


            
            #region Check for AutoScale

            //HERE CHECK TO SEE IF THE WEBAPP HAS AUTO SCALE RULE ENABLED. IF IT DOES, THEN CHECK THE MAXIMUM SET AND MAKE SURE THAT THE SITE IS NOT ALREADY RUNNING AT OR MORE THAN THE MAX SET BY AUTOSCALE. ALSO CHECK IF AUTOSCALE MAX IS MORE THAN THE MAX INSTANCES ALLOWED BASED ON PRICING TIER ?

            var autoScaleProfiles = await dp.Kusto.ExecuteQuery(GetAutoScaleProfileQuery(cxt, siteData), cxt.Resource.Stamp.Name);
            var profilesTable = GetAutoScaleProfileTable(autoScaleProfiles, criticalResponse, warningResponse, successResponse, siteData);            
            
            #endregion



            //Check to see if there is scope to scale out by 20 % more. If not, flag as a warning else things are fine and flag as success
            if(((scaleOutCapacity*1.0/maxAllowedWorkersForSku)*100) < 20)
            {
                var body = new Dictionary<string,string>();
                body["Scenario"] = "App Service Plan hosting this webapp can support less than 20% additional capacity before it reaches its limit for maximum number of worker instances allowed for this pricing tier.";
                
                if((appServicePlan.websites.Count -1)>1)
                {
                   body["Solution"] = "Consider seperating applications into their own app service plans and scale down the current app service plan to allow for more room. Also, assess the applications for possible optimization prospects.";
                }
                else
                {
                    body["Solution"] = "It might be possible to scale up to the next higher tier but with fewer instances and achieve the same performance. This can help create room for additional scale out should the need arise. Also, assess the applications for possible optimization prospects.";
                }
                body["Learn more"] = @"<br/><li/><a target= ""_blank"" href=""https://docs.microsoft.com/en-us/azure/app-service/azure-web-sites-web-hosting-plans-in-depth-overview"">Azure App Service plan overview</a>
                    <li/><a target =""_blank"" href = ""https://docs.microsoft.com/en-us/azure/monitoring-and-diagnostics/insights-how-to-scale"">Scale instance count manually or automatically</a>";


                warningResponse.AddInsight(InsightStatus.Warning, "App Service Plan can scale out by less than 20% for this pricing tier", body);
                warning++;
            }
            else
            {
                var body = new Dictionary<string,string>();
                body["Insight"] = "App Service Plan hosting this webapp has room to scale out by more than additional 20% before it reaches its limit of maximum allowed worker instances for the current pricing tier. This is enough room for most applications to scale out enough and accomodate any unexpected spike in activity.";
                body["Learn more"] = @"<br/><li/><a target= ""_blank"" href=""https://docs.microsoft.com/en-us/azure/app-service/azure-web-sites-web-hosting-plans-in-depth-overview"">Azure App Service plan overview</a>
                    <li/><a target =""_blank"" href = ""https://docs.microsoft.com/en-us/azure/monitoring-and-diagnostics/insights-how-to-scale"">Scale instance count manually or automatically</a>";

                successResponse.AddInsight(InsightStatus.Success, "App Service plan can scale out an additional 20%", body);
                success++;
            }

        }
    }
        

    #endregion

    #region Check for 500.121 response codes
    var Data_500_121 = await dp.Kusto.ExecuteQuery(Get500_121CountForPastDayQuery(cxt), cxt.Resource.Stamp.Name);
    if(Data_500_121.Rows.Count > 0)
    {
        var body = new Dictionary<string,string>();
        body["Scenario"] = "Requests taking longer than 240 seconds are responded back with 500 in order to conserve resources. These requests however are allowed to execute on the worker to avoid data inconsistencies within the application.";
        body["Solution"] = "Please use Application Insights or review the application logs to investigate why these requests are taking a long time.";
        

        warningResponse.AddInsight(InsightStatus.Warning, "Requests exceeding timeout threshold detected in last 1 hour", body);
        //Think again about Drawing bar / line graph as the graph rendering takes on the time series given in the cxt whereas the query here is looking only for the past one hour and that may cause descrepencies
        warningResponse.Dataset.Add(new DiagnosticData()
        {
            Table = Data_500_121,
            RenderingProperties = new TimeSeriesRendering()
            {
                Title = "Requests exceeding timeout in the past 1 hour", 
                Description = "Requests taking longer than 240 seconds to complete are returned back as 500. The requests however are allowed to execute on the worker to avoid data inconsistencies within the application. Please review the application logs to investigate why these requests are taking a long time",
                GraphType = TimeSeriesType.LineGraph
            }
        });
        warning++;

    }
    else
    {
        successResponse.AddInsight(InsightStatus.Success,"No Requests exceeding timeout threshold detected in last 1 hour");
        success++;
    }
    
    #endregion


    #region Check for Swap With Preview
    //site_auth_enabled is true if the site has AAD Auth enabled via portal
    //Look at AdminSubscriptionAuditEvents in CentralUS with timestamp filter picked from observer property slot_swap_status
    //SiteName in the filter will be sitename(sourceslotname) {Don't include production if that's the source slot name}
    //Not sure how to check for AutoSwap being enabled though... Will have to look into that
    //If AutoSwap is not enabled and site Auth is not enabled then recommend to use Swap with Preview option if not already using it. If using it, then acknowledge

    #endregion

    
    #region Summary

    DataSummary successSummary = new DataSummary("Success", success.ToString(), "#007300");
    DataSummary warningSummary = new DataSummary("Warning", warning.ToString(), "#ff9104");
    DataSummary criticalSummary = new DataSummary("Critical", critical.ToString(), "red");

    res.AddDataSummary(new List<DataSummary>() { criticalSummary, warningSummary, successSummary }); 

    #endregion

    res.Dataset.AddRange(criticalResponse.Dataset);
    res.Dataset.AddRange(warningResponse.Dataset);
    res.Dataset.AddRange(successResponse.Dataset);
    
 //}
//  catch(Exception ex)
//  {
//      var body = new Dictionary<string,string>();
//      body["StackTrace"] = ex.StackTrace;
//      res.AddInsight(InsightStatus.Critical, ex.Message, body);
//  }

    return res;
}


private static int GetMaxAllowedWorkersForSku(string currentAppSKU)
{
    int maxAllowedWorkersForSku = -1;
    switch(currentAppSKU)
    {
        case "Basic" : 
            maxAllowedWorkersForSku = 3;
            break;
        case "Standard":
            maxAllowedWorkersForSku = 10;
            break;
        case "Premium" :
            maxAllowedWorkersForSku = 20;
            break;
        default : 
            //This will be true for ASE where sku = Isolated
            //TODO Need to figure out what value should be set here.            
            maxAllowedWorkersForSku = -1;
            break;
    }

    if (maxAllowedWorkersForSku<0)
    {
        if(currentAppSKU == "Free")
        {
            //NOOP. Currently assuming that the detector can be fired even for a FREE webapp. This check is really invalid for a Free tier webapp
        }
        else
        {
            //res.AddInsight(InsightStatus.Critical, "Unrecognized SKU detected. Please report this error");
            if(currentAppSKU == "Isolated")
            {
                   //This is an ASE.. Need to figure out how to perform a check for ASE to see if the ASP can scale more or not on an ASE
                //TODO here.. Currently NOOP
            }
        }
    }
    return maxAllowedWorkersForSku;
}



private static DataTable GetAutoScaleProfileTable(DataTable autoScaleProfiles, Response criticalResponse, Response warningResponse, Response successResponse, dynamic siteData)
{
    //HERE CHECK TO SEE IF THE WEBAPP HAS AUTO SCALE RULE ENABLED. IF IT DOES, THEN CHECK THE MAXIMUM SET AND MAKE SURE THAT THE SITE IS NOT ALREADY RUNNING AT THE MAX SET BY AUTOSCALE. ALSO CHECK IF AUTOSCALE MAX IS MORE THAN THE MAX INSTANCES ALLOWED BASED ON PRICING TIER ?

    
    List<AutoScaleProfile> profiles = new List<AutoScaleProfile>();

    foreach (DataRow row in autoScaleProfiles.Rows)
    {               
        var autoScaleProfileObject = JsonConvert.DeserializeObject<AutoScaleProfile>(row["Profile"].ToString());
        profiles.Add(autoScaleProfileObject);
    }

    DataTable profilesTable = new DataTable();
    profilesTable.Columns.Add("Settings");
    profilesTable.Columns.Add("Value");

    int ruleCount = 1;
    var metricRules = new List<MetricRule>();
    int maxAllowedWorkersForSku = -1;
    int currInstances = siteData.web_workers.Count;
    int currAutoScaleProfileMax = 0;
    foreach(var profile in profiles)
    {
        profilesTable.Rows.Add($"<b>Profile #{ruleCount}</b>", profile.Name);
        profilesTable.Rows.Add("Capacity", $"Min={profile.Capacity.Minimum}, Max={profile.Capacity.Maximum} , Default={profile.Capacity.Default}");

        //Check to see if the max configured by current AutoScale profile can be accomodated within the current pricing tier
        maxAllowedWorkersForSku = GetMaxAllowedWorkersForSku((string)siteData.sku);
        currAutoScaleProfileMax = Convert.ToInt32(profile.Capacity.Maximum);
        if(maxAllowedWorkersForSku>0)
        {
            if(currAutoScaleProfileMax>maxAllowedWorkersForSku)
            {
                //Autoscale max cannot fit into the current pricing tier.
                var body = new Dictionary<string,string>();
                body["Insight"] = $"Autoscale profile {profile.Name} is configured to grow out to a maximum of <strong>{profile.Capacity.Maximum}</strong> instances whereas the current pricing tier <strong>{siteData.sku}</strong> supports <strong>{maxAllowedWorkersForSku}</strong> instances. This might happen if you scaled down to a lower pricing tier after configuring the autoscale settings. Any attempt to scale beyond {maxAllowedWorkersForSku} instances will cause the autoscale operation to fail. Please reconfigure the autoscale settings or scale up to a higher pricing tier that can accomodate the desired number of instances.";
                criticalResponse.AddInsight(InsightStatus.Critical, $"AutoScale Profile : {profile.Name} has it's Max incorrectly configured", body);
                critical++;
            }

            if(!(currInstances<currAutoScaleProfileMax))
            {
                //The site is currently running at the maximum configured by autoscale or is running more than what is configured by autoscale. In either case, the autoscale settings needs to be re-evaluated
                var body = new Dictionary<string,string>();
                body["Insight"] = $"Autoscale profile {profile.Name} is configured to grow out to a maximum of <strong>{profile.Capacity.Maximum}</strong> instances whereas the application is currently running on <strong>{currInstances}</strong> instances. Please reconfigure the autoscale settings.";
                criticalResponse.AddInsight(InsightStatus.Critical, $"AutoScale Profile : {profile.Name} will fail to scale up the application further", body);
                critical++;
            }

        }


        foreach (var rule in profile.Rules)
        {
            string symbol = (rule.MetricTrigger.Operator == "GreaterThan") ? ">" : "<";
            profilesTable.Rows.Add($"{rule.ScaleAction.Direction} Instances by {rule.ScaleAction.Value}", $"When {rule.MetricTrigger.Name} {symbol} {rule.MetricTrigger.Threshold.ToString("0.#########")} with TimeGrain = {rule.MetricTrigger.TimeGrain.Substring(2)} in TimeWindow = {rule.MetricTrigger.TimeWindow.Substring(2)} and Cooldown of {rule.ScaleAction.Cooldown.Substring(2)}");
            if (rule.MetricTrigger.Name == "CpuPercentage" || rule.MetricTrigger.Name == "MemoryPercentage")
            {
                var m = metricRules.Where(x=>x.Name == rule.MetricTrigger.Name).FirstOrDefault();
                if (m == null)
                {   
                    m = new MetricRule();                                     
                    metricRules.Add(m);
                }
                
                m.Name = rule.MetricTrigger.Name;
                if (rule.MetricTrigger.Operator != "GreaterThan")
                {
                    if (m.Min ==0)
                    {
                        m.Min = (int)rule.MetricTrigger.Threshold;
                    }
                    else
                    {
                        m.Min = Math.Min(m.Min, (int)rule.MetricTrigger.Threshold);
                    }
                    
                }
                else
                {
                    if (m.Max ==0)
                    {
                        m.Max = (int)rule.MetricTrigger.Threshold;
                    }
                    else
                    {
                        m.Max = Math.Max((int)rule.MetricTrigger.Threshold, m.Max);
                    }
                    
                }
            } //if (rule.MetricTrigger.Name == "CpuPercentage" || rule.MetricTrigger.Name == "MemoryPercentage")
        } //foreach (var rule in profile.Rules)

        if (profile.Recurrence !=null)
        {
            profilesTable.Rows.Add("RecurringFrequency",profile.Recurrence.Frequency);

            if (profile.Recurrence.Schedule !=null)
            {
                 profilesTable.Rows.Add("TimeZone",profile.Recurrence.Schedule.TimeZone);
                 profilesTable.Rows.Add("Days",string.Join(",",profile.Recurrence.Schedule.Days));
                 profilesTable.Rows.Add("Hours",string.Join(",",profile.Recurrence.Schedule.Hours));
                 profilesTable.Rows.Add("Minutes",string.Join(",",profile.Recurrence.Schedule.Minutes));

            }
        }

        ruleCount++;

    } //foreach(var profile in profiles)

    var insightDetails = new Dictionary<string, string>();
    foreach(var r in metricRules)
    {
        if ((r.Max - r.Min) < 30 && r.Max !=0 && r.Min !=0)
        {            
            insightDetails.Add(r.Name, $"Min = {r.Min} and Max = {r.Max}");                                 
        }
    }

    if (insightDetails.Count > 0)
    {
        insightDetails.Add("Insight", "We recommend choosing an adequate margin between the scale-in and scale-out thresholds (a difference of at least more than 30) to avoid excessive scale operations, where scale-in and scale-out actions occur so frequenctly that the App Service Plan is always in a scaling state either going back or forth.");
        insightDetails.Add("Learn more","<br/><a href='https://docs.microsoft.com/en-us/azure/monitoring-and-diagnostics/insights-autoscale-best-practices' target='_blank'>AutoScaling best practices</a>");
        warningResponse.AddInsights(new List<Insight>(){
            new Insight(InsightStatus.Warning, $"AutoScale rule configuration may cause excessive scale operations", insightDetails),
        });

        warningResponse.Dataset.Add(new DiagnosticData()
                {
                    Table = profilesTable,
                    RenderingProperties = new Rendering(RenderingType.Table){Title = "AutoScale Profiles", Description = "This shows the AutoScale profiles configured for the App Service plan."}
                });
        warning++;
    }
    else
    {

        if(profilesTable.Rows.Count <= 0)
            {
                // This app doesnt have auto-scale configured.
                var insightDetail = new Dictionary<string, string>();
                insightDetail.Add("Insight", "For production apps, its recommended to configure auto scaling. This ensures that the app is ready for burst loads as well as save costs when demand is low (eg: nights and weekends). If this is not a production app, you can safely ignore this warning.");
                warningResponse.AddInsight(InsightStatus.Warning, "Auto-Scale profile not configured", insightDetail);
                warning++;
            }
        else
        {
            successResponse.AddInsight(InsightStatus.Success, $"Autoscale configured for App Service Plan : {siteData.server_farm.server_farm_name}");
            successResponse.Dataset.Add(new DiagnosticData()
                {
                    Table = profilesTable,
                    RenderingProperties = new Rendering(RenderingType.Table){Title = "AutoScale Profiles", Description = "AutoScale profiles configured for the App Service plan."}
                });
            success++;
        }
    }

    return profilesTable;

}

public class AutoScaleProfile
    {
        public string Name { get; set; }
        public Capacity Capacity { get; set; }
        public Rule[] Rules { get; set; }
        public Recurrence Recurrence { get; set; }
    }

public class Recurrence
    {
        public string Frequency { get; set; }
        public Schedule Schedule { get; set; }
    }

    public class Schedule
    {
        public string TimeZone { get; set; }
        public string[] Days { get; set; }
        public int[] Hours { get; set; }
        public int[] Minutes { get; set; }
    }

    public class Capacity
    {
        public string Minimum { get; set; }
        public string Maximum { get; set; }
        public string Default { get; set; }
    }

    public class Rule
    {
        public Metrictrigger MetricTrigger { get; set; }
        public Scaleaction ScaleAction { get; set; }
    }

    public class Metrictrigger
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
        public string Resource { get; set; }
        public string ResourceLocation { get; set; }
        public string TimeGrain { get; set; }
        public string Statistic { get; set; }
        public string TimeWindow { get; set; }
        public string TimeAggregation { get; set; }
        public string Operator { get; set; }
        public float Threshold { get; set; }
        public string Source { get; set; }
        public string MetricType { get; set; }
    }

    public class Scaleaction
    {
        public string Direction { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public string Cooldown { get; set; }
    }

    class MetricRule
    {
        public string Name;
        public int Min;
        public int Max;
    }