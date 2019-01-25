[AppFilter(AppType = AppType.All, PlatformType = PlatformType.Windows | PlatformType.Linux, StackType = StackType.All)]
[Definition(Id = "DiffStatusFEW", Name = "Difference in Status", Author = "pepopesc", Description = "Detector for finding differences between the HTTP status returned by the Worker and the FrontEnd role. This will usually indicate that either the worker was not reachable or that the 230s limit was hit.")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    var summary = await dp.Kusto.ExecuteQuery(GetSummaryQuery(cxt), cxt.Resource.Stamp.Name);
    if (summary.Rows[0].ItemArray[0].ToString() == "0")
    {
        res.AddInsight(new Insight(InsightStatus.Success, "No differences in status were found", null));
    }
    else {
        res.AddInsight(new Insight(InsightStatus.Critical, "Differences were found. This might indicate that the worker or worker process were unreachable", null));
        res.Dataset.Add(new DiagnosticData()
            {
                Table = await dp.Kusto.ExecuteQuery(GetQuery(cxt), cxt.Resource.Stamp.Name),
                RenderingProperties = new TimeSeriesRendering(){
                        Title = "Difference in status", 
                        GraphType = TimeSeriesType.BarGraph
                    }
            });
    }
    return res;
}

private static string GetQuery(OperationContext<App> cxt)
{
    return
    $@"AntaresIISLogFrontEndTable
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | where S_sitename == '{cxt.Resource.Name}'
        | summarize Count = sum(toreal(Sc_status) - toreal(WorkerHttpStatus)) by bin(PreciseTimeStamp, 5m)";
}

private static string GetSummaryQuery(OperationContext<App> cxt)
{
    return
    $@"AntaresIISLogFrontEndTable
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | where S_sitename == '{cxt.Resource.Name}'
        | summarize Diff = sum(toreal(Sc_status) - toreal(WorkerHttpStatus))";
}