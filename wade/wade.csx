private static string GetQuery(OperationContext<App> cxt)
{
    return
    $@"AntaresIISLogFrontEndTable 
    | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
    | project TIMESTAMP , ActivityId , S_sitename , Cs_uri_stem 
    | take 10 ";
}

[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "wade", Name = "Wades Detector", Author = "wadeh", Description = "")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    var tableData = await dp.Kusto.ExecuteQuery(GetQuery(cxt), cxt.Resource.Stamp.Name);

    try{
        foreach(DataRow row in tableData.Rows){
            var s = row["TIMESTAMP"];
            res.AddEmail(s.ToString());
        }
    }catch(Exception e){
        res.AddEmail(e.Message);
    }

    return res;
}