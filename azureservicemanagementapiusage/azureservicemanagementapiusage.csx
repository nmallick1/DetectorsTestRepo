using System.Text;
using System.Collections.Generic;

private static string tableQuerySegment = $@"cluster('wawscus').database('wawsprod').AntaresAdminGeoEvents";
private static string rdfeCallFilterQuerySegment = $@"| where ApiType =='RDFE' 
	and EventId == 40006
	and (isnotnull(UserAgent)
	and isnotempty(UserAgent)
	and UserAgent !contains'AcisExtHost' 
	and UserAgent !contains'Runner' 
	and UserAgent !contains'Antares'
	and UserAgent !contains'CanaryPopulator'
	and UserAgent !contains'portal')";

private static string webspaceNameExtractionQuerySegment = $@"| extend webspaceName = extract('/webspaces/([^/?]+)', 1, Address)";
private static string siteWithSlotNameExtractionQuerySegment = $@"| extend siteWithSlotName = extract('/[sS]ites/([^/?]+)', 1, Address)";
private static string serverFarmNameExtractionQuerySegment = $@"| extend serverFarmName = extract('/[sS]erver[fF]arms/([^/?]+)', 1, Address)";
private static string certificateNameExtractionQuerySegment = $@"| extend certificateName = extract('/certificates/([^/?]+)', 1, Address)";
private static string resourceTypeExtractionQuerySegment = $@"| extend Resource_Type = iif(isnotempty(siteWithSlotName) or Url_Template endswith 'sites', 'site', iif(isnotempty(serverFarmName) or Url_Template endswith 'serverfarms', 'serverfarm', iif(isnotempty(webspaceName) or Url_Template endswith 'webspaces', 'webspace', '')))";

private static string userAgentNameExtractionQuerySegment = 
    $@"| extend Api_Client = extract('^([^/]+)(/|$)', 1, UserAgent) 
	| extend UserAgentVersion = extract('^[^/]+/([^ ]+)( |$)', 1, UserAgent) 
	| extend AzurePowershell = extract('AzurePowershell/([^ ]+)( |$)', 1, UserAgent)
	| extend PS = extract('PSVersion/([^ ]+)( |$)', 1, UserAgent)
	| extend VSTS = extract('VSTS_([^ ]+)( |$)', 1, UserAgent)
	| extend TFS = extract('TFS_([^ ]+)( |$)', 1, UserAgent)
	| extend Api_Client = iif(isnotempty(AzurePowershell) or isnotempty(PS),'Powershell', Api_Client)
	| extend Api_Client = iif(isnotempty(VSTS) or isnotempty(TFS),'VSTS/TFS', Api_Client)
    | extend Api_Client = iif(Api_Client == 'Microsoft.WindowsAzure.Management.WebSites.WebSiteManagementClient','.NET SDK', Api_Client)";

private static string urlTemplateExtractionQuerySegment = 
    $@"| extend Url_Template = replace('/subscriptions/([^/]+)', '/subscriptions/<subscriptionId>', Address)
	| extend Url_Template = replace('/webspaces/([^/]+)', '/webspaces/<webspaceId>', Url_Template)
	| extend Url_Template = replace('/[sS]ites/([^/]+)', '/sites/<siteName>', Url_Template)
	| extend Url_Template = replace('/[sS]erver[fF]arms/([^/]+)', '/serverfarms/<serverfarmName>', Url_Template)
	| extend Url_Template = replace('/hybridconnection/([^/]+)', '/hybridconnection/<hybridConnectionName>', Url_Template)
	| extend Url_Template = replace('/notification/([^/]+)', '/notification/<notificationId>', Url_Template)
	| extend Url_Template = replace('/operations/([^/]+)', '/operations/<operationId>', Url_Template)
	| extend Url_Template = replace('/certificates/([^/]+)', '/certificates/<certificateName>', Url_Template)
	| extend Url_Template = replace('/workers/([^/]+)', '/workers/<workerName>', Url_Template)
	| extend Url_Template = replace('/FirstPartyApps/AntMDS/settings/([^/]+)', '/FirstPartyApps/AntMDS/settings/<settingName>', Url_Template)
	| extend Url_Template = replace('\\?.+$', '', Url_Template)
    | extend Url_Template = trim_end('/', Url_Template)";

