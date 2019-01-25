using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using Newtonsoft.Json;

public class WebResource
{
    public string providerName { get; set; }
    public string resourceType { get; set; }
    public string resourceName { get; set; }
    public string originatingResourceGroupName { get; set; }
    public string serverFarmName { get; set; }
    public string SKU { get; set; }
    public WebResource() { }

    public WebResource(string providerName,
                        string resourceType,
                        string resourceName)
    {

        this.providerName = providerName;
        this.resourceType = resourceType;
        this.resourceName = resourceName;
    }

}

public class MigrationRecord
{
    public string srcSubscription { get; set; }
    public string destSubscription { get; set; }
    public string srcResourceGroup { get; set; }
    public int nSrcRGResourceCount { get; set; }
    public int nUserSelectedResourceCount { get; set; }
    public int nDestRGResourceCount { get; set; }
    public string destResourceGroup { get; set; }
    public int scenarioEncountered { get; set; }
    public int numWebResources { get; set; }
    public bool bCrossSubscriptionMigration { get; set; }
    public bool bOriginatingResourceGroupInBadState { get; set; }
    public bool bSSLCertFound { get; set; }
    public DateTime dateTime { get; set; }
    public string correlationRequestId { get; set; }
    public string requestContent { get; set; }
    public List<WebResource> webResourceList { get; set; }
    public int totalSites { get; set; }
    public int totalServerfarms { get; set; }
    public int totalCertificates { get; set; }
    public int totalResources { get; set; }
    public bool bSSLCertFoundv2 { get; set; }
    public bool bSitesFound { get; set; }
    public bool bServerFarmFound { get; set; }
    public string sData { get; set; }
    public MigrationRecord() { }
}

public enum ResoureType { Sites, Serverfarms, Certificates };

private static string GetMoveOperationsQuery(OperationContext<App> cxt)
{
    string SubscriptionId = cxt.Resource.SubscriptionId;

    return
    $@"AntaresAdminSubscriptionAuditEvents
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}   
        | where SubscriptionId == '{SubscriptionId}' and OperationType == 'ValidateMoveResources'   
        | project ResourceGroupName, PreciseTimeStamp, OperationStatus, RequestContent, Details, SubscriptionId, Exception
        | extend SourceResourceGroup = ResourceGroupName
        | extend TargetResourceGroupURI = extract(""TargetResourceGroup\\>([^\\<]+)"", 1, RequestContent)
        | extend TargetSubscription = extract(""subscriptions/([^/<]+)"", 1, TargetResourceGroupURI)
        | extend TargetResourceGroup= extract(""resourceGroups/([^<]+)"", 1, TargetResourceGroupURI)
        | extend Resources = extractall(""providers/([^\\<]+)"", RequestContent) 
        | project PreciseTimeStamp, SourceResourceGroup, TargetSubscription, TargetResourceGroup, Resources, OperationStatus";
}

private static string GetFailedMoveOperationsCrossSubQuery(OperationContext<App> cxt)
{
    string SubscriptionId = cxt.Resource.SubscriptionId;

    return
    $@"AntaresAdminSubscriptionAuditEvents
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}   
        | where SubscriptionId == '{SubscriptionId}' and OperationType == 'ValidateMoveResources' and OperationStatus == 'Failed'  
        | project ResourceGroupName, PreciseTimeStamp, OperationStatus, RequestContent, Details, SubscriptionId
        | extend SourceResourceGroup = ResourceGroupName
        | extend TargetResourceGroupURI = extract(""TargetResourceGroup\\>([^\\<]+)"", 1, RequestContent)
        | extend TargetSubscription = extract(""subscriptions/([^/<]+)"", 1, TargetResourceGroupURI)
        | extend TargetResourceGroup= extract(""resourceGroups/([^<]+)"", 1, TargetResourceGroupURI)
        | where SubscriptionId != TargetSubscription
        | extend Resources = extractall(""providers/([^\\<]+)"", RequestContent) 
        | project PreciseTimeStamp, SourceResourceGroup, TargetSubscription, TargetResourceGroup, Resources, OperationStatus";
}

private static string GetFailedMoveOperationsSameSubQuery(OperationContext<App> cxt)
{
    string SubscriptionId = cxt.Resource.SubscriptionId;

    return
    $@"AntaresAdminSubscriptionAuditEvents
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}   
        | where SubscriptionId == '{SubscriptionId}' and OperationType == 'ValidateMoveResources' and OperationStatus == 'Failed'  
        | project ResourceGroupName, PreciseTimeStamp, OperationStatus, RequestContent, Details, SubscriptionId
        | extend SourceResourceGroup = ResourceGroupName
        | extend TargetResourceGroupURI = extract(""TargetResourceGroup\\>([^\\<]+)"", 1, RequestContent)
        | extend TargetSubscription = extract(""subscriptions/([^/<]+)"", 1, TargetResourceGroupURI)
        | extend TargetResourceGroup= extract(""resourceGroups/([^<]+)"", 1, TargetResourceGroupURI)
        | where SubscriptionId == TargetSubscription
        | extend Resources = extractall(""providers/([^\\<]+)"", RequestContent) 
        | project PreciseTimeStamp, SourceResourceGroup, TargetSubscription, TargetResourceGroup, Resources, OperationStatus";
}

private static string GetSuccessfulMoveOperationsQuery(OperationContext<App> cxt)
{
    string SubscriptionId = cxt.Resource.SubscriptionId;

    return
    $@"AntaresAdminSubscriptionAuditEvents
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}   
        | where SubscriptionId == '{SubscriptionId}' and OperationType == 'ValidateMoveResources' and OperationStatus == 'Success'  
        | project ResourceGroupName, PreciseTimeStamp, OperationStatus, RequestContent, Details, SubscriptionId
        | extend SourceResourceGroup = ResourceGroupName
        | extend TargetResourceGroupURI = extract(""TargetResourceGroup\\>([^\\<]+)"", 1, RequestContent)
        | extend TargetSubscription = extract(""subscriptions/([^/<]+)"", 1, TargetResourceGroupURI)
        | extend TargetResourceGroup= extract(""resourceGroups/([^<]+)"", 1, TargetResourceGroupURI)
        | extend Resources = extractall(""providers/([^\\<]+)"", RequestContent) 
        | project PreciseTimeStamp, SourceResourceGroup, TargetSubscription, TargetResourceGroup, Resources, OperationStatus";
}

private static string GetMoveOperationsQueryForAnalysis(OperationContext<App> cxt)
{
    string SubscriptionId = cxt.Resource.SubscriptionId;

    return
    $@"AntaresAdminSubscriptionAuditEvents
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}   
        | where SubscriptionId == '{SubscriptionId}' and OperationType == 'ValidateMoveResources' and OperationStatus == 'Failed' and  WebSystemName == 'WebSites'
        | extend errorDetails = extract(""Detail:([^\n]+)"", 1, Exception)
        | project RequestContent, PreciseTimeStamp, CorrelationRequestId, errorDetails";
}

