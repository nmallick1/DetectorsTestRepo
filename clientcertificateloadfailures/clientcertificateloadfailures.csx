[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "clientcertificateloadfailures", Name = "Client Cert Failures", Description = "This detector checks for client certificates load failures.")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
   var certDeletionEvents = await dp.Kusto.ExecuteQuery(GetQuery(cxt), cxt.Resource.Stamp.Name);

    if (certDeletionEvents.Rows.Count > 0)
    {
        res.AddInsight(new Insight(InsightStatus.Critical, "Application code may receive a CryptographicException or NullReferenceException while trying to reference the X509ClientCertificate2 object."));

        res.Dataset.Add(new DiagnosticData()
        {
            Table = certDeletionEvents,
            RenderingProperties = new TimeSeriesRendering() 
            {
                GraphType = TimeSeriesType.BarGraph,
                Title = $"Client Certificate Deletion Events" , 
                Description = $"The number of times the client certificate was deleted because want to avoid exhausting the disk space."
            }
        });

        string email = "<strong><u>Cause</u></strong>";
        email  += @"<br/>Every time a new instance of <strong>X509Certificate2</strong> object is created, some space on the disk is used. 
                        This happens specifically if the <strong>X509KeyStorageFlags.PersistKeySet</strong> flag is used while creating 
                        the certificate. To protect disk space, Azure App Services throttles the applications that go above 
                        a specificate rate of creating certificates and once that happens, we delete the certificate and try
                        to restore the disk space. This can cause application code to fail with <strong>System.Security.Cryptography.CryptographicException</strong> 
                        or the application may fail to read the certificate from certificate store and may fail with a <strong>System.NullReferenceException</strong>";

        email += "<br/><br/><strong><u>Resolution</u></strong>";
        email += @"<br/>It is recommended that you follow these best practices to avoid running in to this issue :- <br/>
                            <ol>
                                <li>
                                    Use the recommended way to create certificates in App Service as described in <a href='https://azure.microsoft.com/en-in/blog/using-certificates-in-azure-websites-applications/'>Using Certificates in Azure Websites Applications</a> and <a href='https://blogs.msdn.microsoft.com/karansingh/2017/03/15/azure-app-services-how-to-determine-if-the-client-certificate-is-loaded/'>Azure App Services: How to determine if the client certificate is loaded</a>
                                </li>
                                <li>
                                    Add an AppSetting on the web app <strong>WEBSITE_LOAD_USER_PROFILE</strong> and set it to 1. That will make sure that the certificate data is stored in a special folder designed just for your site (not shared with other sites on the box)
                                </li>
                                <li>
                                    Avoid creating a new <strong>X509Certificate2</strong> object on each request.
                                </li>
                            </ol>";
    
       res.AddEmail(email); 
                                 
    }
    else
    {
        Insight insight = new Insight(InsightStatus.Success, "We did not detect any issues with loading client certificates for this Web App");
        res.AddInsight(insight); 
    }

    return res;
}

private static string GetQuery(OperationContext<App> cxt)
{
    return
   $@"AntaresRuntimeWorkerEvents
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | where EventId == 15093
        | where OwnerName == strcat('IIS APPPOOL\\','{cxt.Resource.Name}') or OwnerName startswith strcat('IIS APPPOOL\\','{cxt.Resource.Name}__')
        | summarize TimesDeleted=count() by bin(TIMESTAMP, 5m)";
}



