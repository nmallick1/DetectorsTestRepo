
using System.Linq;

private static string GetMemory(OperationContext<App> cxt)
{
    string siteName = cxt.Resource.Name;
    return
    $@"
        let start = todatetime('{cxt.StartTime}');
        let end = todatetime('{cxt.EndTime}');
        let functionname = '{siteName}';
        StatsDWASWorkerProcessOneMinuteTable
        | where TIMESTAMP >= start and TIMESTAMP <= end 
        | where RoundedTimeStamp == bin(RoundedTimeStamp, 5m) 
        | as SubscriptionRecordsDuringTimeSpan
        | where ((ApplicationPool =~ functionname or ApplicationPool startswith strcat(functionname,'__')))
        | project TIMESTAMP = RoundedTimeStamp, RoleInstance, Tenant
        | join
        (
            SubscriptionRecordsDuringTimeSpan 
            | where ApplicationPool !startswith 'mawscanary' and((ComputeType == 'DedicatedCompute') or ((ApplicationPool =~ functionname or ApplicationPool startswith strcat(functionname,'__'))))
            | summarize CpuPercent = round(avg(AverageKernelTimeCpuPercent + AverageUserTimeCpuPercent),1), AverageCurrentJobMemoryUsed = round(avg(AverageCurrentJobMemoryUsed)) by RoleInstance, Tenant, ApplicationPool, TIMESTAMP = bin(RoundedTimeStamp, 5m)
        )
        on TIMESTAMP, RoleInstance, Tenant
        | extend Memory = round(AverageCurrentJobMemoryUsed/exp10(6),1)
        | project TIMESTAMP, RoleInstance = strcat(substring(Tenant, 0, 4), replace('DedicatedWebWorkerRole_IN','', RoleInstance )), Memory
        | take 15
        | order by RoleInstance, TIMESTAMP asc 
    ";
}


private static string GetCPU(OperationContext<App> cxt)
{
    string siteName = cxt.Resource.Name;
    return
    $@"
        let start = todatetime('{cxt.StartTime}');
        let end = todatetime('{cxt.EndTime}');
        let functionname = '{siteName}';
        StatsDWASWorkerProcessOneMinuteTable
        | where TIMESTAMP >= start and TIMESTAMP <= end 
        | where RoundedTimeStamp == bin(RoundedTimeStamp, 5m) 
        | as SubscriptionRecordsDuringTimeSpan
        | where ((ApplicationPool =~ functionname or ApplicationPool startswith strcat(functionname,'__')))
        | project TIMESTAMP = RoundedTimeStamp, RoleInstance, Tenant
        | join
        (
            SubscriptionRecordsDuringTimeSpan 
            | where ApplicationPool !startswith 'mawscanary' and((ComputeType == 'DedicatedCompute') or ((ApplicationPool =~ functionname or ApplicationPool startswith strcat(functionname,'__'))))
            | summarize CpuPercent = round(avg(AverageKernelTimeCpuPercent + AverageUserTimeCpuPercent),1), AverageCurrentJobMemoryUsed = round(avg(AverageCurrentJobMemoryUsed)) by RoleInstance, Tenant, ApplicationPool, TIMESTAMP = bin(RoundedTimeStamp, 5m)
        )
        on TIMESTAMP, RoleInstance, Tenant
        | project TIMESTAMP, RoleInstance = strcat(substring(Tenant, 0, 4), replace('DedicatedWebWorkerRole_IN','', RoleInstance )), CpuPercent
        | take 15
        | order by RoleInstance, TIMESTAMP asc 
    ";
}

private static string GetQuery(OperationContext<App> cxt)
{

    string siteName = cxt.Resource.Name;
    return
    $@"
        let start = todatetime('{cxt.StartTime}');
        let end = todatetime('{cxt.EndTime}');
        let functionname = '{siteName}';
        let aggregation = 5m;
    FunctionsLogs 
        | where TIMESTAMP >= start and TIMESTAMP <= end 
        | where AppName =~ functionname
        | where Summary contains 'completed'
        | summarize Count = count() by FunctionName, bin(PreciseTimeStamp, aggregation)";
}

private static string GetErrors(OperationContext<App> cxt)
{
    string siteName = cxt.Resource.Name;
    return
    $@"
    let functionapp = '{siteName}';
    let laststart =
    FunctionsLogs
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}
        | where AppName =~ functionapp
    | where Summary == 'Job host started' 
    | order by TIMESTAMP desc
    | take 1
    | project Tenant;
    FunctionsLogs
    | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}
    | where AppName =~ functionapp and Tenant in (laststart)
    | where Summary contains 'functions are in error'
    | order by TIMESTAMP desc
    | take 1
    | project Current_Errors = Summary
    ";
}

private static string GetOutofMemory(OperationContext<App> cxt)
{
    string siteName = cxt.Resource.Name;
    return
    $@"
        //Out of Memory Exceptions over time per function app
        let functionapp = '{siteName}';
        FunctionsLogs
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}
        | where AppName =~ functionapp
        | where InnerExceptionType == 'System.OutOfMemoryException' and Source == 'WebJobs.Host'
        | order by TIMESTAMP desc
        | summarize count() by FunctionName, bin(PreciseTimeStamp, 5m)
    ";
}

