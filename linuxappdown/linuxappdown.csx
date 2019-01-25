[SupportTopic(Id = "32542218", PesId = "16170")] // Availability, Performance, and Application Issues/Web app down or reporting errors [Web App (Linux)]
[SupportTopic(Id = "32542218", PesId = "16333")] // Availability, Performance, and Application Issues/Web app down or reporting errors [Web App for Containers]
[SupportTopic(Id = "32562497", PesId = "16333")] // Docker Containers [Web App for Containers]
[SupportTopic(Id = "32588775", PesId = "16333")] // Docker Containers/Docker container startup and configuration [Web App for Containers]
[SupportTopic(Id = "32606472", PesId = "16333")] // Docker Containers/Multi-containers [Web App for Containers]
[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Linux, StackType = StackType.All, InternalOnly = false)]
[Definition(Id = "LinuxAppDown", Name = "Web App Down", Author = "mikono", Description = "Linux app down detectors")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    var httpstatuscodes = new DiagnosticData()
    {
        Table = await dp.Kusto.ExecuteQuery(GetHTTPStatusCodesQuery(cxt), cxt.Resource.Stamp.Name),
        RenderingProperties = new TimeSeriesRendering(){
            Title = "Requests and Errors",
            GraphOptions = new {
                yAxis = new {
                    axisLabel = "Count"
                }
            }
        }
    };

    res.Dataset.Add(httpstatuscodes);
    res.AddDetectorCollection(new List<string>() { "LinuxContainerStartFailure", "LinuxContainerRecycle" });

    var dnsSuffix = cxt.Resource.Stamp.DnsSuffix ?? "azurewebsites.net";
    var kuduUrl = $"https://{cxt.Resource.Name}.scm.{dnsSuffix}";
    var dockerLogUrl = $"{kuduUrl}/api/logs/docker";

    res.AddMarkdownView($@"#### Advanced Investigation
1. Check the <a href=""{dockerLogUrl}"" target=""_blank"">Docker logs</a> emitted by your containers. 
2. Connect your container through <a href=""{kuduUrl}"" target=""_blank"">Kudu</a> or direct SSH <a href=""https://blogs.msdn.microsoft.com/appserviceteam/2018/05/07/remotedebugginglinux/"" target=""_blank"">Learn more</a>.
    "
    );

    return res;
}


private static string GetHTTPStatusCodesQuery(OperationContext<App> cxt)
{
    return
    $@"AntaresIISLogFrontEndTable
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | where S_sitename =~ ""{cxt.Resource.Name}"" 
        | where User_agent != ""AlwaysOn""
        | project PreciseTimeStamp, Http2xx = (Sc_status / 100 == 2), Http3xx = (Sc_status / 100 == 3), Http4xx = (Sc_status / 100 == 4), Http5xx = (Sc_status / 100 == 5)
        | summarize Http2xx = count(Http2xx), Http3xx = count(Http3xx), Http4xx = count(Http4xx), ServerErrors = count(Http5xx)  by bin(PreciseTimeStamp, 5m)
        | order by PreciseTimeStamp asc";
}