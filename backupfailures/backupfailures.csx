[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "backupFailures", Name = "Backup Failures", Author = "pbabut", Description = "Looks into backup operations and analyzes their failures")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    //Generate Insights
    var insights = new List<Insight>();
    var Map = GetMap();
    //Query to get failure details
    var failureDetailsTable =  await dp.Kusto.ExecuteQuery(GetFailures(cxt), cxt.Resource.Stamp.Name);
    //Query to get counts of success v/s failure jobs
    var failureStatsTable = await dp.Kusto.ExecuteQuery(GetFailureStats(cxt), cxt.Resource.Stamp.Name);
    var totalBackupJobs = Int32.Parse(failureStatsTable.Rows[0]["Failed"].ToString()) + Int32.Parse(failureStatsTable.Rows[0]["Success"].ToString()) + Int32.Parse(failureStatsTable.Rows[0]["Partial Succeess"].ToString()) + Int32.Parse(failureStatsTable.Rows[0]["Skipped"].ToString());
    
    //////
    /*
    res.Dataset.Add(new DiagnosticData()
    {
        Table = failureStatsTable,
        RenderingProperties = new Rendering(RenderingType.Table){Title = "Sample Table", Description = "Some description here"}
    });

    foreach (DataRow row in failureDetailsTable.Rows) 
    {
        var shortReason = row["Insights"].ToString().ToLower();
        res.AddInsight(new Insight(InsightStatus.Success, shortReason));
    }
    foreach (DataColumn col in failureDetailsTable.Columns) {
        res.AddInsight(new Insight(InsightStatus.Critical, col.ColumnName));
    }

    foreach (DataRow row in failureDetailsTable.Rows) {
        foreach (DataColumn col in failureDetailsTable.Columns) {
            res.AddInsight(new Insight(InsightStatus.Critical, row[col].ToString()));
        }
    }
    */
    //////
    
    foreach (DataRow row in failureDetailsTable.Rows)
    {
        var shortReason = row["Insights"].ToString().ToLower();
        if(Map.ContainsKey(shortReason)){
            row["Insights"] = Map[shortReason].Reason;
            row["Next Steps"] = Map[shortReason].NextStep;
    
            var insightDetails = new Dictionary<string, string>();
            insightDetails.Add("Next Steps", Map[shortReason].NextStep);
            insightDetails.Add("Detailed Error", row["Detailed Error"].ToString());
            insightDetails.Add("Error Count", row["Error Count"].ToString());

            var insight = new Insight(InsightStatus.Critical, Map[shortReason].Reason, insightDetails);
            insights.Add(insight);

        } else {
             var insightDetails = new Dictionary<string, string>();
             insightDetails.Add("Detailed Error", row["Detailed Error"].ToString());
             insightDetails.Add("Error Count", row["Error Count"].ToString());
             
             var insight = new Insight(InsightStatus.Critical, "Backup could not finish successfully", insightDetails);
             insights.Add(insight);
        } 
    }

    
    if(insights.Count == 0 && totalBackupJobs == 0) 
    {
        if (DateTime.Parse(cxt.EndTime) - DateTime.Parse(cxt.StartTime) >= TimeSpan.FromHours(24))
        {
            var zeroInsightDetails = new Dictionary<string, string>();
            zeroInsightDetails.Add("Recommendation: ", "We recommend that you setup automated daily backups for all your critical apps. In case of accidental data loss or corruption, these backups will save you a lot of pain. The following link explains the process in detail ");
            zeroInsightDetails.Add("Learn more: ", @"<a href=""https://docs.microsoft.com/en-us/azure/app-service/web-sites-backup"" target=""_blank"">How to backup your app in Azure!</a> ");
            var zeroInsight = new Insight(InsightStatus.Critical, "No backups ran for this app in last 24 hrs", zeroInsightDetails);
            insights.Add(zeroInsight);
            
            //email.Add("We recommend that you setup automated daily backups for all your critical apps. In case of accidental data loss or corruption, these backups will save you a lot of pain. The following link explains the process in detail ");
            //email.Add(@"<a href=""https://docs.microsoft.com/en-us/azure/app-service/web-sites-backup"" target=""_blank"">How to backup your app in Azure!</a> ");
        }

    }
    

    res.AddInsights(insights);
    
   if(totalBackupJobs > 0)
   {
       //Graph - trend of backups
        res.Dataset.Add(new DiagnosticData()
        {
            Table = await dp.Kusto.ExecuteQuery(GetFailureTrend(cxt), cxt.Resource.Stamp.Name),
            RenderingProperties = new TimeSeriesRendering()
            {
                Title = "Backup Jobs timeline", 
                Description = "Shows how the jobs have been performing over a period of time",
                GraphType = TimeSeriesType.BarGraph
            }
        });

        //Summary stats - summary of backups
        var failedBackupsLabel = "Failed Backup";
        if(failureStatsTable.Rows[0]["Failed"].ToString() != "1") failedBackupsLabel += "s";
        var successfulBackupsLabel = "Successful Backup";
        if(failureStatsTable.Rows[0]["Success"].ToString() != "1") successfulBackupsLabel += "s";
        var partialBackupsLabel = "Partial Backup";
        if(failureStatsTable.Rows[0]["Partial Succeess"].ToString() != "1") partialBackupsLabel += "s";
        var skippedBackupsLabel = "Skipped Backup";
        if(failureStatsTable.Rows[0]["Skipped"].ToString() != "1") skippedBackupsLabel += "s";
    
        DataSummary ds1 = new DataSummary(failedBackupsLabel, failureStatsTable.Rows[0]["Failed"].ToString(), "#f65314");
        DataSummary ds2 = new DataSummary(successfulBackupsLabel, failureStatsTable.Rows[0]["Success"].ToString(), "#7cbb00");
        DataSummary ds3 = new DataSummary(partialBackupsLabel, failureStatsTable.Rows[0]["Partial Succeess"].ToString(), "#00a1f1");
        DataSummary ds4 = new DataSummary(skippedBackupsLabel, failureStatsTable.Rows[0]["Skipped"].ToString(), "#ffbb00");
        res.AddDataSummary(new List<DataSummary>() { ds1, ds2, ds3, ds4 }); 

   }
    

    