private static string GetHostThresholds(OperationContext<App> cxt)
{
    string siteName = cxt.Resource.Name;
    return
    $@"
        //Out of Memory Exceptions over time per function app
        let functionapp = '{siteName}';
        FunctionsLogs
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}
        | where AppName =~ functionapp
        | where Level < 4 and Details contains 'Host thresholds' 
        | parse Details with * 'System.InvalidOperationException :' hostDetails  '.' *
        | summarize count() by hostDetails  
    ";
}

// start - for Function Information Section - jsanders

private static string GetDefinedFunctions(OperationContext<App> cxt)
{
    // gets functions for this app  - if the function is not hit (stopped), it is possible that this will not return anything    
    string siteName = cxt.Resource.Name;
    return
    $@"
        
        let functionapp = '{siteName}';
        FunctionsLogs
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}
        | where AppName =~ functionapp
        | where Summary contains 'Found the following functions:' 
        | parse Summary with * 'Found the following functions:'  resultOfParse
        | project PreciseTimeStamp,  resultOfParse, Summary
        | order by PreciseTimeStamp desc nulls last 
        | take 1
    ";
}


private static string GetFunctionVersion(OperationContext<App> cxt)
{
    // get current version  - if the function is not hit (stopped), it is possible that this will not return anything    
    string siteName = cxt.Resource.Name;
    return
    $@"
        
        let functionapp = '{siteName}';
        FunctionsLogs
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}
        | where AppName =~ functionapp
        | where Summary contains 'Starting Host (HostId=' 
                | parse Summary with * 'FunctionsExtensionVersion=' Version ')' *
                | project PreciseTimeStamp, Role, RoleInstance, AppName , HostVersion , Version
                                | order by PreciseTimeStamp desc nulls last
                                | take 1
    ";
}

// end - for Function Information Section - jsanders