async static Task<List<MigrationRecord>> StartAnalysis(DataTable tableResult, DataProviders dp, Response res)
{
    List<MigrationRecord> migrationRecords = new List<MigrationRecord>();

    int nRow = 0;

    foreach (DataRow row in tableResult.Rows)
    {
        foreach (DataColumn column in tableResult.Columns)
        {
            switch (column.Ordinal)
            {
                case 0:
                    string srcSubscription = "";
                    string srcResourceGroup = "";
                    string destSubscription = "";
                    string destResourceGroup = "";

                    List<WebResource> webResourceList = new List<WebResource>();

                    ParseXML(row[column].ToString(), ref srcSubscription,
                                                        ref srcResourceGroup,
                                                        ref destSubscription,
                                                        ref destResourceGroup,
                                                        ref webResourceList);

                    MigrationRecord migrationRecord = new MigrationRecord();
                    migrationRecord.srcSubscription = srcSubscription;
                    migrationRecord.destSubscription = destSubscription;
                    migrationRecord.srcResourceGroup = srcResourceGroup;
                    migrationRecord.destResourceGroup = destResourceGroup;

                    if (migrationRecord.srcSubscription != migrationRecord.destSubscription)
                    {
                        migrationRecord.bCrossSubscriptionMigration = true;
                    }
                    migrationRecord.scenarioEncountered = 0;

                    migrationRecord.numWebResources = webResourceList.Count;
                    migrationRecord.webResourceList = webResourceList;

                    migrationRecord.requestContent = row[column].ToString();
                    migrationRecords.Add(migrationRecord);

                    break;

                case 1:
                    migrationRecords[nRow].dateTime = DateTime.Parse(row[column].ToString());

                    break;

                case 2:
                    migrationRecords[nRow].correlationRequestId = row[column].ToString();

                    break;
                
                case 3:
                    migrationRecords[nRow].sData = row[column].ToString();
                    
                    break;

            }
        }

        var task = await AnalyzeRecord(migrationRecords[nRow], dp, res);

        nRow = nRow + 1;
    }

    return migrationRecords;

}

static void ParseXML(string sXML, ref string srcSubscription,
                                    ref string srcResourceGroup,
                                    ref string destSubscription,
                                    ref string destResourceGroup,
                                    ref List<WebResource> webResourceList)
{
    string xmlString = "<myxml>" + sXML + "</myxml>";
    xmlString = xmlString.Replace("a:string", "string");

    StringBuilder output = new StringBuilder();

    // Create an XmlReader
    using (XmlReader reader = XmlReader.Create(new StringReader(xmlString)))
    {
        bool bCsmMoveResourceEnvelope = false;
        bool bResourcesElement = false;
        bool bStringResourcesElement = false;
        string strResource;

        bool bTargetResourceGroup = false;
        string strTargetResourceGroup;

        XmlWriterSettings ws = new XmlWriterSettings();
        ws.Indent = true;
        using (XmlWriter writer = XmlWriter.Create(output, ws))
        {

            // Parse the file and display each of the nodes.
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        writer.WriteStartElement(reader.Name);

                        if (reader.Name == "CsmMoveResourceEnvelope")
                        {
                            bCsmMoveResourceEnvelope = true;
                        }

                        if (reader.Name == "Resources")
                        {
                            if (bCsmMoveResourceEnvelope == true)
                                bResourcesElement = true;
                        }

                        if (reader.Name == "string")
                        {
                            if (bResourcesElement == true)
                                bStringResourcesElement = true;
                        }

                        if (reader.Name == "TargetResourceGroup")
                        {
                            bTargetResourceGroup = true;
                        }


                        break;
                    case XmlNodeType.Text:

                        if (bStringResourcesElement == true)
                        {
                            strResource = reader.Value;
                            String[] substrings = strResource.Split('/');

                            if (substrings.Count() == 9)
                            {
                                srcSubscription = substrings[2];
                                srcResourceGroup = substrings[4];

                                WebResource webResource = new WebResource();

                                webResource.providerName = substrings[6];
                                webResource.resourceType = substrings[7];
                                webResource.resourceName = substrings[8];

                                webResourceList.Add(webResource);
                            }

                            bStringResourcesElement = false;
                        }

                        if (bTargetResourceGroup == true)
                        {
                            strTargetResourceGroup = reader.Value;
                            String[] substrings = strTargetResourceGroup.Split('/');
                            if (substrings.Count() == 5)
                            {
                                destSubscription = substrings[2];
                                destResourceGroup = substrings[4];
                            }

                            bTargetResourceGroup = false;
                        }

                        break;
                    case XmlNodeType.XmlDeclaration:
                    case XmlNodeType.ProcessingInstruction:
                        writer.WriteProcessingInstruction(reader.Name, reader.Value);
                        break;
                    case XmlNodeType.Comment:
                        writer.WriteComment(reader.Value);
                        break;
                    case XmlNodeType.EndElement:

                        if (reader.Name == "Resources")
                        {
                            bResourcesElement = false;
                        }

                        if (reader.Name == "CsmMoveResourceEnvelope")
                        {
                            bCsmMoveResourceEnvelope = false;
                        }

                        writer.WriteFullEndElement();
                        break;
                }
            }

        }

    }

}

