private static string GetQuery(OperationContext<App> cxt)
{
    return
    $@"MSDeploy 
    | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
    | where Error != """"
    | project EventDate, SiteName, ErrorCode, Error, RoleInstance, SourceMoniker, Tenant
    | order by SiteName, EventDate asc";
}

[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "deploymentFailures", Name = "Deployment Failures", Description = "Analyzes deployment and publish logs to uncover failure reasons")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    //Generate Insights
    var insights = new List<Insight>();

    var failureDetailsTable = await dp.Kusto.ExecuteQuery(GetQuery(cxt), cxt.Resource.Stamp.Name);
    foreach (DataRow row in failureDetailsTable.Rows)
    {
        var errorCode = row["ErrorCode"].ToString();
        var errorDetail = row["Error"].ToString();
        if(errorCode.Contains("Generic") && errorDetail.Contains("The directory is not empty"))
        {
            
            var insightDetails = new Dictionary<string, string>();
            insightDetails.Add("ErrorCode", "Generic: The directory is not empty");
            insightDetails.Add("Next Steps", "Add the following MSDeploy switches to customer's MSDeploy command");
            insightDetails.Add("MSDeploy switches", "-skip:Directory='.*\\App_Data\\jobs\\continuous\\ApplicationInsightsProfiler.*' -skip:skipAction=Delete,objectname='dirPath',absolutepath='.*\\App_Data\\jobs\\continuous$' -skip:skipAction=Delete,objectname='dirPath',absolutepath='.*\\App_Data\\jobs$'  -skip:skipAction=Delete,objectname='dirPath',absolutepath='.*\\App_Data$'");
            //insightDetails.Add("Detailed Error", errorDetail);
            var insight = new Insight(InsightStatus.Critical, "Deployment failed as there's a web job defined, and the app's source MSDeploy package doesn't know about it" , insightDetails);
            insights.Add(insight);
        }

    }
    res.AddInsights(insights);
/*
    res.Dataset.Add(new DiagnosticData()
    {
        Table = failureDetailsTable,
        RenderingProperties = new Rendering(RenderingType.Table){Title = "Sample Table", Description = "Some description here"}
    });

    */

    return res;
}