[AppFilter(AppType = AppType.FunctionApp, PlatformType = PlatformType.Windows, StackType = StackType.All, InternalOnly = false)]
[Definition(Author = "finbarr,jsanders", Id = "FunctionsChecker", Name = "Functions Health Checkup", Description = "Checking this Function application for configuration best practices .")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    int warning = 0;
    int critical = 0;
    int success = 0;

    var siteData = (await dp.Observer.GetSite(cxt.Resource.Stamp.Name, cxt.Resource.Name))[0];
    var appServicePlan = siteData.server_farm.virtual_farm;

    Response criticalResponse = new Response();
    Response warningResponse = new Response();
    Response successResponse = new Response();

    #region Check if the site is running on a Free tier
    bool isDynamic = siteData.sku == "Dynamic";    
    if(isDynamic)
    {
        var body = new Dictionary<string,string>();

        body["Insight"] = "Good choice! Your Function app is running on a Consumption Plan. Instances of the Azure Functions host are dynamically added and removed based on the number of incoming events. On a Consumption plan, a function can run for a maximum of 10 minutes.";
        body["Learn more"] = @"<a target=""_blank"" href=""https://docs.microsoft.com/azure/azure-functions/functions-scale"">Azure Functions Hosting Plans</a>";

        successResponse.AddInsight(InsightStatus.Success, "This Function app is running on a Consumption Plan", body);

        success++;
    }else
    {  
        var body = new Dictionary<string,string>();

        body["Insight"] = @"You are currently using an App Service Plan, which does not dynamically scale based on the number of incoming events (like the Consumption Plan). However, you should consider an App Service plan in the following cases:
        <ul>
        <li>You have existing, underutilized VMs that are already running other App Service instances.</li>
        <li>You expect your function apps to run continuously, or nearly continuously. In this case, an App Service Plan can be more cost-effective.</li>
        <li>You need more CPU or memory options than what is provided on the Consumption plan.</li>
        <li>You need to run longer than the maximum execution time allowed on the Consumption plan (of 10 minutes).</li>
        <li>You require features that are only available on an App Service plan, such as support for App Service Environment, VNET/VPN connectivity, and larger VM sizes.</li>
        </ul>";
        body["Learn more"] = @"<a target=""_blank"" href=""https://docs.microsoft.com/azure/azure-functions/functions-scale"">Azure Functions Hosting Plans</a>";

        warningResponse.AddInsight(InsightStatus.Warning, "Using an App Service Plan is best for Functions that are continuously running.", body);     
        warning++;
    }

    if (isDynamic)
            {
                    
                    var thresholdsTable = await dp.Kusto.ExecuteQuery( GetHostThresholds(cxt), cxt.Resource.Stamp.Name);
                    if(thresholdsTable != null && thresholdsTable.Rows != null && thresholdsTable.Rows.Count != 0){                               
                    
                    var body = new Dictionary<string,string>();
                    body["Insight"] = @"We've detected that your Function App is exceeding some of the Host limits and should be investigated (these limits include):
                        <ul>
                        <li>The number of outbound connections.</li>
                        <li>The number of threads.</li>
                        <li>The number of child processes.</li>
                        <li>The number of named pipes.</li>
                        <li>The number of sections.</li>
                        </ul>";
                        body["Learn more"] = @"<a target=""_blank"" href=""https://github.com/Azure/azure-functions-host/wiki/Host-Health-Monitor"">Host threshold Monitoring</a>";

                        criticalResponse.AddInsight(InsightStatus.Critical, "This Function App is exceeding one or more of the Host threshold limits",body);
                        criticalResponse.Dataset.Add(new DiagnosticData()
                            {
                                Table = thresholdsTable,
                                RenderingProperties = new TableRendering() {
                                    Title = "Host Thresholds exceeded",
                                    Description="This table displays which host thresholds have been exceeded"
                                }
                            });
                        critical++;
                    }
            }


// begin Function Information section - jsanders
// var overallInfo = new Dictionary<string,string>();
// 
// var verTable = await dp.Kusto.ExecuteQuery(GetFunctionVersion(cxt), cxt.Resource.Stamp.Name);
// if(verTable != null && verTable.Rows != null && verTable.Rows.Count != 0){
//         overallInfo["Runtime version:"] = verTable.Rows[0][4].ToString() + "( " +  verTable.Rows[0][5].ToString() + " )";
//     }
// 
// var definedFunctions = await dp.Kusto.ExecuteQuery(GetDefinedFunctions(cxt), cxt.Resource.Stamp.Name);
//     if(definedFunctions != null && definedFunctions.Rows != null && definedFunctions.Rows.Count != 0){
//         var result =  definedFunctions.Rows[0][1].ToString();
//         overallInfo["Defined Functions:"] = result.Replace(".Run", ".Run <br>");
//     }
// 
// if (overallInfo.Count > 0)
// {
//    successResponse.AddInsight(InsightStatus.Success, "Function Information", overallInfo);
//    success++; 
// }
// end Function Information section - jsanders

    var dataTable = await dp.Kusto.ExecuteQuery(GetErrors(cxt), cxt.Resource.Stamp.Name);
    if(dataTable != null && dataTable.Rows != null && dataTable.Rows.Count != 0){
        var body = new Dictionary<string,string>();
        body["Info"] = "Functions that fail to initialize will never be triggered and appear offline.";
        body["Current Functions in error"] = dataTable.Rows[0][0].ToString();
        criticalResponse.AddInsight(InsightStatus.Critical, "Functions detected in error", body);
        critical++;
    }

    var outofMemoryTable = await dp.Kusto.ExecuteQuery(GetOutofMemory(cxt), cxt.Resource.Stamp.Name);
    if(outofMemoryTable != null && outofMemoryTable.Rows != null && outofMemoryTable.Rows.Count != 0){
        var body = new Dictionary<string,string>();
        body["Insight"] = "Out of memory exceptions occur when an individual function invocation uses more than 1.5GB of memory <a href='https://docs.microsoft.com/en-us/azure/azure-functions/functions-scale#how-the-consumption-plan-works'>Learn more in the docs.</a>";
        body["Solution"] = "Consider splitting your work into smaller batches or optimizing your code. If you cannot re-architect, you can use Functions in dedicated mode with an App Service plan, which has more memory available.";
        criticalResponse.AddInsight(InsightStatus.Critical, "Out of memory exceptions have occurred", body);
        criticalResponse.Dataset.Add(new DiagnosticData()
        {
            Table = outofMemoryTable,
            RenderingProperties = new TimeSeriesRendering(){
                Title = "Out of Memory Exceptions", 
                Description = "This table shows you which Functions are logging Out of memory exceptions",
                GraphType = TimeSeriesType.BarGraph}
         });
        critical++;
    }

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

    res.Dataset.Add(new DiagnosticData()
    {
        Table = await dp.Kusto.ExecuteQuery(GetQuery(cxt), cxt.Resource.Stamp.Name),
        RenderingProperties = new TimeSeriesRendering() {
            Title = "Function Invocations",
            Description = "This table shows how many times each Function was invoked.",
            GraphType = TimeSeriesType.LineGraph
        }
    });

    var cpuTable = await dp.Kusto.ExecuteQuery(GetCPU(cxt), cxt.Resource.Stamp.Name);
    if(cpuTable == null || cpuTable.Rows == null || cpuTable.Rows.Count == 0){
        res.AddInsight(InsightStatus.Success, "No Function workers during this time");
    }
    else{
        res.Dataset.Add(new DiagnosticData()
        {
            Table = cpuTable,
            RenderingProperties = new TimeSeriesRendering(){
                Title = "Function Worker Average CPU Percentage", 
                Description = "This table shows you the average CPU usage per Function host instance",
                GraphType = TimeSeriesType.LineGraph}
         });
        res.Dataset.Add(new DiagnosticData()
        {
            Table = await dp.Kusto.ExecuteQuery(GetMemory(cxt), cxt.Resource.Stamp.Name),
            RenderingProperties = new TimeSeriesRendering(){
                Title = "Function Worker Average Memory Usage (MB)", 
                Description = "This table shows you the average Memory usage per Function host instance",
                GraphType = TimeSeriesType.LineGraph}
         });

    }

    return res;
}