private static string summarizeByTemplateQuerySegment = $@"| summarize Last_Called = max(PreciseTimeStamp), Call_Count = count() by Api_Client, Verb, Url_Template, Resource_Type";

private static IDictionary<string, string> clientToHelpReferenceMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    { ".NET SDK", @"https://docs.microsoft.com/en-us/dotnet/azure/"},
    { "Powershell", @"https://docs.microsoft.com/en-us/powershell/azure/get-started-azureps?view=azurermps-6.0.0"},
    { "Java", @"https://docs.microsoft.com/en-us/java/azure/"},
    { "Python", @"https://docs.microsoft.com/en-us/python/azure/?view=azure-python"},
    { "Node", @"https://docs.microsoft.com/en-us/javascript/azure/?view=azure-node-latest"},
    { "Rest API", @"https://docs.microsoft.com/en-us/rest/api/appservice/"},
    { "Azure Cli", @"https://docs.microsoft.com/en-us/cli/azure/?view=azure-cli-latest"},
};

private static void SanitizeTable(DataTable table){
    // Fix URL templates
    foreach(DataRow row in table.Rows)
    {
        string column = (string)row[2];
        column = column.Replace("<", "{");
        column = column.Replace(">", "}");
        column = column.Replace("[", string.Empty);
        column = column.Replace("]", string.Empty);
        row[2] = column;
    }

    // Fix column names
    foreach(DataColumn header in table.Columns)
    {
        header.ColumnName = header.ColumnName.Replace("_", " ");
    }
}

private static DataTable FilterTable(DataTable table, string resourceType){
    DataTable tableCopy = table.Copy();
    DataColumn resourceTypeColumn = (DataColumn)tableCopy.Columns["Resource Type"];
    
    for(int i = tableCopy.Rows.Count-1; i >= 0; i--)
    {
        DataRow row = tableCopy.Rows[i];
        if (!string.Equals((string)row["Resource Type"], resourceType))
            row.Delete();
    }

    tableCopy.Columns.Remove(resourceTypeColumn);
    return tableCopy;
}

private static string GetPerResourceUsageQuery(OperationContext<App> cxt, dynamic observerSite)
{
    var subscriptionId = cxt.Resource.SubscriptionId;    
    var resourceGroup = cxt.Resource.ResourceGroup;
    var siteName = (string)observerSite.name;
    var webspaceName = (string)observerSite.webspace.name;

    StringBuilder sb = new StringBuilder();
    sb.AppendLine(tableQuerySegment);
    sb.AppendLine($@"| where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)} and SubscriptionId == '{subscriptionId}'");
    sb.AppendLine(rdfeCallFilterQuerySegment);
    sb.AppendLine(webspaceNameExtractionQuerySegment);
    sb.AppendLine(siteWithSlotNameExtractionQuerySegment);
    sb.AppendLine(serverFarmNameExtractionQuerySegment);
    sb.AppendLine(certificateNameExtractionQuerySegment);
    sb.AppendLine(userAgentNameExtractionQuerySegment);
    sb.AppendLine(urlTemplateExtractionQuerySegment);
    sb.AppendLine(resourceTypeExtractionQuerySegment);
    sb.AppendLine($@"| where webspaceName == '{webspaceName}'");
    sb.AppendLine(summarizeByTemplateQuerySegment);

    return sb.ToString();
}

private static void AddInsightForResource(Response response, DataTable usages, string resourceType, string resourceName, string titlePrefix)
{
    DataTable perResourceUsages = FilterTable(usages, resourceType);
    if (perResourceUsages != null && perResourceUsages.Rows != null && perResourceUsages.Rows.Count > 0)
    {
        var data = new DiagnosticData()
        {
            Table = perResourceUsages,
            RenderingProperties = new Rendering(RenderingType.Table)
            {
                Title = string.Format("{0}: {1}", titlePrefix, resourceName)
            }
        };
        response.AddDynamicInsight(new DynamicInsight(
            InsightStatus.Critical, 
            string.Format("Service Management API calls found for {0} '{1}'", titlePrefix, resourceName),
            data,
            true));
    }
    else
    {
        response.AddInsight(new Insight(InsightStatus.Success, string.Format("No Service Management APIs called for {0} '{1}'", titlePrefix, resourceName)));
    }
}