/*
    //Customer facing email
    
    var rawBackupJobsTable = await dp.Kusto.ExecuteQuery(GetRawBackupJobs(cxt), cxt.Resource.Stamp.Name);
    var numSuccess = 1;
    var numFailures = 5;
    var numInsights = 1;
    var emailContent = getEmailBody(numSuccess, numFailures, numInsights, DateTime.Parse(cxt.StartTime), DateTime.Parse(cxt.EndTime), cxt.Resource.Name, insights, rawBackupJobsTable);
    res.AddEmail(emailContent); 
*/
    return res;
}


private static string GetRawBackupJobs(OperationContext<App> cxt)
{
    return
    $@"AntaresAdminControllerEvents
	| where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
    | where SiteName =~ '{cxt.Resource.Name}'  
    | where EventId == 40073 and (Verb == ""Backup"" or Action == ""Backup"") and Success != """" 
    | take 11
    | project PreciseTimeStamp, Success ";

}

private static string GetFailures(OperationContext<App> cxt)
{
    return
    $@"AntaresAdminControllerEvents
	| where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
    | where SiteName =~ '{cxt.Resource.Name}' 
    | where EventId == 40073 and (Verb == ""Backup"" or Action == ""Backup"") and Success == ""Skipped"" or Success == ""Failed""
    | summarize ErrorCount=count() by Details
    | order by ErrorCount
    | extend Level = ""Critical"", Insights=substring(Details, 0, 19)
    | extend [""Next Steps""] =Insights, [""Detailed Error""] = Details, Count=ErrorCount  
    | project Level, Insights, [""Next Steps""],  [""Detailed Error""], Count, [""Error Count""] = ErrorCount 
    | project-away Count";

}

private static string GetFailureTrend(OperationContext<App> cxt)
{
    return
    $@"AntaresAdminControllerEvents
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | where SiteName =~ '{cxt.Resource.Name}'  
        | where EventId == 40073 and (Verb == ""Backup"" or Action == ""Backup"") 
        | project PreciseTimeStamp, Failed = (Success == ""Failed""), Succeeded = (Success == ""Succeeded""), Skipped = (Success == ""Skipped""), PartiallySucceeded = (Success == ""PartiallySucceeded"")
        | summarize count(Failed), count(Succeeded), count(Skipped), count(PartiallySucceeded)  by bin(PreciseTimeStamp, 5m)
        | order by PreciseTimeStamp asc
        | project PreciseTimeStamp, Failed = count_Failed, Success = count_Succeeded, [""Partial Succeess""] = count_PartiallySucceeded , Skipped = count_Skipped" ;
}

private static string GetFailureStats(OperationContext<App> cxt)
{
    return
    $@"AntaresAdminControllerEvents
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | where SiteName =~ '{cxt.Resource.Name}' 
        | where EventId == 40073 and (Verb == ""Backup"" or Action == ""Backup"") 
        | project PreciseTimeStamp, Failed = (Success == ""Failed""), Succeeded = (Success == ""Succeeded""), Skipped = (Success == ""Skipped""), PartiallySucceeded = (Success == ""PartiallySucceeded"")
        | summarize count(Failed), count(Succeeded), count(Skipped), count(PartiallySucceeded)
        | project Failed = count_Failed, Success = count_Succeeded, [""Partial Succeess""] = count_PartiallySucceeded , Skipped = count_Skipped" ;
}
private static string convertRawBackupJobsTableToEmailBody(DataTableResponseObject rawBackupJobsTable)
{
    string rows = "";
    int i = 0;
    foreach(var item in rawBackupJobsTable.Rows)
    {
        i++;
        if(i==11)
        {
            rows += $@"<tr> 
                    <th scope=""row"">...</th> 
                    <td>Truncated for brevity</td>
                    <td>...</td>
                </tr>";
        } else
        {
            var time = DateTime.Parse(item[0]).ToString() + " UTC";
            rows += $@"<tr> 
                    <th scope=""row"">{i}</th> 
                    <td>{time}</td>
                    <td>{item[1]}</td>
                </tr>";
        }
        
    }

    string body =  $@"
        <table class=""table""> 
            <thead> 
                <tr> 
                    <th scope=""col"">Backup Job</th> 
                    <th scope=""col"">TimeStamp</th> 
                    <th scope=""col"">Status</th>  
                </tr> 
            </thead> 
            <tbody> 
                {rows}
            </tbody> 
        </table>";
    
    return body;
}

private static string convertInsightsToEmailBody(List<Insight> insights)
{
    string rows = "";
    foreach(var insight in insights)
    {
        string bodyItems = $@"<td>";
        foreach(var detailItem in insight.Body)
        {
            bodyItems += $@"<b>{detailItem.Key}: </b>	
                            {detailItem.Value} </br>";
        }
        bodyItems +="</td>";

        rows += $@"<tr> 
                    <th scope=""row"">{insight.Message}</th> 
                    {bodyItems}
                </tr>";
    }
    string body =  $@"
        <table class=""table""> 
            <thead> 
                <tr> 
                    <th scope=""col"">Insight</th> 
                    <th scope=""col"">More info</th>  
                </tr> 
            </thead> 
            <tbody> 
                {rows}
            </tbody> 
        </table>";
    
    return body;
}
private static string getEmailBody(int s, int f, int i, DateTime startTime, DateTime endTime, string siteName, List<Insight> insights, DataTableResponseObject rawBackupJobsTable)
{
    List<string> email = new List<string>();
    
    email.Add("Hi AppService user, ");
    email.Add("");
    email.Add("Thank you for allowing me the opportunity to assist you today. I understand the importance of ensuring healthy backups for your apps. I would be happy to assist you. ");
    email.Add("");
    email.Add($"I have gone ahead and analyzed the backup logs for your app <b>{siteName}</b>  from <b>{startTime.ToString()} (UTC)</b> to <b>{endTime.ToString()} (UTC)</b>. ");
    email.Add("");
    if (f == 0 && s == 1)
    {
        email.Add("I see that the backup is healthy:-");
        email.Add("");
        email.Add($"\t[{startTime.ToString()} (UTC)] \t - \t [{endTime.ToString()} (UTC)] \t - \t<mark> <font size=\"3\" color=\"green\">1 Successful backup</font> </mark>");
        email.Add("");
        email.Add("Everything looks good and nothing is actionable at this time");
    }
    if (f == 0 && s > 1)
    {
        email.Add("I see that the backup jobs are healthy:-");
        email.Add("");
        email.Add("<mark> [[[[[[[[ INSERT PRAVEEN ]]]]]]]] </mark>");
        email.Add("");
        email.Add("Everything looks good and nothing is actionable at this time");
    }
    if (f == 0 && s == 0 && i == 0)
    {
        email.Add("I see that that no backup jobs have run during this time.");
        email.Add("");
        email.Add("");
        email.Add($@"<table class=""table""> <thead> <tr> <th scope=""col"">Start Time (UTC)</th> <th scope=""col"">End Time (UTC)</th> <th scope=""col"">Number of Backups</th> </tr> </thead> <tbody> <tr> <td scope=""row"">{startTime.ToString()}</td> <td>{endTime.ToString()}</td> <td>0 Backups</td> </tr> </tbody> </table>");
        email.Add("");
        email.Add($"Here is the summary of insights that I could gather for {siteName}.");
        email.Add("");
        email.Add(convertInsightsToEmailBody(insights));
        
    }
    if (f == 1 && s == 0)
    {
        email.Add("I see that the backup has failed:-");
        email.Add("");
        email.Add(" [[[[[[[[ INSERT PRAVEEN ]]]]]]]] ");
        if (i > 0)
        {
            email.Add("");
            email.Add("I am including information to help you resolve this issue:-");
            email.Add("<mark> [[[[[[[[ INSERT THE INSIGHTS TABLE ]]]]]]]] </mark>");
        }

    }
    if (f >= 1 && f + s >= 2)
    {
        if (f == 1)
        {
            email.Add("I see that the backup has failed:-");
        }
        else
        {
            email.Add("I see some failed backups:-");
        }

        email.Add("");
        email.Add(convertRawBackupJobsTableToEmailBody(rawBackupJobsTable));
        if (s > 0)
        {
            email.Add("NOTE: Backups that are shown as 'Succeeded' are good and can be used for recovery.");
        }

        if (i > 0)
        {
            email.Add("");
            email.Add($"Here is the summary of insights that I could gather for {siteName}.");
            email.Add("");
            email.Add(convertInsightsToEmailBody(insights));
        }
    }

    email.Add("");
    email.Add($"I hope that this information is useful in diagnosing backup failures for {siteName}.");
    //email.Add("");
    //email.Add(@"<center> <mark> [[[[[[[[ INSERT THE ""Backup Jobs timeline"" GRAPH ]]]]]]]] </mark> </center>");
    email.Add("");
    email.Add("It is our goal to make sure you are completely satisfied with the level of service we are providing you. Please let me know if I have not satisfied all of your questions. I want to personally thank you for being an Azure AppService user. If you need additional information or assistance, please don’t hesitate to reach back.");
    email.Add("");
    email.Add("Have a great day!  ");
    email.Add("");
    email.Add("Sincerely, ");
    email.Add("");
    email.Add("AppServices Team");
    var response = "";
    email.ForEach(x => response += x + "<BR>");
    return response;

}

public class Value
{
    public string Reason { get; set; }
    public string NextStep { get; set; }
}

public static  Dictionary<string, Value> GetMap()
{
    var map = new Dictionary<string, Value>();
    map.Add("storage access fail", new Value {Reason = "SAS URI is invalid due to missing scopes, expired token, etc", NextStep = "Delete backup schedule and reconfigure it" });
    map.Add("backup/restore featu", new Value {Reason = "Bug in ANT66", NextStep = "None, already fixed" });
    map.Add("the website + datab", new Value {Reason = "The web app + database size is over the 10GB limit", NextStep = "You can use a backup.filter file to exclude some files from the backup, or remove the database portion of the backup and use externally offered backups instead" });
    map.Add("error occurred whil", new Value {Reason = "MySQL connection string has invalid user / password combo", NextStep = "Update database connection string" });
    map.Add("cannot resolve", new Value {Reason = "SAS URI is invalid due to missing scopes, expired token, etc", NextStep = "Delete backup schedule and reconfigure it" });
    map.Add("login failed for us", new Value {Reason = "SQL Azure connection string has invalid user / password combo", NextStep = "Update database connection string" });
    map.Add("create database cop", new Value {Reason = "Database copy failed because db user didn't have copy privilege", NextStep = "Use admin user in connection string" });
    map.Add("the server principa", new Value {Reason = "db user needs access to master database", NextStep = "Use admin user in connection string" });
    map.Add("a network-related o", new Value {Reason = "Unable to connect to the database using the connection string. Could be because DB firewall is blocking access from our service.", NextStep = "Check that the connection string is valid, whitelist stamp VIP in database server settings" });
    map.Add("database connection", new Value {Reason = "Connection string format isn't valid", NextStep = "Check the backup log file for the specific details and actionable information." });
    map.Add("the database compat", new Value {Reason = "DAC FX export tools need to be updated to support new API level (Fix in ANT68)", NextStep = "Change the compatibility level to something in the supported range https://technet.microsoft.com/en-us/library/bb933794%28v=sql.110%29.aspx?f=255&MSPPError=-2147217396" });
    map.Add("mysql backup exited", new Value {Reason = "MySqlDump.exe failed to dump the database to a script", NextStep = "Varies based on exit code, but most likely due to SSL backup not being supported. If SSL is a business requirement, we recommend using an alternative database backup solution (snapshots and azure backup vault)" });
    map.Add("one or more unsuppo", new Value {Reason = "DAC FX export fails because the database schema has unsupported elements", NextStep = "Determine which elements of the schema are unsupported in the bacpac format. Try manually exporting the db using SQL Server Management Studio to get better info." });
    map.Add("the network name ca", new Value {Reason = "Storage volume access failure - probably some fileserver issue", NextStep = "User mitigation not possible, caused by platform issues" });
    map.Add("the process failed ", new Value {Reason = "SQL Server backup failure", NextStep = "" });
    map.Add(@"c:\resources\direct", new Value {Reason = "Storage volume access failure - probably some fileserver issue. The path GUID will change with each site, since it's a temp directory used for backups only.", NextStep = "User mitigation not possible, caused by platform issues" });
    map.Add("the network path wa", new Value {Reason = "Storage volume access failure - probably some fileserver issue", NextStep = "User mitigation not possible, caused by platform issues" });
    map.Add("value cannot be nul", new Value {Reason = "Happens to some sites running on Azure Files. Root cause unknown, looks like a bug in our platform.", NextStep = "" });
    map.Add(@"\\?\c:\resources\di", new Value {Reason = "Storage volume access failure - probably some fileserver issue", NextStep = "User mitigation not possible, caused by platform issues" });
    map.Add("cannot open server ", new Value {Reason = "SQL Azure connection failed with invalid login", NextStep = "Check that the connection string is valid" });
    map.Add("missing mandatory p", new Value {Reason = "SAS URI is invalid due to missing scopes, expired token, etc", NextStep = "Delete backup schedule and reconfigure it" });
    map.Add("database copy of", new Value {Reason = "Database is possibly too large, or server is too slow.", NextStep = "Check database size, because the copy step is timing out. Check server performance during backup." });
    map.Add("could not extract p", new Value {Reason = "SQL Azure database dump fails because of schema or configuration issue", NextStep = "Customer should check the backup log message for an actionable cause. Cause varies." });
    map.Add("object reference no", new Value {Reason = "Null ref happening in the DAC export library. Seems to be a bug in their framework.", NextStep = "User mitigation not possible, caused by platform issues" });
    map.Add("the specified netwo", new Value {Reason = "Storage volume access failure - probably some fileserver issue", NextStep = "User mitigation not possible, caused by platform issues" });
    map.Add("the device is not r", new Value {Reason = "Storage volume access failure - probably some fileserver issue", NextStep = "" });
    map.Add("there is not enough", new Value {Reason = "Disk space ran out while doing the database export", NextStep = "User mitigation not possible, caused by platform issues" });

    return map;
}