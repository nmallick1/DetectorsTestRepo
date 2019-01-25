[AppFilter(AppType = AppType.All, PlatformType = PlatformType.Windows | PlatformType.Linux, StackType = StackType.All)]
[Definition(Id = "sample", Name = "Sample Detector", Author="applensv2", Description = "This is a sample detector")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    
   //1) Add Data Summary
    DataSummary ds1 = new DataSummary("Success", "40", "green");
    DataSummary ds2 = new DataSummary("Failures", "60", "red");
    res.AddDataSummary(new List<DataSummary>(){ ds1, ds2 });

    res.AddMarkdownView(@"
        Below is a collection of detectors. This detector, the parent, provides the ids of the detectors that it wants to list. 
        The detectors are run and the status of each detector is displayed, based on the most severe insight in that detector. 
    ");

    res.AddDetectorCollection(new List<string>() { "bestpractice", "swap", "Migration", "functionsinerror", "functionschecker", "functionscale", "linuxcontainerrecycle", "AzureServiceManagementAPIUsage" });

    // Add a markdown section
    // We will use it here to explain how to create an insight

    res.AddMarkdownView(@"
        ## This is the markdown rendering type
        
        You can use this to create a view that uses github markdown. 

        You can find full documentation of what is available [here](https://jfcere.github.io/ngx-markdown/ ""ngx-markdown"")

        ---

        Source:

        ```csharp
            res.AddMarkdownView(@""
            ## This is the markdown rendering type
            
            You can use this to create a view that uses github markdown. 
            
            You can find full documentation of what is available [here](https://jfcere.github.io/ngx-markdown/ ""ngx-markdown"")
            "");
        ```
    ");

    //2)  add insight, include mardown in one of them
    var insightDetails = new Dictionary<string, string>();
    insightDetails.Add("Detail A", "You can add customer ready content, like below, that only display internally.");

    // To render content as markdown, just surround the markdown text with '<markdown>...</markdown>'
    insightDetails.Add("Customer Ready Content", 
    @"<markdown>
    #### Consider Scaling Up

    High resource usage was detected for your web app. You are currently using a `Basic` tier App Service Plan. Consider scaling if your application is designed to be resource intensive.  

    App Service Plan Tier | CPU Cores | Physical Memory
    --- | --- | ---
    Basic | 1 | 1.75GB
    Standard | 2 | 3.5GB
    Premium | 4 | 7GB
    </markdown>");

     Insight success = new Insight(InsightStatus.Success, "Add message, Sample Detector");
     Insight info = new Insight(InsightStatus.Warning, "Info Insight with markdown", insightDetails);
     info.IsExpanded = true;
     res.AddInsights(new List<Insight>(){ success, info });

 
  // 3) Add Table to response, TimeSeries
    var httpstatuscodes = new DiagnosticData()
    {
        Table = await dp.Kusto.ExecuteQuery(GetHTTPStatusCodesQuery(cxt), cxt.Resource.Stamp.Name),
        RenderingProperties = new TimeSeriesRendering(){
            Title = "Sample Time Series with Specified Graph Options",
            GraphOptions = new {
                forceY = new int[] { 0, 100 }, //This means y axis will be 0 to 100.
                yAxis = new {
                    axisLabel = "This is a Y axis label"
                } 
            }
        }
    };

    res.Dataset.Add(httpstatuscodes);

    res.AddDynamicInsight(new DynamicInsight(
        InsightStatus.Critical, //Status
        "This is a graph inside an insight", // Message to be displayed on insight
        httpstatuscodes, // Inner Diagnostic Data, you can put any data and rendering type here
        true)); // whether insight is expanded by default

    // 4) Add Table to response
    res.Dataset.Add(new DiagnosticData()
    {
        Table = await dp.Kusto.ExecuteQuery(GetTableQuery(cxt), cxt.Resource.Stamp.Name),
        RenderingProperties = new Rendering(RenderingType.Table){
            Title = "Sample Table", 
            Description = "Some description here"
        }
    });


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
        | summarize count(Http2xx), count(Http3xx), count(Http4xx), count(Http5xx)  by bin(PreciseTimeStamp, 5m)
        | order by PreciseTimeStamp asc";
}

private static string GetTableQuery(OperationContext<App> cxt)
{
    return
    $@"AntaresIISLogFrontEndTable
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | where S_sitename =~ ""{cxt.Resource.Name}"" 
        | where User_agent != ""AlwaysOn""
        | project PreciseTimeStamp, Cs_host, Sc_status
        | take 10";
}