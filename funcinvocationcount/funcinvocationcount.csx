private static string GetCountQuery(OperationContext<App> cxt)
{

    string siteName = cxt.Resource.Name;
    return
    $@"
        let start = todatetime('{cxt.StartTime}');
        let end = todatetime('{cxt.EndTime}');
        let functionAppName = '{siteName}';
        let aggregation = 5m;
    FunctionsLogs 
        | where TIMESTAMP >= start and TIMESTAMP <= end 
        | where AppName =~ functionAppName
        | where Summary contains 'completed'
        | parse Summary with * 'Function completed (' Status ', Id=' Id ', Duration=' Duration:long 'ms)' *
        | summarize Success = countif(Status == 'Success'), Failure = countif(Status == 'Failure'), Total = count() by FunctionName, bin(PreciseTimeStamp, aggregation)";
}

private static string GetFunctionsQuery(OperationContext<App> cxt)
{

    string siteName = cxt.Resource.Name;
    return
    $@"
        let start = todatetime('{cxt.StartTime}');
        let end = todatetime('{cxt.EndTime}');
        let functionAppName = '{siteName}';
    FunctionsLogs 
        | where TIMESTAMP >= start and TIMESTAMP <= end 
        | where AppName =~ functionAppName
        | where Summary contains 'completed'
        | parse Summary with * 'Function completed (' Status ', Id=' Id ', Duration=' Duration:long 'ms)' *
        | summarize Success = countif(Status == 'Success'), Failure = countif(Status == 'Failure'), Total = count(), SuccessRate = round (100 * toreal (countif(Status == 'Success'))/count(),1) by FunctionName";
}

private static string GetExceptionsQuery(OperationContext<App> cxt)
{

    string siteName = cxt.Resource.Name;
    return
    $@"
        let start = todatetime('{cxt.StartTime}');
        let end = todatetime('{cxt.EndTime}');
        let functionAppName = '{siteName}';
    FunctionsLogs 
       |where TIMESTAMP >= start and TIMESTAMP <= end 
        |where AppName =~ functionAppName 
        |where Summary contains 'Exception' 
        |summarize Count = count() by FunctionName, Summary";
}

private static string GetPerfQuery(OperationContext<App> cxt, string functionName)
{

    string siteName = cxt.Resource.Name;
    return
    $@"
        let start = todatetime('{cxt.StartTime}');
        let end = todatetime('{cxt.EndTime}');
        let functionAppName = '{siteName}';
        let functionName =  '{functionName}';
        let aggregation = 5m;
    FunctionsLogs 
        | where TIMESTAMP >= start and TIMESTAMP <= end 
        | where AppName =~ functionAppName
        | where FunctionName == functionName
        | where Summary contains 'completed'
        | parse Summary with * 'Function completed (' Status ', Id=' Id ', Duration=' Duration:long 'ms)' *
        | summarize avg(Duration), percentiles(Duration, 50, 90, 95)  by FunctionName,  bin(PreciseTimeStamp, aggregation)";
}

private static string GetSlowOutlierQuery(OperationContext<App> cxt, string functionName, long count)
{

    string siteName = cxt.Resource.Name;
    return
    $@"
        let start = todatetime('{cxt.StartTime}');
        let end = todatetime('{cxt.EndTime}');
        let functionAppName = '{siteName}';
        let functionName =  '{functionName}';
        let aggregation = 5m;
        let c = {count};
    FunctionsLogs 
        | where TIMESTAMP >= start and TIMESTAMP <= end 
        | where AppName =~ functionAppName
        | where FunctionName == functionName
        | where Summary contains 'completed'
        | parse Summary with * 'Function completed (' Status ', Id=' Id ', Duration=' Duration:long 'ms)' *
        | project ActivityId, PreciseTimeStamp , Duration, Status
        | top c by Duration desc nulls last
        | order by PreciseTimeStamp asc nulls last";
}


