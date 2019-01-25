private static string GetWebJobCPUMem(OperationContext<App> cxt)
{
     string siteName = cxt.Resource.Name;
     string tenantId = Utilities.TenantFilterQuery(cxt.Resource);
    return
    $@"let start = todatetime('{cxt.StartTime}');
       let end = todatetime('{cxt.EndTime}');
StatsDWASWorkerProcessOneMinuteTable
            | where TIMESTAMP >= start and TIMESTAMP <= end 
            | where {tenantId} and SubscriptionId == '{cxt.Resource.SubscriptionId}'
            | where RoundedTimeStamp == bin(RoundedTimeStamp, 5m)
            | as SubscriptionRecordsDuringTimeSpan
            | where ApplicationPool contains '~1{siteName}'
            | project TIMESTAMP = RoundedTimeStamp, RoleInstance, Tenant
            | join
            (
                SubscriptionRecordsDuringTimeSpan
                | where ApplicationPool contains '~1{siteName}'
                | summarize CpuPercent = round(avg(AverageKernelTimeCpuPercent + AverageUserTimeCpuPercent),1) by RoleInstance, Tenant, ApplicationPool, TIMESTAMP = bin(RoundedTimeStamp, 5m)
            )
            on TIMESTAMP, RoleInstance, Tenant
            | project TIMESTAMP,CpuPercent, RoleInstance, ApplicationPool, Tenant 
            | order by TIMESTAMP, ApplicationPool, RoleInstance asc";
       
}
private static string GetWebJobInfo(OperationContext<App> cxt)
{
     string siteName = cxt.Resource.Name;
     string tenantId = Utilities.TenantFilterQuery(cxt.Resource);
    return
    $@"let start = todatetime('{cxt.StartTime}');
       let end = todatetime('{cxt.EndTime}');
    Kudu
       | where jobName != ''
       | where TIMESTAMP >= start and TIMESTAMP <= end
       | where {tenantId}
       | where (jobType == 'triggered' and trigger !='') or jobType == 'continuous'
       | where siteName startswith '{siteName}' or siteName =~ '{siteName}' 
       | project TIMESTAMP , jobName , jobType , trigger
       | summarize by jobName,jobType, trigger";
       
       
}

private static string GetCountInfo(OperationContext<App> cxt)
{
     string siteName = cxt.Resource.Name;
     string tenantId = Utilities.TenantFilterQuery(cxt.Resource);
    return
    $@"let start = todatetime('{cxt.StartTime}');
       let end = todatetime('{cxt.EndTime}');
    Kudu
       | where jobName != ''
       | where TIMESTAMP >= start and TIMESTAMP <= end
       | where {tenantId}
       | where siteName startswith '{siteName}' or siteName =~ '{siteName}' 
       | summarize Success = countif(Message contains 'Job initialization success'), Failure = countif(error contains 'Job failed'), Total = count() by jobName, bin(PreciseTimeStamp, aggregation)";
}

private static string GetWebjobAGInfo(OperationContext<App> cxt)
{
     string siteName = cxt.Resource.Name;
     string tenantId = Utilities.TenantFilterQuery(cxt.Resource);
    return
    $@"let start = todatetime('{cxt.StartTime}');
       let end = todatetime('{cxt.EndTime}');
    Kudu
       | where jobName != ''
       | where TIMESTAMP >= start and TIMESTAMP <= end
       | where {tenantId}
       | where siteName startswith '{siteName}' or siteName =~ '{siteName}' 
       | summarize Starts = countif(Message contains 'Job initialization success'), Failures = countif(error contains 'Job failed') by jobName";
}

private static string GetFailuresQuery(OperationContext<App> cxt)
{

    string siteName = cxt.Resource.Name;
    string tenantId = Utilities.TenantFilterQuery(cxt.Resource);
    return
    $@"
        let start = todatetime('{cxt.StartTime}');
        let end = todatetime('{cxt.EndTime}');
        let functionAppName = '{siteName}';
    Kudu 
       |where TIMESTAMP >= start and TIMESTAMP <= end 
        | where {tenantId}
        | where siteName startswith '{siteName}' or siteName =~ '{siteName}'  
        | where error contains 'Job failed' 
        |summarize Count = count() by jobName, error"; 
}
private static string GetInstances(OperationContext<App> cxt)
{
     string siteName = cxt.Resource.Name;
     string tenantId = Utilities.TenantFilterQuery(cxt.Resource);
    return
    $@"let start = todatetime('{cxt.StartTime}');
       let end = todatetime('{cxt.EndTime}');
StatsDWASWorkerProcessOneMinuteTable
            | where TIMESTAMP >= start and TIMESTAMP <= end 
            | where {tenantId} and SubscriptionId == '{cxt.Resource.SubscriptionId}'
            | where RoundedTimeStamp == bin(RoundedTimeStamp, 5m)
            | as SubscriptionRecordsDuringTimeSpan
            | where (ApplicationPool =~'{siteName}' or ApplicationPool startswith '{siteName}__')
            | summarize by RoleInstance";
       
}


