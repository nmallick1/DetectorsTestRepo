private static string GetQuery(OperationContext<App> cxt)
{
    return
    $@"LinuxSiteStats
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | where SiteName =~ '{cxt.Resource.Name}'
        | extend Memory = todouble(MemoryPercentage), Instance = strcat(substring(Tenant, 0, 6), '_', replace('DedicatedLinuxWebWorkerRole_IN', '', RoleInstance))
        | summarize Average = avg(Memory) by bin(TIMESTAMP, 5m), Tenant, RoleInstance";
}

[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Linux, StackType = StackType.All, InternalOnly = false)]
[Definition(Id = "LinuxMemoryPercentage", Name = "Memory Usage", Description = "Average memory usage consumed by Linux apps", Author = "mikono")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    try
    {
        var table = await dp.Kusto.ExecuteQuery(GetQuery(cxt), cxt.Resource.Stamp.Name);

        bool highCpuFound = false;
        bool veryHighCpuFound = false;

        foreach(DataRow row in table.Rows)
        {
            if ((double)row["Average"] > 95.0)
            {
                veryHighCpuFound = true;
                break;
            }
            else if ((double)row["Average"] > 80.0)
            {
                highCpuFound = true;
            }
        }

        if (veryHighCpuFound)
        {
            var message = String.Format("Very high memory usage was detected.");
            res.AddInsight(new Insight(InsightStatus.Critical, message, null));
            
        }
        else if (highCpuFound)
        {
            var message = String.Format("High memory usage was detected.");
            res.AddInsight(new Insight(InsightStatus.Warning, message, null));
        }
        else
        {
            res.AddInsight(new Insight(InsightStatus.Success, "Memory usage is healthy.", null));
        }

/*
        if (table.Rows.Cast<DataRow>().Any(row => row.Field<double>("Average") > 80.0))
        {
            var message = String.Format("High CPU was detected.");
            res.AddInsight(new Insight(InsightStatus.Warning, message, null));
        }
        else
        {
            res.AddInsight(new Insight(InsightStatus.Success, "CPU is healthy.", null));
        }
*/
        res.Dataset.Add(new DiagnosticData()
        {
            Table = table,
            RenderingProperties = new TimeSeriesPerInstanceRendering() 
            {
                Title = "%Memory",
                GraphType = TimeSeriesType.LineGraph,
                GraphOptions = new
                {
                    forceY = new int[] { 0, 100 }
                }
            }
        });
    }
    catch(Exception ex){
        res.AddInsight(new Insight(InsightStatus.Critical, ex.ToString(), null));
    }
    
    return res;
}