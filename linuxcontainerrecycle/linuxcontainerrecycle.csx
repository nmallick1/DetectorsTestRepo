private static string GetQuery(OperationContext<App> cxt)
{
    return
    $@"LinuxRuntimeEvents
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | where Facility =~ '{cxt.Resource.Name}'
        | where EventName in ('ContainerRecycleStarted', 'SiteStopRequested')
        | project Time = bin(TIMESTAMP, 10m), Instance = strcat(substring(Tenant, 0, 6), '_', replace('DedicatedLinuxWebWorkerRole_IN', '', RoleInstance)), Reason
        | summarize RecycleCount = count() by Time, Instance, Reason";
}

private static string GetChartQuery(OperationContext<App> cxt)
{
    return
    $@"LinuxRuntimeEvents
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | where Facility =~ '{cxt.Resource.Name}'
        | where EventName in ('ContainerRecycleStarted', 'SiteStopRequested')
        | project Time = bin(TIMESTAMP, 10m), Instance = strcat(substring(Tenant, 0, 6), '_', replace('DedicatedLinuxWebWorkerRole_IN', '', RoleInstance))
        | summarize RecycleCount = count() by Time, Instance";
}

private static string GetLwasRestartQuery(OperationContext<App> cxt)
{
    return
    $@"LinuxRuntimeEvents
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | where EventName == 'LWASRestarted'
        | project Time = bin(TIMESTAMP, 10m), Tenant, RoleInstance
        | summarize RestartCount = count() by Time, Tenant, RoleInstance
        | join (LinuxSiteStats
          | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
          | where SiteName =~ '{cxt.Resource.Name}'
          | project Time = bin(TIMESTAMP, 10m), Tenant, RoleInstance
          | distinct Time, Tenant, RoleInstance
        ) on Time, Tenant, RoleInstance
        | project Time, Instance = strcat(substring(Tenant, 0, 6), '_', replace('DedicatedLinuxWebWorkerRole_IN', '', RoleInstance)), RestartCount";
}

[SupportTopic(Id = "32570954", PesId = "16170")] // Availability, Performance, and Application Issues/Web app restarted [Web App (Linux)]
[SupportTopic(Id = "32570954", PesId = "16333")] // Availability, Performance, and Application Issues/Web app restarted [Web App for Containers]
[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Linux, StackType = StackType.All, InternalOnly = false)]
[Definition(Id = "LinuxContainerRecycle", Name = "Web App Restarted", Author = "mikono", Description = "Container recycle and stop events detector for Linux apps")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    try
    {
        var table = await dp.Kusto.ExecuteQuery(GetQuery(cxt), cxt.Resource.Stamp.Name);
        var chartTable = await dp.Kusto.ExecuteQuery(GetChartQuery(cxt), cxt.Resource.Stamp.Name);
        DataTable lwasRestartTable = null;

        if (table.Rows.Count == 0)
        {
            res.AddInsight(new Insight(InsightStatus.Success, "No container recycle event was found in the time range.", null));
        }
        else
        {
            //var message = String.Format("There were container recycle events in the time range.");
            //res.AddInsight(new Insight(InsightStatus.Warning, message, null));
            AddRecycleReasonInsights(cxt, res, table);
        }

        if (cxt.IsInternalCall)
        {
            lwasRestartTable = await dp.Kusto.ExecuteQuery(GetLwasRestartQuery(cxt), cxt.Resource.Stamp.Name);
            var numLwasRestarts = lwasRestartTable.Rows.Count;

            if (numLwasRestarts == 0)
            {
                res.AddInsight(new Insight(InsightStatus.Success, "No LWAS restart event found in the time range"));
            }
            else
            {
                if (numLwasRestarts > 1)
                {
                    var message = String.Format("There were {0} restart events in the time range.", numLwasRestarts);
                    res.AddInsight(new Insight(InsightStatus.Critical, message));
                }
                else
                {
                    var message = String.Format("There was one restart event in the time range.");
                    res.AddInsight(new Insight(InsightStatus.Critical, message));
                }
            }
        }

        // Add tables
        res.Dataset.Add(new DiagnosticData()
        {
            Table = chartTable,
            RenderingProperties = new TimeSeriesRendering(){Title = "Container Recycles", GraphType = TimeSeriesType.BarGraph}
        });

        res.Dataset.Add(new DiagnosticData()
        {
            Table = table,
            RenderingProperties = new TableRendering(),
        });

        if (cxt.IsInternalCall && lwasRestartTable != null)
        {
            res.Dataset.Add(new DiagnosticData()
            {
                Table = lwasRestartTable,
                RenderingProperties = new TimeSeriesRendering(){Title = "LWAS Restarts", GraphType = TimeSeriesType.BarGraph}
            });
        }
    }
    catch(Exception ex)
    {
        res.AddInsight(new Insight(InsightStatus.Critical, ex.ToString(), null));
    }

    return res;
}