[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "WebJobs Details", Name = "WebJob Details", Description = "")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    
    
    //Response debugResponse = new Response();
    //var bodyDebug = new Dictionary<string,string>();
    //bodyDebug["Functions Query:"] = GetWebJobCPUMem(cxt);

try
{
    
   var WebJobTable = await dp.Kusto.ExecuteQuery(GetWebJobInfo(cxt), cxt.Resource.Stamp.Name);
   foreach (DataRow dr in WebJobTable.Rows)
   {
       if (dr["trigger"].ToString().Contains("External") )
       {
           dr["trigger"] = "On Demand";
       }
   }
    res.Dataset.Add(new DiagnosticData()
    {
        Table = WebJobTable,
        RenderingProperties = new TableRendering() {
            Title = "WebJobs for Timeframe",
           Description = "."
        }
        
    });

   
    var JobinfoTable = await dp.Kusto.ExecuteQuery(GetWebjobAGInfo(cxt), cxt.Resource.Stamp.Name );
if(JobinfoTable == null || JobinfoTable.Rows == null || JobinfoTable.Rows.Count == 0){
        res.AddInsight(InsightStatus.Warning, "No Web Job executions during this time");
    } else {

        var invocationCountSummary = GetSummary(JobinfoTable);
        int failure = int.Parse(invocationCountSummary[2].Value);
       

        if (failure > 0) {
                var body = new Dictionary<string,string>();
                body["Explanation"] = @"Webjob Failures were detected. Review the Total WebJob executions table to locate failed execuctions." ;

        } else {
            res.AddInsight(InsightStatus.Success, "No Execution Failures during this time");
        }

        var exceptionsTable = await dp.Kusto.ExecuteQuery(GetFailuresQuery(cxt), cxt.Resource.Stamp.Name);
        bool exceptionsDetected = false;
        if(exceptionsTable == null || exceptionsTable.Rows == null || exceptionsTable.Rows.Count == 0) {
            res.AddInsight(InsightStatus.Success, "No Webjob Exceptions during this time");
        } else {
            exceptionsDetected = true;
            var body = new Dictionary<string,string>();
            body["Explanation"] = @"Errors were detected. Review the Errors table below." ;
            res.AddInsight(InsightStatus.Critical, "WebJob Errors", body);
        }

        res.Dataset.Add(new DiagnosticData()
        {
            Table = JobinfoTable,
            RenderingProperties = new TableRendering() {
                Title = "Webjob Executions"  
            }
        });
if (exceptionsDetected)             
        {
            res.Dataset.Add(new DiagnosticData()
            {
                Table = exceptionsTable,
                RenderingProperties = new TableRendering() {
                    Title = "Errors"  
                }
            });
        }
            var perfTable = await dp.Kusto.ExecuteQuery(GetWebJobCPUMem(cxt), cxt.Resource.Stamp.Name);
            res.Dataset.Add(new DiagnosticData()
            {
                Table = perfTable,
                
               RenderingProperties = new TimeSeriesRendering() {
                  Title = "CPU Analysis for " + cxt.Resource.Name + "[Kudu]" ,
                   GraphType = TimeSeriesType.LineGraph, 
                    GraphOptions = new {
                        forceY = new int[] {0, 100},
                        yAxis = new {
                            axisLabel = "CPU"
                        }
                    }
                }

            });

    
}
}
catch (Exception e) 
{
    var err = e.ToString();
  // bodyDebug["Exception:"] = e.ToString();
   
}
   // debugResponse.AddInsight(InsightStatus.Success, "Debug statement", bodyDebug);
    //DataSummary debugSummary = new DataSummary("Debug", debugResponse.ToString(), "black");
    //res.Dataset.AddRange(debugResponse.Dataset);

    return res;
}
private static List<DataSummary> GetSummary(DataTable dt)
{
   List<DataSummary> ds = new List<DataSummary>();
   int failure = 0;
    int success = 0;

    foreach(DataRow dr in dt.Rows) {
        failure += int.Parse(dr["Failures"].ToString());
        success += int.Parse(dr["Starts"].ToString());
    }
    int total = failure + success;
    ds.Add(new DataSummary("Total", ""+total));
    ds.Add(new DataSummary("Starts", ""+success, "green"));
    ds.Add(new DataSummary("Failures", ""+failure, "red"));

    return ds;

}