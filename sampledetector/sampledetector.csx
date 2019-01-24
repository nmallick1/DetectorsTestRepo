private static string GetQuery(OperationContext<HostingEnvironment> cxt)
{
    return
    $@"<YOUR_TABLE_NAME>
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | <YOUR_QUERY>";
}

[HostingEnvironmentFilter(HostingEnvironmentType = HostingEnvironmentType.All, PlatformType = PlatformType.Windows)]
[Definition(Id = "sampledetector", Name = "Sample Detector", Description = "")]
public async static Task<Response> Run(DataProviders dp, OperationContext<HostingEnvironment> cxt, Response res)
{
    res.AddEmail("testing sample detector");
    return res;
}