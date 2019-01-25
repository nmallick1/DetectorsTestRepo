private static string GetQuery(OperationContext<App> cxt)
{
    return
    $@"LinuxRuntimeEvents
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | where Facility =~ '{cxt.Resource.Name}'
        | where EventName == 'SiteStartFailed'
        | project Time = bin(TIMESTAMP, 10m), Instance = strcat(substring(Tenant, 0, 6), '_', replace('DedicatedLinuxWebWorkerRole_IN', '', RoleInstance)), Message, timeTaken = parse_json(AdditionalInfo).timeTaken
        | summarize fastFailure = countif(timeTaken < 10), slowFailure = countif(timeTaken > 230), FailureCount = count() by Time, Instance, Message
        | order by Time asc";
}

private static string GetChartQuery(OperationContext<App> cxt)
{
    return
    $@"LinuxRuntimeEvents
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | where Facility =~ '{cxt.Resource.Name}'
        | where EventName == 'SiteStartFailed'
        | project Time = bin(TIMESTAMP, 10m), Instance = strcat(substring(Tenant, 0, 6), '_', replace('DedicatedLinuxWebWorkerRole_IN', '', RoleInstance))
        | summarize FailureCount = count() by Time, Instance
        | order by Time asc";
}

[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Linux, StackType = StackType.All, InternalOnly = false)]
[Definition(Id = "LinuxContainerStartFailure", Name = "Container Start Issues", Description = "Container start failure detector for Linux apps")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    try
    {
        var table = await dp.Kusto.ExecuteQuery(GetQuery(cxt), cxt.Resource.Stamp.Name);
        var chartTable = await dp.Kusto.ExecuteQuery(GetChartQuery(cxt), cxt.Resource.Stamp.Name);

        if (table.Rows.Count == 0)
        {
            res.AddInsight(new Insight(InsightStatus.Success, "Your container started successfully. No container start failure was found in this time range."));
            AddDockerLogLinkInsight(cxt, res);
        }
        else
        {
            //var message = String.Format("There were container start failures in this time range (Expand to see more details.)");
            //var insightDetails = new Dictionary<string, string>();
            var containerNotFound = false;
            var invalidMultiContainerDefinition = false;
            var noContainerInDefinition = false;
            var fastFailureFound = false;
            var slowFailureFound = false;

            foreach(DataRow row in table.Rows)
            {
                var entryMessage = (string)row["Message"];
                var fastFailure = (long)row["fastFailure"];

                if (fastFailure > 0)
                {
                    var insightDetails = new Dictionary<string, string>();
                    insightDetails.Add("Observation", "Container failed to start almost immediately.");

                    // WORKAROUND until the message fix gets deployed. TODO: change the string after upgrade.
                    if (!containerNotFound && entryMessage.StartsWith("One or more"))
                    {
                        var message = "Possible image name misconfiguration was detected";
                        insightDetails.Add("Possible reason", "Your Docker image may not exist.");
                        insightDetails.Add("Suggestion", "Please check the repository name and the image name to make sure they are correct.");

                        res.AddInsight(new Insight(InsightStatus.Critical, message, insightDetails));
                        containerNotFound = true;
                    }
                    else if (!containerNotFound && entryMessage.StartsWith("No such image:"))
                    {
                        var message = "Your Docker image was not found";
                        insightDetails.Add("Failure reason", "Either your Docker image was not found or login failed.");
                        insightDetails.Add("Error message", entryMessage);
                        insightDetails.Add("Suggestion", "Please check the container name, user name, and password to make sure they are correct.");

                        res.AddInsight(new Insight(InsightStatus.Critical, message, insightDetails));
                        containerNotFound = true;
                    }
                    else if (!invalidMultiContainerDefinition && entryMessage.StartsWith("Valid multi container definition not found"))
                    {
                        var message = "Invalid multi container definition was detected";
                        var realError = entryMessage.Replace("Valid multi container definition not found: ex = ", "");
                        insightDetails.Add("Error message", realError);
                        insightDetails.Add("Suggestion", "Please fix the invalid container definition.");

                        res.AddInsight(new Insight(InsightStatus.Critical, message, insightDetails));
                        invalidMultiContainerDefinition = true;
                    }
                    else if (!noContainerInDefinition && entryMessage.StartsWith("Invalid config file"))
                    {
                        var message = "No valid container definition was found";
                        var realError = entryMessage.Replace("Invalid config file, ", "");
                        insightDetails.Add("Error message", realError);
                        insightDetails.Add("Suggestion", "Please fix the invalid container definition.");

                        res.AddInsight(new Insight(InsightStatus.Critical, message, insightDetails));
                        noContainerInDefinition = true;
                    }
                    else if (!fastFailureFound)
                    {
                        var message = "Container start failed immediately";
                        insightDetails.Add("Possible reason", "Your Docker image may not exist.");
                        insightDetails.Add("Suggestion", "Please check the repository name, image name, and container definitions.");
                        res.AddInsight(new Insight(InsightStatus.Critical, message, insightDetails));
                        fastFailureFound = true;
                    }
                }

                if (!slowFailureFound)
                {
                    var slowFailure = (long)row["slowFailure"];

                    if (slowFailure > 0)
                    {
                        var message = "Container start timed out";
                        var insightDetails = new Dictionary<string, string>();
                        insightDetails.Add("Observation", "Container started but did not respond to health checks.");

                        if (entryMessage.Contains("Multi container unit"))
                        {
                            insightDetails.Add("Error message", entryMessage);
                            insightDetails.Add("Suggestion", "Check your App Settings to make sure that the PORT setting of your container is correct.");
                        }
                        else
                        {
                            insightDetails.Add("Suggestion", "Check your App Settings to make sure that the PORT setting of your container is correct.");
                        }

                        res.AddInsight(new Insight(InsightStatus.Critical, message, insightDetails));
                        slowFailureFound = true;
                    }
                }
            }

            //res.AddInsight(new Insight(InsightStatus.Critical, message, insightDetails));
            AddDockerLogLinkInsight(cxt, res);

            // Remove unnecessary columns from the retrieved table before rendering
            var renderingTable = table.Copy();
            renderingTable.Columns.Remove("slowFailure");
            renderingTable.Columns.Remove("fastFailure");

            res.Dataset.Add(new DiagnosticData()
            {
                Table = chartTable,
                RenderingProperties = new TimeSeriesRendering(){Title = "Number of Container Start Failures", GraphType = TimeSeriesType.BarGraph}
            });

            res.Dataset.Add(new DiagnosticData()
            {
                Table = renderingTable,
                RenderingProperties = new TableRendering(),
            });
        }
    }
    catch(Exception ex)
    {
        res.AddInsight(new Insight(InsightStatus.Critical, ex.ToString(), null));
    }

    return res;
}

private static void AddDockerLogLinkInsight(OperationContext<App> cxt, Response res)
{
    var dnsSuffix = cxt.Resource.Stamp.DnsSuffix ?? "azurewebsites.net";
    var kuduUrl = $"https://{cxt.Resource.Name}.scm.{dnsSuffix}/api/logs/docker";
    var kuduDetails = new Dictionary<string, string>();

    kuduDetails.Add("Docker logs Link: ", $@"<a href=""{kuduUrl}"" target=""_blank"">Get JSON with Docker log links</a> ");
    res.AddInsight(new Insight(InsightStatus.Info, "Docker logs", kuduDetails));
}
