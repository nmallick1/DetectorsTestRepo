using System.Linq;

private static string GetQuery(OperationContext<App> cxt)
{
    string siteName = cxt.Resource.Name;
    return $@"
        //Worker count for Function App
        let start = todatetime('{cxt.StartTime}');
        let end = todatetime('{cxt.EndTime}');
        let functionAppName = '{siteName}';
        let aggregation = 5m;
        ScaleControllerEvents
        | where PreciseTimeStamp > start and PreciseTimeStamp < end
        | where SiteName  =~ functionAppName 
        | where TaskName == 'SiteInformation'
        | where Message startswith 'Pinging site'
        | parse Message with 'Pinging site on ' WorkerCount:long * 
        | summarize AllocatedRoleInstanceCount = max(WorkerCount) by SourceMoniker, bin(PreciseTimeStamp, aggregation)
        | union ( range PreciseTimeStamp from bin(start, aggregation) to end step aggregation | extend AllocatedRoleInstanceCount = 0)
        | summarize AllocatedRoleInstanceCount = sum(AllocatedRoleInstanceCount) by bin(PreciseTimeStamp, aggregation) 
        //|render timechart
    ";
}

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

private static string GetInvocations(OperationContext<App> cxt)
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

[AppFilter(AppType = AppType.FunctionApp, PlatformType = PlatformType.Windows | PlatformType.Linux, StackType = StackType.All, InternalOnly = false)]
[Definition(Id = "functionscale", Name = "Function Scaling Issues", Description = "Investigate issues with Functions not scaling correctly.")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
try
    {
    var body = new Dictionary<string,string>();
    body["Notable Patterns"] = "To identify a scale problem, look for time periods with high CPU or memory usage, while at the same time, the Function workers are not increasing.";
    body["Solution"] =  @"<a target =""_blank"" href = ""https://docs.microsoft.com/azure/azure-functions/functions-best-practices#scalability-best-practices"">Read more about designing for scale in the docs</a>";
    res.AddInsight(InsightStatus.Info, "How to Identify Functions Scaling Issues", body);

    var invocationTable = await dp.Kusto.ExecuteQuery(GetInvocations(cxt), cxt.Resource.Stamp.Name);
    if(invocationTable == null || invocationTable.Rows == null || invocationTable.Rows.Count == 0){
        res.AddInsight(InsightStatus.Warning, "No Function invocations during this time");
    }
    else{
        res.Dataset.Add(new DiagnosticData()
        {
            Table = invocationTable,
            RenderingProperties = new TimeSeriesRendering() {
                Title = "Function Invocations",
                GraphType = TimeSeriesType.LineGraph
            }
        });
    }

    var dataTable = await dp.Kusto.ExecuteQuery(GetQuery(cxt), cxt.Resource.Stamp.Name);
    if(dataTable == null || dataTable.Rows == null || dataTable.Rows.Count == 0){
        res.AddInsight(InsightStatus.Warning, "No Function workers during this time");
    }
    else{
        res.Dataset.Add(new DiagnosticData()
        {
            Table = dataTable,
            RenderingProperties = new TimeSeriesRendering(){
                Title = "Function Workers Allocated", 
                GraphType = TimeSeriesType.LineGraph}
            //RenderingProperties = new Rendering(RenderingType.Table){Title = "Current Functions in Error"}
         });
        res.Dataset.Add(new DiagnosticData()
        {
            Table = await dp.Kusto.ExecuteQuery(GetCPU(cxt), cxt.Resource.Stamp.Name),
            RenderingProperties = new TimeSeriesRendering(){
                Title = "Function Worker Average CPU Percentage", 
                GraphType = TimeSeriesType.LineGraph}
         });
        res.Dataset.Add(new DiagnosticData()
        {
            Table = await dp.Kusto.ExecuteQuery(GetMemory(cxt), cxt.Resource.Stamp.Name),
            RenderingProperties = new TimeSeriesRendering(){
                Title = "Function Worker Average Memory Usage (MB)", 
                GraphType = TimeSeriesType.LineGraph}
         });
    }
    }
        catch(Exception ex){
        res.AddInsight(new Insight(InsightStatus.Critical, ex.ToString(), null));
    }

    return res;
}