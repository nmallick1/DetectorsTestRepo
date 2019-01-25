using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Cryptography;

[SupportTopic(Id = "32581615", PesId = "14748")]
[AppFilter(AppType = AppType.All, PlatformType = PlatformType.Windows | PlatformType.Linux, StackType = StackType.All, InternalOnly = false)]
[Definition(Id = "swap", Name = "Check Swap Operations", Author = "puneetg", Description = "Checks for deployment slot swap operations and reasons why they failed")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    var swapsTable1 = await dp.Kusto.ExecuteQuery(GetSwapOperationTimeline(cxt), cxt.Resource.Stamp.InternalName);
    var swapsummary = GetSummary(swapsTable1);
    
    
    if(swapsummary[0].Value == "0")
    {
        var insightDetails = new Dictionary<string, string>();
        insightDetails.Add("Recommendation", "If you publish new code or content to a production app, the best practice is to deploy to a staging slot first, test and perform a swap operation. This minimizes downtimes for the apps");
        insightDetails.Add("Learn more: ", @"<a href=""https://docs.microsoft.com/en-us/azure/app-service/web-sites-staged-publishing"" target=""_blank"">Setup staging environments in Azure App Service</a> ");
        res.AddInsight(new Insight(InsightStatus.Success, "No swap operations occured during this timeframe ", insightDetails));
    }
    else
    {
        //Proceed only if the number of swaps is > 0
            
        res.AddDataSummary(swapsummary);
        res.Dataset.Add(new DiagnosticData()
        {
            Table = swapsTable1,
            RenderingProperties = new TimeSeriesRendering() {
                Title = "Swap Operation Timeline",
                GraphOptions = new {
                    color = new string[] {"#006313","#b20505"}
                },
                GraphType = TimeSeriesType.BarGraph
            }
        });
        // Add a markdown section
        // We will use it here to explain how to create an insight

        if(cxt.IsInternalCall)
        {
            res.AddMarkdownView(@"
                Hello, 

                I've been investigating the issue that you've been having with your app, which looks to be related to an issue with a deployment slot swap operation. 

                In the last 24 hours, I noticed that you tried to swap your deployment slots, but the **deployment slot didn't swap appropriately**. 

                **Root Cause**

                The reason why that deployment slot swap failed is because the worker process in the 'stage-slot' slot aborted the warmup request.
                - When an HTTP request to the root URL path is aborted, **it usually happens if the web app has a URL rewrite rule** (such as Enforce Domain or Enforce HTTPs) to abort some requests.
                - Because the swap process will make another HTTP request to the root URL path on each web worker, this is most likely the case. 

                **Recommendation/Fix**
                
                I'd recommend that you modify this rewrite rule by appending the ```{WARMUP_REQUEST} and  {REMOTE_ADDR}``` server variables in the **URL rewrite rule's conditions in your Web.Config file.** For more details, please check out this **[blog post](http://ruslany.net/2017/11/most-common-deployment-slot-swap-failures-and-how-to-fix-them/)**, which will explain exactly how swaps work and provide more context to this recommended action. 

                If that doesn't work, then I'd suggest that you make sure that you have allowed the internal IP address range used by the swap process. Your web app may have IP restriction rules that prevent the swap process from connecting to it. 

                I hope this has been helpful in getting your web app back up and running and answered your question. If you have additional questions about your deployment slot swap issue that is not answered above or in the links for more details, please do not hesitate to ask. 

                Best Regards, 

            ", "Customer Ready Email");
        }

        var swapsTable = await dp.Kusto.ExecuteQuery(GetAllSwapOperations(cxt), cxt.Resource.Stamp.InternalName);
        res.Dataset.Add(new DiagnosticData()
        {
            Table = swapsTable,
            RenderingProperties = new TableRendering() {
                Title = "All Swap Operations",
                Description = "This table displays the details about each swap operation that occurred during this time period. You can see which slots the customers was trying to swap. If it was not successful, you will see the errors below."
            }
        });

        
        List<SwapDetails> failedSwaps = new List<SwapDetails>();
        foreach(DataRow row in swapsTable.Select("Status <> 'Success'"))
        {
            var activityId = row["RequestId"].ToString();
            if (!failedSwaps.Any(x=>x.RequestId == activityId))
            {
                SwapDetails swap = new SwapDetails();
                swap.TIMESTAMP = row["TIMESTAMP"].ToString();
                swap.SiteName = row["SiteName"].ToString();
                swap.RequestId = row["RequestId"].ToString();
                swap.Status = row["Status"].ToString();
                swap.TargetSlot = row["TargetSlot"].ToString();
                failedSwaps.Add(swap);
            }    
        }

        if (failedSwaps.Count > 0) 
        {
            var failedSwapsActivities = failedSwaps.Select(x => x.RequestId).ToList();

            var swapOperationsDetails  = await dp.Kusto.ExecuteQuery(GetSwapOperationDetails(cxt, failedSwapsActivities), cxt.Resource.Stamp.InternalName);        
            foreach (var failedSwap in failedSwaps)
            {                    
                DataView failedSwapTableView = swapOperationsDetails.DefaultView;
                failedSwapTableView.RowFilter = $" (ActivityId = '{failedSwap.RequestId}' or RequestId = '{failedSwap.RequestId}') and EventId = 40200";
                DataTable failedSwapTable = failedSwapTableView.ToTable("FailedSwaps", true, "TIMESTAMP", "TraceMessage");
                        
                if (failedSwapTable.Rows.Count > 0)
                {
                    string failedInstances = "<i>INSTANCE_ID_COMING_SOON</i>";                    
                    List<string> instancesProcessingChangeNotification, instancesWorkerProcessNotRunning, instancesWarmingUpLocalCacheFailed, instancesWarmupAppInitFailed;

                    var processingChangeNotificationFound = CheckTraceMessagesAndFindInstances(failedSwapTable, "TraceMessage LIKE '%to process change notification for site%'", out instancesProcessingChangeNotification);
                    var noWorkerProcessRunning = CheckTraceMessagesAndFindInstances(failedSwapTable, "TraceMessage LIKE '%No worker process is running on worker%'", out instancesWorkerProcessNotRunning);
                    var warmingUpLocalCacheFailed = CheckTraceMessagesAndFindInstances(failedSwapTable, "TraceMessage LIKE '%Warmup for ''LocalCache'' on server%' and TraceMessage LIKE '%failed.%'", out instancesWarmingUpLocalCacheFailed);
                    var warmupAppInitFailed = CheckTraceMessagesAndFindInstances(failedSwapTable, "TraceMessage LIKE '%Warmup for ''AppInit'' on server%' and TraceMessage LIKE '%failed.%'", out instancesWarmupAppInitFailed);

                    string cause = "";
                    string recommendation = "";                    
                        
                    if (warmupAppInitFailed)
                    {
                        failedInstances = string.Join(",", instancesWarmupAppInitFailed);
                        cause = $"The swap operation failed during Warm-up for AppInit code on the instance(s) {failedInstances}. This means that either your Web App is having a long warmup init time or requests to Warmup URL's are timing out";
                        recommendation = $"Please check the application logs to determine why the warmup requests are not succeeding or try restarting the process on the instance {failedInstances} and retry the swap operation. It is also recommend that you use Swap with Preview to avoid these kind of issues.";
                    }
                    else 
                    {
                        if (warmingUpLocalCacheFailed)
                        {
                            failedInstances = string.Join(",", instancesWarmingUpLocalCacheFailed);
                            cause = $"The swap operation failed during Warm-up for the LocalCache on the instance(s) {failedInstances}.";
                            recommendation = "Please check local cache configuration or try using swap with preview if local cache initialization time is very long.";
                        }
                        else
                        {
                            if (processingChangeNotificationFound)
                            {
                                failedInstances = string.Join(",", instancesProcessingChangeNotification);
                                cause = "The swap operation failed while processing change notifications on the instance(s) serving the requests.";
                                recommendation = $"Please try restarting the process on the instance(s) {failedInstances} and retry the swap operation. It is also recommend that you use Swap with Preview to avoid these kind of issues.";
                            }
                            else
                            {
                                if (noWorkerProcessRunning)
                                {
                                    failedInstances = string.Join(",", instancesWorkerProcessNotRunning);
                                    cause = $"The Swap operation failed because the worker process was not running on the instance(s) - {failedInstances}";
                                    recommendation = $"Make sure that the process is running on the instance(s) - {failedInstances}. You can try restarting the process or try to Scale Up or Down to see if that fixes the issue.";
                                }
                                else
                                {
                                    // Suggest looking at the TraceMessages table only in case of AppLens
                                    if(cxt.IsInternalCall)
                                    {
                                        cause = "We could not determine the exact reason why the Swap operation failed";
                                        recommendation = "Please check the detailed SWAP trace messages for this Swap Activity Id to understand why the failure happened.";
                                    }                                    
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(cause) && !string.IsNullOrWhiteSpace(recommendation) && failedSwap.Status.Contains("because the worker process") && failedSwap.Status.Contains("slot could not be started."))
                    {
                        var insightDetails = new Dictionary<string, string>();
                        insightDetails.Add("Root Cause", cause);
                        insightDetails.Add("Recommendation", recommendation);            
                        res.AddInsight(new Insight(InsightStatus.Critical, $"Failures detected during Swap Opearation with Id {failedSwap.RequestId}", insightDetails));                        
                    }
                    
                    // Display the TraceMessages table only in case of AppLens
                    if(cxt.IsInternalCall)
                    {
                        DataTable dtCombined = new DataTable();
                        dtCombined.Columns.Add("TraceMessages",typeof(String));
                        foreach (DataRow r in failedSwapTable.Rows)
                        {                
                            DataRow dr = dtCombined.NewRow();
                            dr["TraceMessages"] = r["TIMESTAMP"] + " - " + r["TraceMessage"];
                            dtCombined.Rows.Add(dr);
                        }
                        res.Dataset.Add(new DiagnosticData()
                        {
                            Table = dtCombined,
                            RenderingProperties = new TableRendering() {
                                Title = $"Detailed Swap Trace Messages for Swap with ActivitId = {failedSwap.RequestId}",
                                Description = $"Shows all the Trace logs for the Swap Activity with the ID - {failedSwap.RequestId}"
                            }
                        });
                    }
                }
                else
                {
                    //
                    // TODO - If in future we want to add more checks based on failedSwap.Status
                    //                  
                }
            }
        }
    }

    res.AddMarkdownView(@"
    The following links will help you understand how to setup deployment slots:-

    <a href=""https://docs.microsoft.com/azure/app-service/web-sites-staged-publishing"" target=""_blank"">Set up staging environments in Azure App Service</a>
    
    <a href=""http://ruslany.net/2016/10/using-powershell-to-manage-azure-web-app-deployment-slots/"" target=""_blank"">Using Powershell to manage Azure Web App Deployment Slots</a>

    <a href=""https://blogs.msdn.microsoft.com/benjaminperkins/2017/09/04/database-connection-string-when-swapping-between-app-servers-slots/"" target=""_blank"">How to configure Database Connection strings when using Slots </a>

    <a href =""https://stackoverflow.com/questions/31064071/how-to-prevent-azure-webjobs-from-being-swapped-in-azure-website-production"" target=""_blank"">How to prevent Azure webjobs from being swapped in Azure website production to staging slots</a>
    
    ", 
    "Deployment Slots documentation");

    res.AddMarkdownView(@"
    There are a number of reasons why a slot swap operation may not succeed including the following:-
    1. Local Cache Initialization

    2. Http Requests to the site root timing out.

    3. IP Restrictions affecting the ability of the swap process to connect.

    4. UrlRewrite rules configured incorrectly.

    The following links will help you understand these scenarios and how to resolve them:

    <a href=""http://ruslany.net/2017/11/most-common-deployment-slot-swap-failures-and-how-to-fix-them/"" target=""_blank"">How to fix Common Deployment Slot Swap issues.</a>
    
    <a href=""http://ruslany.net/2015/09/how-to-warm-up-azure-web-app-during-deployment-slots-swap/"" target=""_blank"">How to warm up Azure Web App during deployment slots swap.</a>
    ", 
    "How to resolve the most common Deployment Slots issues?");
    
    return res;
}

// From \\rdindex\index\rd_websites_stable\websites\src4\hosting\microsoft.web.hosting.runtime\logic\
static string ComputeHash(string input)
{
    SHA256 sha1 = new SHA256CryptoServiceProvider();
    // The ARR treats the input string as a wide-char array, so each char is 2-byte value. Consequently, we'll get the unicode
    // representation of the string.
    
    byte[] inputBytes = Encoding.Unicode.GetBytes(input);
    byte[] hashBytes = sha1.ComputeHash(inputBytes);

    char[] buffer = new char[hashBytes.Length * 2];

    for (int i = 0; i < hashBytes.Length; i++)
    {
        buffer[2 * i] = HexToASCII((hashBytes[i] & 0xF0) >> 4);
        buffer[2 * i + 1] = HexToASCII(hashBytes[i] & 0xF);
    }
    return new string(buffer);
}

// From \\rdindex\index\rd_websites_stable\websites\src4\hosting\microsoft.web.hosting.runtime\logic\
static char HexToASCII(int c)
{
    return (char)((c < 10) ? (c + '0') : (c + 'a' - 10));
}

private static bool CheckTraceMessagesAndFindInstances(DataTable failedSwapTable, string rowFilter, out List<string> instances)
{
    
    var traceMessagesMatch = failedSwapTable.Select(rowFilter);
    bool foundMatch = traceMessagesMatch.Length > 0 ? true : false;
    instances = new List<string>();

    if (foundMatch)
    {
        // https://stackoverflow.com/questions/4890789/regex-for-an-ip-address
        var regExpIpAddress = @"((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)";                    
        foreach (DataRow dr in traceMessagesMatch)
        {
            Regex regex = new Regex(regExpIpAddress);
            MatchCollection result = regex.Matches(dr["TraceMessage"].ToString());
            if (result.Count > 0)
            {
                string instanceId = ComputeHash(result[0].Value);
                if (!instances.Contains(instanceId))
                {
                    instances.Add(instanceId);
                }                            
            }
        }
    }

    return foundMatch;
}

private static List<DataSummary> GetSummary(DataTable dt) 
{
    int failed = 0, success = 0;
    foreach(DataRow row in dt.Rows) {
        failed += int.Parse(row["Failed"].ToString());
        success += int.Parse(row["Success"].ToString());
    }

    int total = failed + success;
    var dataSummaries = new List<DataSummary>();
    dataSummaries.Add(new DataSummary("Total", total.ToString(), "#0076a5"));
    dataSummaries.Add(new DataSummary("Success", success.ToString(), "#00871b"));
    dataSummaries.Add(new DataSummary("Failed", failed.ToString(), "#b20505"));

    return dataSummaries;
} 

private static string GetAllSwapOperations(OperationContext<App> cxt)
{
    string siteName = cxt.Resource.Name;

    return
    $@"AntaresAdminSubscriptionAuditEvents
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}        
        | where SiteName startswith '{siteName}(' or SiteName =~ '{siteName}'
        | where OperationType == 'Swap'
        | parse Address with * '&targetSlot=' TargetSlot
        | project TIMESTAMP, SiteName, TargetSlot, RequestId 
        | join kind = leftouter (
            AntaresAdminControllerEvents
            | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}        
            | where SiteName =~ '{siteName}' or SiteName startswith '{siteName}(' 
            | where Exception != ''
            | project RequestId, Exception 
            | summarize by RequestId, Exception
        ) on RequestId 
        | extend Status = iif(Exception != '', Exception, 'Success')
        | project-away RequestId1, Exception 
        | order by TIMESTAMP asc";
}

private static string GetSwapOperationDetails(OperationContext<App> cxt, List<string> ActivityIds)
{
    string siteName = cxt.Resource.Name;
    var normalizedActivities = NormalizeActivityIdList(ActivityIds);    
    return
    $@"AntaresAdminControllerEvents
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}        
        | where SiteName =~ '{siteName}' or SiteName startswith '{siteName}(' 
        | where ActivityId in ({normalizedActivities}) or RequestId in ({normalizedActivities})
        | parse Address with * '&targetSlot=' TargetSlot
        | project TIMESTAMP, SiteName, EventId, ActivityId, RequestId, Exception, TraceMessage, TargetSlot, Address
        | order by TIMESTAMP asc";
}


private static string GetSwapOperationTimeline(OperationContext<App> cxt)
{
    string siteName = cxt.Resource.Name;
    return
    $@"{GetAllSwapOperations(cxt)}
    | summarize Success = count(Status == 'Success'), Failed = count(Status != 'Success') by bin(TIMESTAMP, 5m) ";
}

private static string NormalizeActivityIdList(List<string> ActivityIds)
{
    var normalizedList = new List<string>();
    foreach (var activity in ActivityIds)
    {
        normalizedList.Add($"'{activity}'");
    }
    return string.Join(",",normalizedList);
}

class SwapDetails
{
    public string TIMESTAMP;
    public string SiteName;
    public string TargetSlot;
    public string RequestId;
    public string Status;   
}