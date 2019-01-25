private static string GetSuccessfulFTPLogins(OperationContext<App> cxt)
{
    return
    $@"AntaresPublisherEvents 
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}
        | order by TIMESTAMP asc 
        | where SourceMoniker == ""{cxt.Resource.Stamp.Name}""
        | where Protocol == ""FTP"" 
        | where (SiteName =~ ""{cxt.Resource.Name}"" or User contains ""{cxt.Resource.Name}"") and Message contains ""has been authorized"" 
        | project TIMESTAMP , User, UserAddress , Protocol, Message , SubscriptionId, WebSpaceName
        | count()";

}

private static string GetFailedFTPLogins(OperationContext<App> cxt)
{
    return
    $@"AntaresPublisherEvents 
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}
        | where SourceMoniker == ""{cxt.Resource.Stamp.Name}""
        | order by TIMESTAMP asc 
        | where Protocol == ""FTP"" 
        | where (SiteName =~ ""{cxt.Resource.Name}"" or User contains ""{cxt.Resource.Name}"") and Message contains ""has been rejected"" 
        | project TIMESTAMP , User, UserAddress , Protocol, Message , SubscriptionId, WebSpaceName";

}

private static string AllFTPLogins(OperationContext<App> cxt)
{
    return
    $@"AntaresPublisherEvents 
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}
        | order by TIMESTAMP asc 
        | where Protocol == ""FTP"" 
        | where SiteName =~ ""{cxt.Resource.Name}"" or User contains ""{cxt.Resource.Name}""
        | project TIMESTAMP, User, UserAddress, Message , SiteName, SubscriptionId, WebSpaceName";

}


[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "FTPDetector", Name = "FTP Login Detector", Description = "FTP Login Detector")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    //1) Add Data Summary
    DataSummary ds1 = new DataSummary("Successful Logins", "x" );
    DataSummary ds2 = new DataSummary("Failed Logins", "x", "red");
    res.AddDataSummary(new List<DataSummary>(){ ds1, ds2 }, "Logins Attempts");


    res.Dataset.Add(new DiagnosticData()
    {
        Table = await dp.Kusto.ExecuteQuery(AllFTPLogins(cxt), cxt.Resource.Stamp.Name),
        RenderingProperties = new Rendering(RenderingType.Table){
            Title = "FTP Attempts", 
            Description = "Some description here"
        }
    });

    return res;
}