public async static Task<MigrationRecord> BuildResourceGroupResourceList(string subscriptionid, string resourceGroupName, Response res, DataProviders dp)
{
    MigrationRecord migrationRecord = new MigrationRecord();
    List<WebResource> webResourcelist = new List<WebResource>();

    migrationRecord.srcSubscription = subscriptionid;
    migrationRecord.srcResourceGroup = resourceGroupName;

    dynamic siteList = null;
    dynamic serverfarmList = null;
    dynamic certsList = null;

    if (subscriptionid != null && resourceGroupName != null)
        if (subscriptionid.Length > 0 && resourceGroupName.Length > 0)
        {
            try
            {
                siteList = await dp.Observer.GetResource($"https://wawsobserver.azurewebsites.windows.net/subscriptions/{subscriptionid}/resourceGroups/{resourceGroupName}");
                //res.AddMarkdownView($"https://wawsobserver.azurewebsites.windows.net/subscriptions/{subscriptionid}/resourceGroups/{resourceGroupName}");
                
                string debug_trace = null;

                if (siteList != null)
                {
                    for (int i = 0; i < siteList.websites.Count; i++)
                    {
                        //JToken token = sitesArr[i];
                        WebResource webResource = new WebResource();

                        string strVal = (string)siteList.websites[i].name;
                        if (strVal != null)
                            if (strVal.Length > 0)
                            {

                                webResource.resourceName = strVal;
                                debug_trace = debug_trace + strVal + "<br>" ;
                                webResource.resourceType = "sites";
                                //res.AddMarkdownView(i+": "+strVal);
                                webResourcelist.Add(webResource);
                            }
                    }

                    if (siteList.websites.Count > 0)
                    {
                        migrationRecord.bSitesFound = true;
                        migrationRecord.totalSites = siteList.websites.Count;
                        //debug_trace = debug_trace + siteList.websites.Count + "<br>" ;

                    }
                }

                //res.AddMarkdownView(debug_trace);
                serverfarmList = await dp.Observer.GetResource($"https://wawsobserver.azurewebsites.windows.net/subscriptions/{subscriptionid}/resourceGroups/{resourceGroupName}");

                if (serverfarmList != null)
                {
                    for (int i = 0; i < serverfarmList.server_farms.Count; i++)
                    {
                        //JToken token = sitesArr[i];
                        WebResource webResource = new WebResource();

                        string strVal = (string)serverfarmList.server_farms[i].server_farm_name;
                        if (strVal != null)
                            if (strVal.Length > 0)
                            {

                                webResource.resourceName = strVal;
                                webResource.resourceType = "serverFarms";
                                //res.AddMarkdownView(i+": "+strVal);
                                webResourcelist.Add(webResource);
                            }
                    }

                    if (serverfarmList.server_farms.Count > 0)
                    {
                        migrationRecord.bServerFarmFound = true;
                        migrationRecord.totalServerfarms = serverfarmList.server_farms.Count;
                    }
                }                

                certsList = await dp.Observer.GetResource($"https://wawsobserver.azurewebsites.windows.net/subscriptions/{subscriptionid}/certificates");

                if (certsList != null)
                {
                    for (int i = 0; i < certsList.Count; i++)
                    {
                        //JToken token = sitesArr[i];
                        WebResource webResource = new WebResource();

                        string sResourceGroupName = (string)certsList[i].resource_group_name;
                        string strVal = (string)certsList[i].certificate_name;
                        
                        if (sResourceGroupName.ToUpper() == resourceGroupName.ToUpper())
                        {
                            if (strVal != null)
                                if (strVal.Length > 0)
                                {

                                    webResource.resourceName = strVal;
                                    webResource.resourceType = "certificates";

                                    webResourcelist.Add(webResource);
                                }
                        }
                    }


                    if (certsList.Count > 0)
                    {
                        migrationRecord.bSSLCertFoundv2 = true;
                        migrationRecord.totalCertificates = certsList.Count;
                    }
                }                

            }
            catch (Exception ex)
            {
                res.AddMarkdownView("BuildResourceGroupResourceList - " + ex.Message);
            }
        }    

    migrationRecord.totalResources = migrationRecord.totalSites + migrationRecord.totalServerfarms + migrationRecord.totalCertificates;

    migrationRecord.webResourceList = webResourcelist;

    return migrationRecord;
}

public static int CalculateNumberOfResources(MigrationRecord migrationRecord)
{
    //bool bRetVal = false;
    List<WebResource> webResourcelist = migrationRecord.webResourceList;
    int nTotal = 0;
    int nSites = 0;
    int nServerfarm = 0;
    int nCerts = 0;

    foreach (WebResource webResource in webResourcelist)
    {
        if (webResource.resourceType == "sites")
            nSites = nSites + 1;
        if (webResource.resourceType == "serverFarms")
            nServerfarm = nServerfarm + 1;
        if (webResource.resourceType == "certificates")
            nCerts = nCerts + 1;
    }

    migrationRecord.totalSites = nSites;
    migrationRecord.totalServerfarms = nServerfarm;
    migrationRecord.totalCertificates = nCerts;

    nTotal = nSites + nServerfarm + nCerts;

    return nTotal;
}

private async static Task<dynamic> WawsObserver_GetSite(string url, DataProviders dp, Response res)
{
    var sites = await dp.Observer.GetResource(url);
    return sites[0];
}