private static void AddRecycleReasonInsights(OperationContext<App> cxt, Response res, DataTable table)
{
    var flags = new List<string>();

    foreach(DataRow row in table.Rows)
    {
        var reason = (string)row["Reason"];
        if (flags.Contains(reason)) {
            continue;
        }

        flags.Add(reason);

        switch (reason) {
            case "RestartTriggerWatcher":
                res.AddInsight(new Insight(InsightStatus.Warning, "Your app deployment caused the container to stop and start again"));
                break;

            case "RestartApiOperation":
                {
                    var insightsDetails = new Dictionary<string, string>();
                    var description = "Your container(app) was restarted due to a user action like stopping the app from azure portal. " +
                                      "You can find more information about these operations in activity logs.";
                    
                    var bladeUrl = $"https://portal.azure.com/subscriptions/{cxt.Resource.SubscriptionId}/resourceGroups/{cxt.Resource.ResourceGroup}/providers/Microsoft.Web/sites/{cxt.Resource.Name}/EventLogs";

                    insightsDetails.Add("Description", description);
                    //insightsDetails.Add("View Activity Logs", $"<a href=\"{bladeUrl}\">View Activity Logs</a>");
                    insightsDetails.Add("Learn more", "<a href=\"https://bit.ly/2jULkCJ\" target=\"_blank\">How to view Activity Logs</a>");
                    res.AddInsight(new Insight(InsightStatus.Warning, "User requested app to restart", insightsDetails));
                }
                break;

            case "RecycleApiOperation":
                res.AddInsight(new Insight(InsightStatus.Warning, "User requested container recycle was detected"));
                break;

            case "StorageVolumeFailover":
                {
                    var insightsDetails = new Dictionary<string, string>();
                    var description = "Your application was recycled due to an intermittent Azure infrastructure issue " +
                                    "while accessing remote file storage. This can happen due to multiple reasons like platform " +
                                    "instances getting upgraded or instance(s) experiencing high latencies accessing the remote storage. " +
                                    "In case the instance(s) where your application is running is experiencing high latencies accessing " +
                                    "remote storage, platform tries to heal your application by switching to different remote storage " +
                                    "which is having low latency. This can also cause the application process to restart.";
                        
                    insightsDetails.Add("Description", description);
                    res.AddInsight(new Insight(InsightStatus.Warning, "File server volume path change was found", insightsDetails));
                }
                break;

            case "LwasShuttingDown":
                {
                    var insightsDetails = new Dictionary<string, string>();
                    var description = "Your application was recycled as the Azure scale unit was undergoing an upgrade. " +
                                      "There are periodic updates made by Microsoft to the underlying Azure platform to improve " +
                                      "overall reliability, performance, and security of the platform infrastructure where " +
                                      "your application is running on." +
                                      " Most of these updates are performed without any impact upon your web app. " +
                                      "To reduce the impact of such events on your application, consider deploying your application to " +
                                      "multiple regions and use Azure Traffic Manager to distribute the load across regions.";
                        
                    insightsDetails.Add("Description", description);
                    insightsDetails.Add("Suggestion", "Consider using Azure Traffic Manager");
                    res.AddInsight(new Insight(InsightStatus.Warning, "Azure scale unit was undergoing an upgrade", insightsDetails));
                }
                break;

            case "SiteStartFailed":
                res.AddInsight(new Insight(InsightStatus.Warning, "Site was failing to start"));
                break;
                
            case "NullTokenFoundDuringChangeNotification":
            case "UnhealthyContainer":
                {
                    var insightsDetails = new Dictionary<string, string>();
                    var description = "Your application was stopped because the machine running your application was in a bad state. " +
                                      "Normally your app is automatically started without any impact upon your web app. " +
                                      "To reduce the impact of such events on your application, consider deploying your application to " +
                                      "multiple regions and use Azure Traffic Manager to distribute the load across regions.";
                        
                    insightsDetails.Add("Description", description);
                    insightsDetails.Add("Suggestion", "Consider using Azure Traffic Manager");
                    res.AddInsight(new Insight(InsightStatus.Warning, "Unhealthy worker was found", insightsDetails));
                }
                break;

        }
    }
}