using System.Net;

[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "checkipconfiguration", Name = "Check IP Configuration", Author = "puneetg,magopise", Description = "This detector helps to display the inbound and outbound IP configuration for a Web App")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    var observerSite = (await dp.Observer.GetSite(cxt.Resource.Stamp.Name, cxt.Resource.Name))[0];
    var outboundIp = (string)observerSite.stamp.outbound_ip_addresses;

    var insightDetailsOutboundIp = new Dictionary<string, string>();
    insightDetailsOutboundIp.Add("Outbound IP addresses", 
    $"<markdown>Your Web App will use one of these IP addresses `{ outboundIp }`</markdown>");

    insightDetailsOutboundIp.Add("Where is this displayed in Portal ?", 
    @"<markdown>
    To get this in Azure portal, please follow the steps :-
    1. Browse to the details of your specific web app
    2. Towards the top of the details for your web app, there is a link for `All settings`. 
    3. Clicking `All settings` will open up a list of web app information that you can drill into further. The specific information to drill into is **Properties**. Click on the **Properties** section.
    4. Within the **Properties** section, there is a textbox showing the set of **Outbound IP Addresses**. Using the icon to the side of the *Outbound IP Addresses* textbox you can select all of the addresses. Pressing Ctrl+C will then copy the addresses to the clipboard.
    </markdown>");

    Insight whatIsMyOutboundIp = new Insight(InsightStatus.Info, "What is my Outbound IP address ?", insightDetailsOutboundIp);
    res.AddInsight(whatIsMyOutboundIp);
    var ipBased = false;
    string ipAddress = "";
    string hostName = "";

    foreach(var host in observerSite.hostnames)
    {
        if (host.hostname.ToString().EndsWith(".azurewebsites.net"))
        {
            hostName = host.hostname.ToString();
        }

        if (host.vip_mapping != null)
        {
            if (host.vip_mapping.virtual_ip !=null)
            {                
                ipAddress = host.vip_mapping.virtual_ip ;               
                ipBased = true;
            }            
        }
    }

    if (ipBased)
    {
        var insightDetailsInboundIp = new Dictionary<string, string>();
        insightDetailsInboundIp.Add("Inbound IP addresses", 
            $"<markdown>Since you have configured IP Based SSL, your Web App's IP address is `{ ipAddress }`</markdown>");
        Insight whatIsmyWebAppIp = new Insight(InsightStatus.Info, "What is my Web App's IP address ?", insightDetailsInboundIp);
        res.AddInsight(whatIsmyWebAppIp);
    }
    else
    {
        IPAddress address = Dns.GetHostAddresses(hostName)[0];
        var insightDetailsInboundIp = new Dictionary<string, string>();
        insightDetailsInboundIp.Add("Inbound IP addresses", 
            $"<markdown>Your Web App will use one of these IP addresses `{ address.ToString() }`</markdown>");
        Insight whatIsmyWebAppIp = new Insight(InsightStatus.Info, "What is my Web App's IP address ?", insightDetailsInboundIp);
        res.AddInsight(whatIsmyWebAppIp);
    }

     res.AddMarkdownView(@"
    ### Will my App's Outbound IP address ever change ?
    We never change the outbound IP addresses assigned to a Web App but if we do, we inform you well in advance over an email so that you can whitelist the new IP address'es for your Web App 

    ### How do I get a dedicated IP address for my Web App ?
    If you need to configure a dedicated\reserved IP address for inbound calls made to the azure web app site, you will need to install and configure an IP based SSL certificate. Please note that in order to do this your App Service Plan should be in Basic or higher pricing tier. You also need a custom domain and a certifcate assigned to your Web App before configuring an IP Based SSL Binding.
    
    ### What happens if I switch from SNI-Based SSL Binding to IP-Based SSL Binding ?
    If you delete an exisiting binding, then you likley will get a new inbound IP address allocated.  This would cause a problem with an A record DNS configuration.  So make sure before deleting the IP Based binding, you are absolutely sure that you don't need that binding and that you are ok to reconfigure the DNS settings for the Web App if required
    
    ");

    return res;
}