[AppFilter(AppType = AppType.FunctionApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "funcInvocationCount", Name = "Function Invocation Count", Description = "")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{

    Response debugResponse = new Response();
    var bodyDebug = new Dictionary<string,string>();
    bodyDebug["Functions Query:"] = GetFunctionsQuery(cxt);
    
    var functionsTable = await dp.Kusto.ExecuteQuery(GetFunctionsQuery(cxt), cxt.Resource.Stamp.Name);
    if(functionsTable == null || functionsTable.Rows == null || functionsTable.Rows.Count == 0){
        res.AddInsight(InsightStatus.Warning, "No Function invocations during this time");
    } else {

        var invocationCountSummary = GetSummary(functionsTable);
        int failure = int.Parse(invocationCountSummary[2].Value);
        res.AddDataSummary(invocationCountSummary);

        if (failure > 0) {
                var body = new Dictionary<string,string>();
                body["Explanation"] = @"Function Failures were detected. Review the Total Function Invocations table
                and Function Invocations chart to locate failed execuctions." ;

            res.AddInsight(InsightStatus.Critical, "Execution Failures", body);
        } else {
            res.AddInsight(InsightStatus.Success, "No Execution Failures during this time");
        }

        var exceptionsTable = await dp.Kusto.ExecuteQuery(GetExceptionsQuery(cxt), cxt.Resource.Stamp.Name);
        bool exceptionsDetected = false;
        if(exceptionsTable == null || exceptionsTable.Rows == null || exceptionsTable.Rows.Count == 0) {
            res.AddInsight(InsightStatus.Success, "No Function Exceptions during this time");
        } else {
            exceptionsDetected = true;
            var body = new Dictionary<string,string>();
            body["Explanation"] = @"Runtime exceptions were detected. Review the Exceptions table below." ;
            res.AddInsight(InsightStatus.Critical, "Function Exceptions", body);
        }

        res.Dataset.Add(new DiagnosticData()
        {
            Table = functionsTable,
            RenderingProperties = new TableRendering() {
                Title = "Total Function Invocations"  
            }
        });

        var invocationsTable = await dp.Kusto.ExecuteQuery(GetCountQuery(cxt), cxt.Resource.Stamp.Name);
        bodyDebug["Rows:"] = invocationsTable.Rows.Count.ToString();



        res.Dataset.Add(new DiagnosticData()
        {
            Table = invocationsTable,
            RenderingProperties = new TimeSeriesRendering() {
                Title = "Function Invocations",
                GraphType = TimeSeriesType.LineGraph,
                GraphOptions = new {
                    yAxis = new {
                        axisLabel = "Invocation Count"
                    }
                }
            }
        });

        foreach (DataRow dr in functionsTable.Rows) {
        string fname = dr["FunctionName"].ToString();
            var perfTable = await dp.Kusto.ExecuteQuery(GetPerfQuery(cxt, fname), cxt.Resource.Stamp.Name);

            res.Dataset.Add(new DiagnosticData()
            {
                Table = perfTable,
                RenderingProperties = new TimeSeriesRendering() {
                    Title = "Function Performance for " + fname + " (ms)",
                    GraphType = TimeSeriesType.LineGraph, 
                    GraphOptions = new {
                        yAxis = new {
                            axisLabel = "Duration (ms)"
                        }
                    }
                }
            });  

            long countOutliers = 0;
            try {
                foreach (DataRow drp in perfTable.Rows) {    
                    double delta = (double) drp["avg_Duration"] - (long) drp["percentile_Duration_95"];
                    if (delta > 0) {
                        countOutliers++;
                    }
                }
            }
            catch (Exception e) {
                bodyDebug["Exception:"] = e.ToString();
            }

            if (countOutliers > 0) {
                var body = new Dictionary<string,string>();
                body["Explanation"] = @"When the 95% percentile function invocation duration is lower than the average
                in each 5 minute interval,
                it can indicate unusually long running executions that are skewing the results.
                Narrow down the time range and review the top 10 slowest executions." ;
                res.AddInsight(InsightStatus.Warning, countOutliers + " instances of unusually long executions found.", body);
            }

            long topCount = 10;
            var topSlowTable = await dp.Kusto.ExecuteQuery(GetSlowOutlierQuery(cxt, fname, topCount), cxt.Resource.Stamp.Name);

            res.Dataset.Add(new DiagnosticData()
            {
                Table = topSlowTable,
                RenderingProperties = new TableRendering() {
                    Title = "Top " + topCount + " Slow Function Invocations (ms)"  
                }
            });
       
        }

                
        if (exceptionsDetected)             
        {
            res.Dataset.Add(new DiagnosticData()
            {
                Table = exceptionsTable,
                RenderingProperties = new TableRendering() {
                    Title = "Exceptions"  
                }
            });
        }


    }
   
    bodyDebug["Tables:"] = res.Dataset.ToString();
    bodyDebug["Tables count:"] = res.Dataset.Count.ToString();

    debugResponse.AddInsight(InsightStatus.Success, "Debug statement", bodyDebug);
    DataSummary debugSummary = new DataSummary("Debug", debugResponse.ToString(), "black");
    //res.Dataset.AddRange(debugResponse.Dataset);


    return res;
}

private static List<DataSummary> GetSummary(DataTable dt)
{
    List<DataSummary> ds = new List<DataSummary>();
    int failure = 0;
    int success = 0;

    foreach(DataRow dr in dt.Rows) {
        failure += int.Parse(dr["Failure"].ToString());
        success += int.Parse(dr["Success"].ToString());
    }
    int total = failure + success;
     ds.Add(new DataSummary("Total", ""+total));
     ds.Add(new DataSummary("Success", ""+success, "green"));
     ds.Add(new DataSummary("Failure", ""+failure, "red"));

    return ds;
}