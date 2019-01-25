using System.Linq;

private static string GetQuery(OperationContext<App> cxt)
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

private static string GetErrorText(OperationContext<App> cxt)
{
    string siteName = cxt.Resource.Name;
    return
    $@"
        let functionapp = '{siteName}';
        FunctionsLogs
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}
        | where AppName =~ functionapp
        | where InnerExceptionType == 'System.OutOfMemoryException' and Source == 'WebJobs.Host'
        | parse Details with * '---> (Inner Exception #0)' Exception '<---' *
        | order by TIMESTAMP desc
        | project TIMESTAMP, FunctionInvocationId, FunctionName, Exception = iif(Exception != '', Exception, Details)
    ";
}

[AppFilter(AppType = AppType.FunctionApp, PlatformType = PlatformType.Windows | PlatformType.Linux, StackType = StackType.All, InternalOnly = false)]
[Definition(Id = "functionsoutofmemory", Name = "Analyze Out of Memory Errors", Description = "Investigate more details about out of memory errors for Functions apps.")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    var dataTable = await dp.Kusto.ExecuteQuery(GetQuery(cxt), cxt.Resource.Stamp.Name);
    if(dataTable == null || dataTable.Rows == null || dataTable.Rows.Count == 0){
        var body = new Dictionary<string,string>();
        body["Insight"] = "Out of memory exceptions occur when an individual function invocation uses more than 1.5GB of memory <a href='https://docs.microsoft.com/azure/azure-functions/functions-scale#how-the-consumption-plan-works'>Learn more in the docs.</a>";
        res.AddInsight(InsightStatus.Success, "No Functions detected out of memory.", body);
    }
    else{
        var body = new Dictionary<string,string>();
        body["Insight"] = "Out of memory exceptions occur when an individual function invocation uses more than 1.5GB of memory <a href='https://docs.microsoft.com/en-us/azure/azure-functions/functions-scale#how-the-consumption-plan-works'>Learn more in the docs.</a>";
        body["Solution"] = "Consider splitting your work into smaller batches or optimizing your code. If you cannot re-architect, you can use Functions in dedicated mode with an App Service plan, which has more memory available.";
        res.AddInsight(InsightStatus.Warning, "Out of memory exceptions have occurred", body);
        res.Dataset.Add(new DiagnosticData()
        {
            Table = dataTable,
            RenderingProperties = new TimeSeriesRendering(){
                Title = "Out of Memory Errors", 
                GraphType = TimeSeriesType.BarGraph}
         });

        res.Dataset.Add(new DiagnosticData()
        {
            Table = await dp.Kusto.ExecuteQuery(GetErrorText(cxt), cxt.Resource.Stamp.Name),
            RenderingProperties = new Rendering(RenderingType.Table){Title = "Error Text"}
         });
    }


    return res;
}