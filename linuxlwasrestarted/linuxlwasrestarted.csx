private static string GetQuery(OperationContext<App> cxt)
{
    return
    $@"LinuxRuntimeEvents
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | where EventName == 'LWASRestarted'
        | project FiveMin = bin(TIMESTAMP, 5m), Tenant, RoleInstance
        | summarize RestartCount = count() by FiveMin, Tenant, RoleInstance
        | join (LinuxSiteStats
          | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
          | where SiteName =~ '{cxt.Resource.Name}'
          | project FiveMin = bin(TIMESTAMP, 5m), Tenant, RoleInstance
          | distinct FiveMin, Tenant, RoleInstance
        ) on FiveMin, Tenant, RoleInstance
        | project FiveMin, Instance = strcat(substring(Tenant, 0, 6), '_', replace('DedicatedLinuxWebWorkerRole_IN', '', RoleInstance)), RestartCount";
}

[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Linux, StackType = StackType.All)]
[Definition(Id = "LinuxLWASRestarted", Name = "LWAS Restart events", Description = "This detector finds any restart events of LWAS, the platform service running on Linux workers")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    var table = await dp.Kusto.ExecuteQuery(GetQuery(cxt), cxt.Resource.Stamp.Name);

    if (table.Rows.Count == 0)
    {
        res.AddInsight(new Insight(InsightStatus.Success, "No LWAS restart event found in the time range", null));
    }
    else
    {
        if (table.Rows.Count > 1)
        {
            var message = String.Format("There were {0} restart events in the time range.", table.Rows.Count);
            res.AddInsight(new Insight(InsightStatus.Critical, message, null));
        }
        else
        {
            var message = String.Format("There was a restart event in the time range.");
            res.AddInsight(new Insight(InsightStatus.Critical, message, null));
        }

        res.Dataset.Add(new DiagnosticData()
        {
            Table = table,
            RenderingProperties = new TimeSeriesRendering(){Title = "LWAS Restarts", GraphType = TimeSeriesType.BarGraph}
        });
    }

    return res;
}