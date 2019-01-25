private static string GetQuery(OperationContext<HostingEnvironment> cxt)
{
    return
    $@"cluster('wawscus').database('wawsprod').
        AntaresAdminGeoEvents
            | where HostingEnvironmentName == '{cxt.Resource.Name}'
            | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}
            | where EventId == 50085 or EventId == 50065 or EventId  == 50066 
            | project PreciseTimeStamp , Status = iff(Result=='Healthy', 1, -1)
            | summarize Status = min(Status) by Time = bin(PreciseTimeStamp, 5m)
            | project Time, Up = iff(Status>0, 1, 0), Status = 0, Down = iff(Status<0, -1, 0)";
}
//, Details, FinalResult = iff(Result !='Healthy',strcat(Result, Details), Result) , Exception

[HostingEnvironmentFilter(HostingEnvironmentType = HostingEnvironmentType.All, PlatformType = PlatformType.Windows)]
[Definition(Id = "ASEUpDown", Name = "ASE Up Down", Description = "Checks if ASE has been healthy during this duration")]
public async static Task<Response> Run(DataProviders dp, OperationContext<HostingEnvironment> cxt, Response res)
{
    res.Dataset.Add(new DiagnosticData()
    {
        Table = await dp.Kusto.ExecuteQuery(GetQuery(cxt), cxt.Resource.InternalName),
        RenderingProperties = new TimeSeriesRendering() {
            Title = "Up or Down",
            GraphType = TimeSeriesType.BarGraph

        }
    });

    res.Dataset.Add(new DiagnosticData()
    {
        Table = await dp.Kusto.ExecuteQuery(GetQuery(cxt), cxt.Resource.InternalName),
        RenderingProperties = new Rendering(RenderingType.Table){Title = "Sample Table", Description = "Some description here"}
    });


    return res;
}