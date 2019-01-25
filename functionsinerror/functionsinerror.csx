using System.Linq;

private static string GetQuery(OperationContext<App> cxt)
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

[AppFilter(AppType = AppType.FunctionApp, PlatformType = PlatformType.Windows | PlatformType.Linux, StackType = StackType.All)]
[Definition(Id = "functionsinerror", Name = "Functions In Error", Description = "")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    var dataTable = await dp.Kusto.ExecuteQuery(GetQuery(cxt), cxt.Resource.Stamp.Name);
    if(dataTable == null || dataTable.Rows == null || dataTable.Rows.Count == 0){
        var body = new Dictionary<string,string>();
        body["Info"] = "Functions that fail to initialize will never be triggered and appear offline. If your last host startup had any such errors, they would be displayed here.";
        res.AddInsight(InsightStatus.Success, "No Functions detected in error", body);
    }
    else{
        var body = new Dictionary<string,string>();
        body["Info"] = "Functions that fail to initialize will never be triggered and appear offline.";
        body["Current Functions in error"] = dataTable.Rows[0][0].ToString();
        res.AddInsight(InsightStatus.Critical, "Functions detected in error", body);
    }


    return res;
}