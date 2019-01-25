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

[AppFilter(AppType = AppType.FunctionApp, PlatformType = PlatformType.Windows, StackType = StackType.All, InternalOnly = true)]
[Definition(Id = "functionsCPUMemory", Name = "Function CPU/Memory", Description = "")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    var dataTable = await dp.Kusto.ExecuteQuery(GetCPU(cxt), cxt.Resource.Stamp.Name);
    if(dataTable == null || dataTable.Rows == null || dataTable.Rows.Count == 0){
        res.AddInsight(InsightStatus.Success, "No Function workers during this time");
    }
    else{
        res.Dataset.Add(new DiagnosticData()
        {
            Table = dataTable,
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


    return res;
}