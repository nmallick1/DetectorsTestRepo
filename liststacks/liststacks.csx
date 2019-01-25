private static string GetQuery(OperationContext<App> cxt)
{
    return
    $@"WorkerRequestTelemetry
        | where TIMESTAMP >= datetime({cxt.StartTime}) and TIMESTAMP <= datetime({cxt.EndTime})
        | where SiteName == ""{cxt.Resource.Name}""
        | summarize by Handler
        | order by Handler asc";
}

[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "ListStacks", Name = "List App Stacks", Author = "wadeh", Description = "")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    try
    {
        res.Dataset.Add(new DiagnosticData()
        {
            Table = await dp.Kusto.ExecuteQuery(GetQuery(cxt), cxt.Resource.Stamp.Name),
            RenderingProperties = new Rendering(RenderingType.Table)
            {
                Title = "App Stacks", 
                Description = "The following handlers are in use on this site"
            }
        });
    } catch(Exception e)
    {
        res.AddEmail(e.Message);
    }

    return res;
}