public async static Task<string> GetOriginatingResourceGroup(string subscriptionid, string resourceName, string resourceType, DataProviders dp, Response res)
{
    string strORGName = "";

    if (subscriptionid.Length > 0 && resourceName.Length > 0 && resourceType.Length > 0)
    {
        if (resourceType == "sites")
        {
            try
            {
                var siteInfo = await WawsObserver_GetSite($"https://wawsobserver.azurewebsites.windows.net/sites/{resourceName}", dp, res);
                var strSiteWSName = (string)siteInfo.webspace.name;
                //res.AddEmail("site_webspace_name:" + strSiteWSName);

                if (strSiteWSName.Length > 0)
                {
                    try
                    {
                        strORGName = await dp.Observer.GetWebspaceResourceGroupName(subscriptionid, strSiteWSName);
                        //res.AddEmail(strORGName);
                    }
                    catch (Exception e)
                    {
                        res.AddMarkdownView("GetOriginatingResourceGroup->GetWebspaceResourceGroupName: " + e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                res.AddMarkdownView("GetOriginatingResourceGroup->WawsObserver_GetSite: " + e.Message);
            }
        }
        else if (resourceType == "serverFarms")
        {
            try
            {
                var strSFWSName = await dp.Observer.GetServerFarmWebspaceName(subscriptionid, resourceName);
                strSFWSName = strSFWSName.Replace("\"", "");

                //res.AddEmail("serverfarm_webspace_name:" + strSFWSName);

                if (strSFWSName.Length > 0)
                {
                    try
                    {
                        strORGName = await dp.Observer.GetWebspaceResourceGroupName(subscriptionid, strSFWSName);
                        //es.AddEmail(strORGName);
                    }
                    catch (Exception e)
                    {
                        res.AddMarkdownView("GetOriginatingResourceGroup->WawsObserver_GetSite: " + e.Message);
                    }
                }

            }
            catch (Exception e)
            {
                res.AddMarkdownView("GetOriginatingResourceGroup->WawsObserver_GetSite: " + e.Message);
            }

        }
    }

    return strORGName.Replace("\"", "");
}

private async static Task<int> PopulateOriginatingResourceGroup(MigrationRecord migrationRecord, DataProviders dp, Response res)
{
    List<WebResource> webResourcelist = migrationRecord.webResourceList;

    foreach (WebResource webResource in webResourcelist)
    {
        string orgName = await GetOriginatingResourceGroup(migrationRecord.srcSubscription, webResource.resourceName, webResource.resourceType, dp, res);

        if (orgName != null)
        {
            if (orgName.Length > 0)
            {
                webResource.originatingResourceGroupName = orgName;
            }
        }
    }

    return 0;
}

private async static Task<int> AnalyzeRecord(MigrationRecord migrationRecord, DataProviders dp, Response res)
{
    //Update record with originating resource group name
    var task = await PopulateOriginatingResourceGroup(migrationRecord, dp, res);

    //Update record with serverfarm + SKU for the site   
    //PopulateServerFarmInfo(migrationRecord);

    MigrationRecord tmpMigrationRecord_src = await BuildResourceGroupResourceList(migrationRecord.srcSubscription, migrationRecord.srcResourceGroup, res, dp);

    MigrationRecord tmpMigrationRecord_dest = await BuildResourceGroupResourceList(migrationRecord.destSubscription, migrationRecord.destResourceGroup, res, dp);

    int nTotalResources = CalculateNumberOfResources(migrationRecord);

    //BUGBUG: 
    /*
    PopulateSitesForServerFarm(migrationRecord);
    */

    //TODO: More scenarios: 
    // Check if we have imported cert under the SrcRG
    // Check if we have DestRG is empty   

    //Scenario #1 - While performing cross subscription migration user tried to move only the website(s) 
    //              Solution: Move app service plan too.
    //              Limitation: Currently support only website(s) or app service plan(s)
    //              Will enable mixed resources scenario after getting the API to retrive app service plan information
    if (IsOnlyWebsiteType(migrationRecord.webResourceList) == true)
    {
        migrationRecord.scenarioEncountered = 1;
    }

    //Scenario #2 - While performing cross subscription migration user tried to move only the serverfarm(s) 
    //              Solution: Move app service plan too.
    //              Limitation: Currently support only website(s) or app service plan(s)
    //              Will enable mixed resources scenario after getting the API to retrive app service plan information
    if (IsOnlyServerFarmType(migrationRecord.webResourceList) == true)
    {
        migrationRecord.scenarioEncountered = 2;
    }

    //Scenario #3 - Check if all the resources are in the right state for a move.
    //Check if SrcRG and ORG are same, if no then flag
    //Test sub: 5edddfe6-80ac-4716-8b83-6f668125582e
    if (IsBadResourceState(migrationRecord, res) == true)
    {
        //res.AddEmail("IsBadResourceState(migrationRecord) == true");
        migrationRecord.bOriginatingResourceGroupInBadState = true;
        if (migrationRecord.scenarioEncountered == 0)
        {
            //res.AddEmail("migrationRecord.scenarioEncountered = 3");
            migrationRecord.scenarioEncountered = 3;
        }
    }

    //TODO: Tried to move imported cert .. : Test sub: ef90e930-9d7f-4a60-8a99-748e0eea69de
    //Scenario #4 - Imported cert?
    // Test sub: ef90e930-9d7f-4a60-8a99-748e0eea69de
    //06/01/2018: We support cross-subscription cert move now. 
    /*
    if (SSLCertFound(migrationRecord) == true)
    {
        migrationRecord.bSSLCertFound = true;
        if (migrationRecord.scenarioEncountered == 0)
        {
            migrationRecord.scenarioEncountered = 4;
        }
    }
    */
    //--------

    int nSrcRgTotalResources = tmpMigrationRecord_src.totalSites + tmpMigrationRecord_src.totalServerfarms + tmpMigrationRecord_src.totalCertificates;

    //TODO: If number of Microsoft.Web item is more than the source resource group, then we should show message to the user.  
    migrationRecord.nSrcRGResourceCount = nSrcRgTotalResources;
    migrationRecord.nUserSelectedResourceCount = nTotalResources;


    //Found an interesting scenario... 
    // Customer selected resources                  4 sites + 1 serverfarm + 0 cert = 5 resources
    // Current state of the same resource group     4 sites + 0 serverfarm + 1 cert = 5 resources
    // So , basically bypassed the logic below, and failed to show message related to cert.
    // Need to show cert repro differently:
    // 1. Check user list for cert
    // if not found  
    //      1.1. Check srcRG for certs
    //      if found update main list of resources
    // or
    // Always alert for cert issue:
    // Report issue:
    // Src sub has cert
    // Report solution:
    // Steps to mitigate and list of cert from src RG, pull the data then and there

    if (nSrcRgTotalResources > nTotalResources)
    {
        migrationRecord.bSSLCertFound = tmpMigrationRecord_src.bSSLCertFoundv2;

        if (migrationRecord.scenarioEncountered == 0)
        {
            if (migrationRecord.srcResourceGroup != migrationRecord.destResourceGroup)
            {
                migrationRecord.scenarioEncountered = 5;
            }
        }
    }

    int nDestRgTotalResources = tmpMigrationRecord_dest.totalSites + tmpMigrationRecord_dest.totalServerfarms + tmpMigrationRecord_dest.totalCertificates;
/*
    string debug_info = "SubscriptionId:" + migrationRecord.destSubscription + "<br>" + "destResourceGroup:" + migrationRecord.destResourceGroup + "<br>" + "Total resources:" + nDestRgTotalResources + "<br>";
    res.AddMarkdownView(debug_info);
*/
    if (nDestRgTotalResources > 0)
    {
        migrationRecord.nDestRGResourceCount = nDestRgTotalResources;

        if (migrationRecord.scenarioEncountered == 0)
        {
            if (migrationRecord.srcResourceGroup != migrationRecord.destResourceGroup)
            {
                migrationRecord.scenarioEncountered = 6;
            }
        }

    }

    //Failed to match with any known pattern!
    if (migrationRecord.scenarioEncountered == 0)
    {
        if (migrationRecord != null)
        {
            //res.AddMarkdownView("Failed to match a known issue!");
        }
    }

    return 0;
}

public static bool SSLCertFound(MigrationRecord migrationRecord)
{
    bool bRetVal = false;
    List<WebResource> webResourcelist = migrationRecord.webResourceList;

    foreach (WebResource webResource in webResourcelist)
    {
        if (webResource.resourceType == "certificates")
            bRetVal = true;
    }

    return bRetVal;
}

public static bool IsBadResourceState(MigrationRecord migrationRecord, Response res)
{
    bool bRetVal = false;
    List<WebResource> webResourcelist = migrationRecord.webResourceList;

    foreach (WebResource webResource in webResourcelist)
    {
        if (webResource.originatingResourceGroupName != null)
        {
            if (webResource.originatingResourceGroupName.Length > 0)
            {
                if (webResource.originatingResourceGroupName.ToLower() != migrationRecord.srcResourceGroup.ToLower())
                {
                    //res.AddEmail("originatingResourceGroupName:" + webResource.originatingResourceGroupName);
                    //res.AddEmail("srcResourceGroup:" +migrationRecord.srcResourceGroup);
                    bRetVal = true;
                }
            }
        }
    }

    return bRetVal;
}

public static bool IsOnlyWebsiteType(List<WebResource> webResourcelist)
{
    bool bRetVal = true;
    foreach (WebResource webResource in webResourcelist)
    {
        if (webResource.resourceType != "sites")
        {
            return false;
        }
    }

    return bRetVal;
}

public static bool IsOnlyServerFarmType(List<WebResource> webResourcelist)
{
    bool bRetVal = true;
    foreach (WebResource webResource in webResourcelist)
    {
        if (webResource.resourceType != "serverFarms")
        {
            return false;
        }
    }

    return bRetVal;
}
private async static Task <string> Report(List<MigrationRecord> migrationRecords, Response res, DataProviders dp)
{
    string strProblem = "";
    string strSolution = "";
    
    foreach (MigrationRecord migrationRecord in migrationRecords)
    {
/*
        if (migrationRecord.scenarioEncountered == 1)
        {
            //res.AddEmail("DEBUG:migrationRecord.scenarioEncountered == 1"); 
            strProblem = ReportProblem(migrationRecord.scenarioEncountered, migrationRecord);
            strSolution = await ShareSolution(migrationRecord.scenarioEncountered, migrationRecord, dp, res);
        }

        if (migrationRecord.scenarioEncountered == 2)
        {
            //res.AddEmail("DEBUG:migrationRecord.scenarioEncountered == 2"); 
            strProblem = ReportProblem(migrationRecord.scenarioEncountered, migrationRecord);
            strSolution = await ShareSolution(migrationRecord.scenarioEncountered, migrationRecord, dp, res);
        }

        if (migrationRecord.scenarioEncountered == 3)
        {
            //res.AddEmail("DEBUG:migrationRecord.scenarioEncountered == 3"); 
            strProblem = ReportProblem(migrationRecord.scenarioEncountered, migrationRecord);
            strSolution = await ShareSolution(migrationRecord.scenarioEncountered, migrationRecord, dp, res);
        }

        if (migrationRecord.scenarioEncountered == 4)
        {
            //res.AddEmail("DEBUG:migrationRecord.scenarioEncountered == 4"); 
            strProblem = ReportProblem(migrationRecord.scenarioEncountered, migrationRecord);
            strSolution = await ShareSolution(migrationRecord.scenarioEncountered, migrationRecord, dp, res);
        }

        if (migrationRecord.scenarioEncountered == 5)
        {
            //res.AddEmail("DEBUG:migrationRecord.scenarioEncountered == 5"); 
            strProblem = ReportProblem(migrationRecord.scenarioEncountered, migrationRecord);
            strSolution = await ShareSolution(migrationRecord.scenarioEncountered, migrationRecord, dp, res);
        }
*/
        if (migrationRecord.scenarioEncountered >= 0)
        {
            strProblem = ReportProblem(migrationRecord.scenarioEncountered, migrationRecord);
            strSolution = await ShareSolution(migrationRecord.scenarioEncountered, migrationRecord, dp, res);

            Response criticalResponse = new Response();
            var body = new Dictionary<string, string>();
            if (migrationRecord.scenarioEncountered == 0)
            {
                body["Details"] = migrationRecord.sData;
            } else
            {
                body["Problem"] = strProblem;
                body["Solution"] = strSolution;
            }
            criticalResponse.AddInsight(InsightStatus.Critical, "Cross-subscription move operation encountered a known limitation", body);
            res.Dataset.AddRange(criticalResponse.Dataset);
        }

    }

    return "";
}

private static string ReportProblem(int scenarioEncountered, MigrationRecord migrationRecord)
{
    string strProblem = "";

    switch (scenarioEncountered)
    {
        case 1:

            strProblem = "Resource migration operation from <b><i>" + migrationRecord.srcSubscription + "</i></b> to <b><i>" +
                                                                migrationRecord.destSubscription + "</i></b> failed at around <b><i>" +
                                                                migrationRecord.dateTime +
                                                            "</i></b><br>becasue you tried to move the following site(s) without their app service plan(s).<br>";

            strProblem = strProblem + "<ol>";

            foreach (WebResource webResource in migrationRecord.webResourceList)
            {
                strProblem = strProblem + "<li>" + webResource.resourceName + " (" + webResource.resourceName + ".azurewebsites.net" + ")</li>";
            }

            strProblem = strProblem + "</ol>";

            if (migrationRecord.nSrcRGResourceCount > migrationRecord.nUserSelectedResourceCount)
            {
                if (migrationRecord.bCrossSubscriptionMigration)
                {
                    strProblem = strProblem + "<li>" + "You did not select all the Microsoft.Web resources from the source ResourceGroup <b>" + migrationRecord.srcResourceGroup + "</b> for migration.</li>";     
                }

            }

            if (migrationRecord.nDestRGResourceCount > 0)
            {
                strProblem = strProblem + "<li>" + "Destination ResourceGroup <b>" + migrationRecord.destResourceGroup + "</b> contains Microsoft.Web resource(s).</li>";
            }

            if (migrationRecord.bOriginatingResourceGroupInBadState == true)
            {
                strProblem = strProblem + "<li>Following site(s)/app service plan(s) are not in their originating resource group.</li>";
                strProblem = strProblem + "<ol>";
                foreach (WebResource webResource in migrationRecord.webResourceList)
                {
                    if (webResource.originatingResourceGroupName != migrationRecord.srcResourceGroup)
                    {

                        if (webResource.resourceType == "sites")
                        {
                            strProblem = strProblem + "<li>" + webResource.resourceName + " (" + webResource.resourceName + ".azurewebsites.net" + ")</li>";
                        }

                        if (webResource.resourceType == "serverFarms")
                        {
                            strProblem = strProblem + "<li>" + webResource.resourceName + "</li>";
                        }

                    }
                }
                strProblem = strProblem + "</ol>";
            }

            break;

        case 2:
            strProblem = "Resource migration operation from <b><i>" + migrationRecord.srcSubscription + "</i></b> to <b><i>" +
                                                                migrationRecord.destSubscription + "</i></b> failed at around <b><i>" +
                                                                migrationRecord.dateTime +
                                                            "</i></b><br>becasue you had tried to move the following App Service Plan(s) without their assigned Site(s).<br>";

            strProblem = strProblem + "<ol>";
            foreach (WebResource webResource in migrationRecord.webResourceList)
            {
                strProblem = strProblem + "<li>" + webResource.resourceName + "</li>";
            }
            strProblem = strProblem + "</ol>";

            if (migrationRecord.nSrcRGResourceCount > migrationRecord.nUserSelectedResourceCount)
            {
                if (migrationRecord.bCrossSubscriptionMigration)
                {
                    strProblem = strProblem + "<li>" + "You did not select all the Microsoft.Web resources from the source ResourceGroup <b>" + migrationRecord.srcResourceGroup + "</b> for migration.</li>";     
                }

            }

            if (migrationRecord.nDestRGResourceCount > 0)
            {
                strProblem = strProblem + "<li>" + "Destination ResourceGroup <b>" + migrationRecord.destResourceGroup + "</b> contains Microsoft.Web resource(s).</li>";
            }

            if (migrationRecord.bOriginatingResourceGroupInBadState == true)
            {
                strProblem = strProblem + "<li>Following site(s)/app service plan(s) are not in their originating resource group.</li>";
                strProblem = strProblem + "<ol>";
                foreach (WebResource webResource in migrationRecord.webResourceList)
                {
                    if (webResource.originatingResourceGroupName != migrationRecord.srcResourceGroup)
                    {

                        if (webResource.resourceType == "sites")
                        {
                            strProblem = strProblem + "<li>" + webResource.resourceName + " (" + webResource.resourceName + ".azurewebsites.net" + ")</li>";
                        }

                        if (webResource.resourceType == "serverFarms")
                        {
                            strProblem = strProblem + "<li>" + webResource.resourceName + "</li>";
                        }

                    }
                }
                strProblem = strProblem + "</ol>";
            }

            break;

        case 3:

            strProblem = "Resource migration operation from <b><i>" + migrationRecord.srcSubscription + "</i></b> to <b><i>" +
                                                                migrationRecord.destSubscription + "</i></b> failed at around <b><i>" +
                                                                migrationRecord.dateTime +
                                                            "</i></b><br>becasue following site(s)/app service plan(s) are not in their originating resource group.<br>";

            strProblem = strProblem + "<ol>";

            if (migrationRecord.bOriginatingResourceGroupInBadState == true)
            {
                foreach (WebResource webResource in migrationRecord.webResourceList)
                {
                    if (webResource.originatingResourceGroupName != migrationRecord.srcResourceGroup)
                    {
                        if (webResource.resourceType == "sites")
                        {
                            strProblem = strProblem + "<li>" + webResource.resourceName + " (" + webResource.resourceName + ".azurewebsites.net" + ")</li>";
                        }

                        if (webResource.resourceType == "serverFarms")
                        {
                            strProblem = strProblem + "<li>" + webResource.resourceName + "</li>";
                        }
                    }
                }
            }

            strProblem = strProblem + "</ol>";

            if (migrationRecord.nSrcRGResourceCount > migrationRecord.nUserSelectedResourceCount)
            {
                if (migrationRecord.bCrossSubscriptionMigration)
                {
                    strProblem = strProblem + "<li>" + "You did not select all the Microsoft.Web resources from the source ResourceGroup <b>" + migrationRecord.srcResourceGroup + "</b> for migration.</li>";     
                }

            }

            if (migrationRecord.nDestRGResourceCount > 0)
            {
                strProblem = strProblem + "<li>" + "Destination ResourceGroup <b>" + migrationRecord.destResourceGroup + "</b> contains Microsoft.Web resource(s).</li>";
            }
            
            break;

        case 4:
            /*
            if (bOnlyRG == false)
            {
                strProblem = "Resource migration operation from " + migrationRecord.srcSubscription + " to " +
                                                                    migrationRecord.destSubscription + " failed at around " +
                                                                    migrationRecord.dateTime +
                                                                "\nbecasue you tried to move SSL certificate(s).\n";
            }
            else
            {
                strProblem = "SSL certificate(s) found.\n";
            }

            Console.WriteLine(strProblem);

            if (migrationRecord.nSrcRGResourceCount > migrationRecord.nUserSelectedResourceCount)
            {
                if (migrationRecord.bCrossSubscriptionMigration)
                    Console.WriteLine("You did not select all the Microsoft.Web resources from the source ResourceGroup " + migrationRecord.srcResourceGroup + " for migration.\n");
            }

            if (migrationRecord.nDestRGResourceCount > 0)
            {
                Console.WriteLine();
                Console.Write("Destination ResourceGroup " + migrationRecord.destResourceGroup + " contains Microsoft.Web resource(s).\n\n");
            }
            */
            break;

        case 5:
            strProblem = "Resource migration operation from <b><i>" + migrationRecord.srcSubscription + "</i></b> to <b><i>" +
                                                                migrationRecord.destSubscription + "</i></b> failed at around <b><i>" +
                                                                migrationRecord.dateTime +
                                                            "</i></b><br>becasue you did not select all the resources from the source ResourceGroup <b>" + migrationRecord.srcResourceGroup + "</b><br>";
            
            if (migrationRecord.nDestRGResourceCount > 0)
            {
                strProblem = strProblem + "<li>" + "Destination ResourceGroup <b>" + migrationRecord.destResourceGroup + "</b> contains Microsoft.Web resource(s).</li>";
            }
            
            break;

        case 6:
            if (migrationRecord.nDestRGResourceCount > 0)
            {
                strProblem = "Resource migration operation from <b><i>" + migrationRecord.srcSubscription + "</i></b> to <b><i>" +
                                                                migrationRecord.destSubscription + "</i></b> failed at around <b><i>" +
                                                                migrationRecord.dateTime +
                                                            "</i></b><br>becasue destination ResourceGroup <b>" + migrationRecord.destResourceGroup + "</b> contains Microsoft.Web resource(s).<br>";
            }

            break;


    }

    return strProblem;
}

private static async Task <string> ShareSolution(int scenarioEncountered, MigrationRecord migrationRecord, DataProviders dp, Response res)
{
    string srcRG = migrationRecord.srcResourceGroup;

    string strSolution = "Recommended steps to mitigate the problem: <br>";
    strSolution = strSolution + "<ul style=\"list-style-type:square\">";

    switch (scenarioEncountered)
    {
        case 1:
/*
            foreach (WebResource webResource in migrationRecord.webResourceList)
            {
                if (webResource.originatingResourceGroupName != null && webResource.originatingResourceGroupName.Length > 0)
                {
                    if (webResource.originatingResourceGroupName.ToLower() != migrationRecord.srcResourceGroup.ToLower())
                    {
                        if (webResource.resourceType == "sites")
                        {
                            strSolution = strSolution + "<li>" + "You had migrated site " + "\"" + webResource.resourceName + "\"" + " from " + "\"" + webResource.originatingResourceGroupName + "\"" + " resource group to " + "\"" + srcRG + "\"" + " resource group.<br>";
                            strSolution = strSolution + " Please move this site back to " + "\"" + webResource.originatingResourceGroupName + "\"" + " resource group to enable it for new migration operation.</li>";
                        }

                        if (webResource.resourceType == "serverFarms")
                        {
                            strSolution = strSolution + "<li>" + "You had migrated App Service Plan " + "\"" + webResource.resourceName + "\"" + " from " + "\"" + webResource.originatingResourceGroupName + "\"" + " resource group to " + "\"" + srcRG + "\"" + " resource group.<br>";
                            strSolution = strSolution + " Please move this App Service Plan back to " + "\"" + webResource.originatingResourceGroupName + "\"" + " resource group to enable it for new migration operation.</li>";
                        }
                    }
                }
            }
*/
            strSolution = strSolution + "<li>" + "You must move site(s) with their <b>App Service Plan</b>(s) when performing cross subscription migration.</li>";

            if (migrationRecord.nDestRGResourceCount > 0)
            {
               strSolution = strSolution + "<li>Please make sure destination ResourceGroup <b>" + migrationRecord.destResourceGroup + "</b> doesn't have any Microsoft.Web resources before the move operation. Currently, it has <b>" + migrationRecord.nDestRGResourceCount + "</b> Microsoft.Web resources in it.</li>";
            }

            if (migrationRecord.bCrossSubscriptionMigration)
            {
                strSolution = strSolution + "<li>Please select all the Microsoft.Web resources from <b>" + migrationRecord.srcResourceGroup + "</b> resource group for cross subscription migration. Please see the list below:<br>";

                string srcSubscription1 = migrationRecord.srcSubscription.Replace(" ", "");
                string resourceGroupName1 = migrationRecord.srcResourceGroup;
                MigrationRecord mr1 = null;

                if (resourceGroupName1 != null && resourceGroupName1.Length > 0)
                    mr1 = await BuildResourceGroupResourceList(srcSubscription1, resourceGroupName1, res, dp);

                var task = await PopulateOriginatingResourceGroup(mr1, dp, res);

                strSolution = strSolution + DisplayRgRecord(mr1);
            }

            break;

        case 2:
/*
            foreach (WebResource webResource in migrationRecord.webResourceList)
            {
                if (webResource.originatingResourceGroupName != null && webResource.originatingResourceGroupName.Length > 0)
                {
                    if (webResource.originatingResourceGroupName.ToLower() != migrationRecord.srcResourceGroup.ToLower())
                    {
                        if (webResource.resourceType == "sites")
                        {
                            strSolution = strSolution + "<li>" + "You had migrated site " + "\"" + webResource.resourceName + "\"" + " from " + "\"" + webResource.originatingResourceGroupName + "\"" + " resource group to " + "\"" + srcRG + "\"" + " resource group.<br>";
                            strSolution = strSolution + " Please move this site back to " + "\"" + webResource.originatingResourceGroupName + "\"" + " resource group to enable it for new migration operation.</li>";
                        }

                        if (webResource.resourceType == "serverFarms")
                        {
                            strSolution = strSolution + "<li>" + "You had migrated App Service Plan " + "\"" + webResource.resourceName + "\"" + " from " + "\"" + webResource.originatingResourceGroupName + "\"" + " resource group to " + "\"" + srcRG + "\"" + " resource group.<br>";
                            strSolution = strSolution + " Please move this App Service Plan back to " + "\"" + webResource.originatingResourceGroupName + "\"" + " resource group to enable it for new migration operation.</li>";
                        }
                    }
                }
            }
*/
            strSolution = strSolution + "<li>" + "You cannot move sites without their App Service Plans, and vice versa when performing cross subscription migration.</li>";

            if (migrationRecord.nDestRGResourceCount > 0)
            {
               strSolution = strSolution + "<li>Please make sure destination resource group <b>" + migrationRecord.destResourceGroup + "</b> doesn't have any Microsoft.Web resources before the move operation. Currently, it has <b>" + migrationRecord.nDestRGResourceCount + "</b> Microsoft.Web resources in it.</li>";
            }

            if (migrationRecord.bCrossSubscriptionMigration)
            {
                strSolution = strSolution + "<li>Please select all the Microsoft.Web resources from <b>" + migrationRecord.srcResourceGroup + "</b> resource group for cross subscription migration. Please see the list below:<br>";

                string srcSubscription1 = migrationRecord.srcSubscription.Replace(" ", "");
                string resourceGroupName1 = migrationRecord.srcResourceGroup;
                MigrationRecord mr1 = null;

                if (resourceGroupName1 != null && resourceGroupName1.Length > 0)
                    mr1 = await BuildResourceGroupResourceList(srcSubscription1, resourceGroupName1, res, dp);

                var task = await PopulateOriginatingResourceGroup(mr1, dp, res);

                strSolution = strSolution + DisplayRgRecord(mr1);
            }

            break;

            case 3:
/*
            foreach (WebResource webResource in migrationRecord.webResourceList)
            {
                if (webResource.originatingResourceGroupName != null && webResource.originatingResourceGroupName.Length > 0)
                {
                    if (webResource.originatingResourceGroupName.ToLower() != migrationRecord.srcResourceGroup.ToLower())
                    {
                        if (webResource.resourceType == "sites")
                        {
                            strSolution = strSolution + "<li>" + "You had migrated site " + "\"" + webResource.resourceName + "\"" + " from " + "\"" + webResource.originatingResourceGroupName + "\"" + " resource group to " + "\"" + srcRG + "\"" + " resource group.<br>";
                            strSolution = strSolution + " Please move this site back to " + "\"" + webResource.originatingResourceGroupName + "\"" + " resource group to enable it for new migration operation.</li>";
                        }

                        if (webResource.resourceType == "serverFarms")
                        {
                            strSolution = strSolution + "<li>" + "You had migrated App Service Plan " + "\"" + webResource.resourceName + "\"" + " from " + "\"" + webResource.originatingResourceGroupName + "\"" + " resource group to " + "\"" + srcRG + "\"" + " resource group.<br>";
                            strSolution = strSolution + " Please move this App Service Plan back to " + "\"" + webResource.originatingResourceGroupName + "\"" + " resource group to enable it for new migration operation.</li>";
                        }
                    }
                }
            }
*/
            if (migrationRecord.nDestRGResourceCount > 0)
            {
               strSolution = strSolution + "<li>Please make sure destination resource group <b>" + migrationRecord.destResourceGroup + "</b> doesn't have any Microsoft.Web resources before the move operation. Currently, it has <b>" + migrationRecord.nDestRGResourceCount + "</b> Microsoft.Web resources in it.</li>";
            }

            if (migrationRecord.bCrossSubscriptionMigration)
            {
                strSolution = strSolution + "<li>Please select all the Microsoft.Web resources from <b>" + migrationRecord.srcResourceGroup + "</b> resource group for cross subscription migration. Please see the list below:<br>";

                string srcSubscription1 = migrationRecord.srcSubscription.Replace(" ", "");
                string resourceGroupName1 = migrationRecord.srcResourceGroup;
                MigrationRecord mr1 = null;

                if (resourceGroupName1 != null && resourceGroupName1.Length > 0)
                    mr1 = await BuildResourceGroupResourceList(srcSubscription1, resourceGroupName1, res, dp);

                var task = await PopulateOriginatingResourceGroup(mr1, dp, res);

                strSolution = strSolution + DisplayRgRecord(mr1);
            }

            break;

            case 5:

            //if (migrationRecord.nSrcRGResourceCount > migrationRecord.nUserSelectedResourceCount)
            //{
            if (migrationRecord.bCrossSubscriptionMigration)
            {
                strSolution = strSolution + "<li>Please select all the Microsoft.Web resources from <b>" + migrationRecord.srcResourceGroup + "</b> resource group for cross subscription migration. Please see the list below:<br>";

                string srcSubscription1 = migrationRecord.srcSubscription.Replace(" ", "");
                string resourceGroupName1 = migrationRecord.srcResourceGroup;
                MigrationRecord mr1 = null;

                if (resourceGroupName1 != null && resourceGroupName1.Length > 0)
                    mr1 = await BuildResourceGroupResourceList(srcSubscription1, resourceGroupName1, res, dp);

                var task = await PopulateOriginatingResourceGroup(mr1, dp, res);

                strSolution = strSolution + DisplayRgRecord(mr1);
                
            }
            //}
            
            if (migrationRecord.nDestRGResourceCount > 0)
            {
               strSolution = strSolution + "<li>Please make sure destination resource group <b>" + migrationRecord.destResourceGroup + "</b> doesn't have any Microsoft.Web resources before the move operation. Currently, it has <b>" + migrationRecord.nDestRGResourceCount + "</b> Microsoft.Web resources in it.</li>";
            }

            break;

            case 6:

            if (migrationRecord.nDestRGResourceCount > 0)
            {
               strSolution = strSolution + "<li>Please make sure destination resource group <b>" + migrationRecord.destResourceGroup + "</b> doesn't have any Microsoft.Web resources before the move operation. Currently, it has <b>" + migrationRecord.nDestRGResourceCount + "</b> Microsoft.Web resources in it.</li>";
            }

            if (migrationRecord.bCrossSubscriptionMigration)
            {
                strSolution = strSolution + "<li>Please select all the Microsoft.Web resources from <b>" + migrationRecord.srcResourceGroup + "</b> resource group for cross subscription migration. Please see the list below:<br>";

                string srcSubscription1 = migrationRecord.srcSubscription.Replace(" ", "");
                string resourceGroupName1 = migrationRecord.srcResourceGroup;
                MigrationRecord mr1 = null;

                if (resourceGroupName1 != null && resourceGroupName1.Length > 0)
                    mr1 = await BuildResourceGroupResourceList(srcSubscription1, resourceGroupName1, res, dp);

                var task = await PopulateOriginatingResourceGroup(mr1, dp, res);

                strSolution = strSolution + DisplayRgRecord(mr1);
                
            }

            break;

    }

    strSolution = strSolution + "</ul>";

    return strSolution;
}

private static string DisplayRgRecord(MigrationRecord migrationRecord)
{
    string strReturn = null;
    
    strReturn = "<ol>";

    int n = 0;
    foreach (WebResource webResource in migrationRecord.webResourceList)
    {
        strReturn = strReturn  + "<li><b>" + webResource.resourceName + "</b> (" + webResource.resourceType + ")</li>";

        if (webResource.originatingResourceGroupName != null && webResource.originatingResourceGroupName.Length > 0)
        {
            if (webResource.originatingResourceGroupName.ToLower() != migrationRecord.srcResourceGroup.ToLower())
            {
                if (webResource.resourceType == "sites")
                {
                    strReturn = strReturn + "<b><font size=1 color=\"red\">NOTE:</font></b>This site was originally created in <b>"  + "\"" + webResource.originatingResourceGroupName + "\"" + "</b> resource group." + " Please move this site back to the <b>" + "\"" + webResource.originatingResourceGroupName + "\"" + "</b> resource group to enable it for new migration operation.";
                }

                if (webResource.resourceType == "serverFarms")
                {
                    strReturn = strReturn + "<b><font size=1 color=\"red\">NOTE:</font></b>This App Service Plan was originally created in <b>"  + "\"" + webResource.originatingResourceGroupName + "\"" + "</b> resource group." + " Please move this site back to the <b>" + "\"" + webResource.originatingResourceGroupName + "\"" + "</b> resource group to enable it for new migration operation.";
                }
            }
        }
        
        n++;
    }

    if (n == 0)
        strReturn = strReturn + "<i>Sorry, the resource group doesn't have any Microsoft.Web resources at the moment.</i>";

    strReturn = strReturn + "</ol>";

    return strReturn;
}

[AppFilter(AppType = AppType.All, PlatformType = PlatformType.Windows | PlatformType.Linux, StackType = StackType.All)]
[Definition(Id = "Migration", Name = "Migration Operations", Author = "marashid", Description = "Analyze failed migration operations of a subscription in a given time frame")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    string sGeoMaster = "gm-prod-sn1";
    string SubscriptionId = cxt.Resource.SubscriptionId;
    Response resAllOperations = new Response();
    Response resSuccessfulOperations = new Response();
    Response resFailedOperations = new Response();
    Response resCrossSubFailedOperations = new Response();
    Response resSameSubFailedOperations = new Response();
    var nAllOperations = 0;
    var nSuccessfulOperations = 0;
    var nFailedOperations = 0;

    var tableMigration = await dp.Kusto.ExecuteQuery(GetMoveOperationsQuery(cxt), sGeoMaster);

    foreach (DataRow row in tableMigration.Rows)
    {
        nAllOperations++;
    }

    foreach (DataRow row in tableMigration.Select("OperationStatus = 'Success'"))
    {
        nSuccessfulOperations++;
    }

    foreach (DataRow row in tableMigration.Select("OperationStatus = 'Failed'"))
    {
        nFailedOperations++;
    }

    resSuccessfulOperations.Dataset.Add(new DiagnosticData()
    {
        Table = await dp.Kusto.ExecuteQuery(GetSuccessfulMoveOperationsQuery(cxt), sGeoMaster),
        RenderingProperties = new Rendering(RenderingType.Table) { Title = "Resource move operations successfully completed on subscription " + SubscriptionId, Description = "Time Frame: " + cxt.StartTime + " to " + cxt.EndTime }
    });

    resCrossSubFailedOperations.Dataset.Add(new DiagnosticData()
    {
        Table = await dp.Kusto.ExecuteQuery(GetFailedMoveOperationsCrossSubQuery(cxt), sGeoMaster),
        RenderingProperties = new Rendering(RenderingType.Table) { Title = "Failed cross-subscription move operations on subscription " + SubscriptionId, Description = "Time Frame: " + cxt.StartTime + " to " + cxt.EndTime }
    });

    resSameSubFailedOperations.Dataset.Add(new DiagnosticData()
    {
        Table = await dp.Kusto.ExecuteQuery(GetFailedMoveOperationsSameSubQuery(cxt), sGeoMaster),
        RenderingProperties = new Rendering(RenderingType.Table) { Title = "Failed same subscription move operations on subscription " + SubscriptionId, Description = "Time Frame: " + cxt.StartTime + " to " + cxt.EndTime }
    });

    DataSummary failedSummary = new DataSummary("Failed", nFailedOperations.ToString(), "red");
    DataSummary successSummary = new DataSummary("Success", nSuccessfulOperations.ToString(), "#007300");
    DataSummary totalSummary = new DataSummary("Total", nAllOperations.ToString(), "blue");

    res.AddDataSummary(new List<DataSummary>() { failedSummary, successSummary, totalSummary });

    res.Dataset.AddRange(resCrossSubFailedOperations.Dataset);
    res.Dataset.AddRange(resSameSubFailedOperations.Dataset);
    res.Dataset.AddRange(resSuccessfulOperations.Dataset);

    var tableMigrationAnalysis = await dp.Kusto.ExecuteQuery(GetMoveOperationsQueryForAnalysis(cxt), sGeoMaster);

    List<MigrationRecord> migrationRecords;

    migrationRecords = await StartAnalysis(tableMigrationAnalysis, dp, res);

    if (migrationRecords != null)
    {
        var task = await Report(migrationRecords, res, dp);
    }

    return res;
}