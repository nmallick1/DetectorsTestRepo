private static string GetQuery(OperationContext<App> cxt)
{
    return
    $@"let requestTimeTable = WorkerRequestTelemetry
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | where SiteName =~ ""{cxt.Resource.Name}""
        | parse NotificationSummaryJSON with * ""RQ_EXECUTE_REQUEST_HANDLER\"", \""Milliseconds\"": \"""" handlerTime:decimal ""\"""" Terminator
        | summarize numHits = count(), workerTime = bin(avg(TotalPipelineTime),1), appTime = bin(avg(handlerTime),1), ioTime = bin(avg(FromClientIO+ToClientIO),1) by RoleInstance, RequestUrl, Handler
        | order by appTime desc;
        requestTimeTable | take 10";
}

[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "tenslowesturls", Name = "10 Slowest URLs", Author = "wadeh", Description = "")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    try
    {
        res.Dataset.Add(new DiagnosticData()

        {
            Table = await dp.Kusto.ExecuteQuery(GetQuery(cxt), cxt.Resource.Stamp.Name),
            RenderingProperties = new Rendering(RenderingType.Table)
            {
                Title = "10 Slowest URLs",
                Description = "Top 10 URLs in terms of time spent in application"

            }

        });

    } catch(Exception e)
    {
        res.AddEmail(e.Message);
    }

    return res;
}