private static void AddApiClientReferences(Response response, DataTable usages){
    // Assumes there is usages data
    HashSet<string> apiClients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach(DataRow row in usages.Rows)
    {
        string apiClient = (string)row[0];
        if (!apiClients.Contains(apiClient))
        {
            apiClients.Add(apiClient);
        }
    }

    DataTable datatable = new DataTable("ApiClientReferences");
    datatable.Columns.Add("Api Client", typeof(string));
    datatable.Columns.Add("Recommended Azure Resource Manager client", typeof(string));

    foreach(string apiClientName in apiClients){
        if (clientToHelpReferenceMap.ContainsKey(apiClientName)){
            datatable.Rows.Add(new object[] { apiClientName, string.Format("<a href='{0}'>{1}</a>", clientToHelpReferenceMap[apiClientName], apiClientName) });
        }
        else
        {
            datatable.Rows.Add(new object[] { apiClientName, "Unfortunately we do not have a corresponding Azure Resource manager client in the language/SDK. Please refer to <a href='@https://docs.microsoft.com/en-us/azure/index#pivot=sdkstools'>Azure SDK and tools</a> for other options." } );
        }
    }

    datatable.Rows.Add(new object[] { "Rest API", string.Format("You can also take a look at the raw API reference at <a href='{0}'>Rest API</a>",clientToHelpReferenceMap["Rest API"]) });

    var diagnosticData = new DiagnosticData()
    {
        Table = datatable,
        RenderingProperties = new Rendering(RenderingType.Table)
            {
                Title = "Api clients"
            }
    };

    response.AddDynamicInsight(new DynamicInsight(
            InsightStatus.Critical, 
            string.Format("We have determined that you are using the following Service Management API client(s). Please switch over to the recommended Azure Resource Manager client(s) instead."),
            diagnosticData,
            true));
}

[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows | PlatformType.Linux, StackType = StackType.All, InternalOnly = false)]
[Definition(Id = "AzureServiceManagementAPIUsage", 
Name = "Deprecating APIs", 
Description = "Determines if there have been any calls to soon to be deprecated APIs for the web app or associated resources.")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    var zeroInsightDetails = new Dictionary<string, string>();
    zeroInsightDetails.Add("Learn more about the announcement: ", @"<a href=""https://blogs.msdn.microsoft.com/appserviceteam/2018/03/12/deprecating-service-management-apis-support-for-azure-app-services"" target=""_blank"">Deprecating Service Management APIs support for Azure App Services</a>");
    var zeroInsight = new Insight(InsightStatus.Info, "Service Management APIs for Azure Web Apps will be deprecated June 30, 2018. This does not affect any other Azure Service. Please switch over any automation using clients identified below to the Azure Resource Management counterparts.", zeroInsightDetails);
    res.AddInsight(zeroInsight);

    var observerSite = (await dp.Observer.GetSite(cxt.Resource.Stamp.Name, cxt.Resource.Name))[0];
    var query = GetPerResourceUsageQuery(cxt, observerSite);

    var usages = await dp.Kusto.ExecuteQuery(query, cxt.Resource.Stamp.Name);

    if (usages == null || usages.Rows == null || usages.Rows.Count == 0)
    {
        res.AddInsight(new Insight(InsightStatus.Success, string.Format("No Service Management APIs called for web app '{0}'", observerSite.name)));
        res.AddInsight(new Insight(InsightStatus.Success, string.Format("No Service Management APIs called for app service plan '{0}'", observerSite.server_farm.server_farm_name)));
        res.AddInsight(new Insight(InsightStatus.Success, string.Format("No Service Management APIs called for webspace '{0}'", observerSite.webspace.name)));
        return res;
    }

    SanitizeTable(usages);
    try
    {
        AddApiClientReferences(res, usages);
        AddInsightForResource(res, usages, "site", (string)observerSite.name, "Web App");
        AddInsightForResource(res, usages, "serverfarm", (string)observerSite.server_farm.server_farm_name, "App Service Plan");
        AddInsightForResource(res, usages, "webspace", (string)observerSite.webspace.name, "Webspace");
    }
    catch(Exception e)
    {
        res.AddInsight(new Insight(InsightStatus.Critical, e.ToString()));
    }

    return res;
}