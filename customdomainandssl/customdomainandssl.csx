using System.Net;
using System.Runtime.InteropServices;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.ComponentModel;


    public class CNameRecordInfo
    {
        public string Name { get; set; }
        public int TTL { get; set; }
        public int ResolutionLatencyInMilliseconds { get; set; }

        public CNameRecordInfo(string name, int ttl = -1, int resolutionLatencyInMilliseconds = -1)
        {
            Name = name;
            TTL = ttl;
            ResolutionLatencyInMilliseconds = resolutionLatencyInMilliseconds;
        }

        public CNameRecordInfo()
        {
            Name = string.Empty;
            TTL = -1;
            ResolutionLatencyInMilliseconds = -1;
        }
    }


    public enum DNNSRecordTypes
    { 
        CNAME,
        A,
        TXT,
        NS,
        UNSPECIFIED
    }
    public class DNSEntry : CNameRecordInfo
    {
        public DNNSRecordTypes RecordType;
        public DNSEntry(CNameRecordInfo recordToCopy)
        {
            this.Name = recordToCopy.Name.ToLower();
            this.ResolutionLatencyInMilliseconds = recordToCopy.ResolutionLatencyInMilliseconds;
            this.TTL = recordToCopy.TTL;
            this.RecordType = DNNSRecordTypes.CNAME;
        }
        
        public DNSEntry(System.Net.IPAddress ipAddress)
        {
            this.Name = ipAddress.ToString();
            this.ResolutionLatencyInMilliseconds = -1;
            this.TTL = -1;
            this.RecordType = DNNSRecordTypes.A;
        }

    }

    /// <summary>
    /// Helper methods related to the DNS - it is expected all names passed in are already in ASCII format so the caller is responsible to convert IDN host names to Punycode.
    /// </summary>
    public static class DnsUtilities
    {

        private static System.Collections.Generic.List<string> TLDs =  new System.Collections.Generic.List<string>()  ;
        public static bool isNakedDomain(string domain)
        {
            bool isNakedDomain = false;
            if (domain.Split('.').Length == 2)
            {
                isNakedDomain = true;
                return isNakedDomain;
            }
            if (DnsUtilities.TLDs.Count < 1)
            {
                //Populate the TLD's ASYNC here, issue with JObject and JArray hence hardcoding
                
                //List of all the TLD's found from https://api.ote-godaddy.com/v1/domains/tlds.
                DnsUtilities.TLDs.Add(".academy"); DnsUtilities.TLDs.Add(".accountants"); DnsUtilities.TLDs.Add(".actor"); DnsUtilities.TLDs.Add(".ag"); DnsUtilities.TLDs.Add(".agency"); DnsUtilities.TLDs.Add(".airforce"); DnsUtilities.TLDs.Add(".am"); DnsUtilities.TLDs.Add(".amsterdam"); DnsUtilities.TLDs.Add(".apartments"); DnsUtilities.TLDs.Add(".archi"); DnsUtilities.TLDs.Add(".army"); DnsUtilities.TLDs.Add(".asia"); DnsUtilities.TLDs.Add(".associates"); DnsUtilities.TLDs.Add(".at"); DnsUtilities.TLDs.Add(".attorney"); DnsUtilities.TLDs.Add(".auction"); DnsUtilities.TLDs.Add(".audio"); DnsUtilities.TLDs.Add(".auto"); DnsUtilities.TLDs.Add(".band"); DnsUtilities.TLDs.Add(".bar"); DnsUtilities.TLDs.Add(".bargains"); DnsUtilities.TLDs.Add(".bayern"); DnsUtilities.TLDs.Add(".be"); DnsUtilities.TLDs.Add(".beer"); DnsUtilities.TLDs.Add(".best"); DnsUtilities.TLDs.Add(".bet"); DnsUtilities.TLDs.Add(".bid"); DnsUtilities.TLDs.Add(".bike"); DnsUtilities.TLDs.Add(".bingo"); DnsUtilities.TLDs.Add(".bio"); DnsUtilities.TLDs.Add(".biz"); DnsUtilities.TLDs.Add(".biz.pl"); DnsUtilities.TLDs.Add(".black"); DnsUtilities.TLDs.Add(".blackfriday"); DnsUtilities.TLDs.Add(".blog"); DnsUtilities.TLDs.Add(".blue"); DnsUtilities.TLDs.Add(".boston"); DnsUtilities.TLDs.Add(".boutique"); DnsUtilities.TLDs.Add(".build"); DnsUtilities.TLDs.Add(".builders"); DnsUtilities.TLDs.Add(".business"); DnsUtilities.TLDs.Add(".buzz"); DnsUtilities.TLDs.Add(".bz"); DnsUtilities.TLDs.Add(".ca"); DnsUtilities.TLDs.Add(".cab"); DnsUtilities.TLDs.Add(".cafe"); DnsUtilities.TLDs.Add(".camera"); DnsUtilities.TLDs.Add(".camp"); DnsUtilities.TLDs.Add(".capital"); DnsUtilities.TLDs.Add(".car"); DnsUtilities.TLDs.Add(".cards"); DnsUtilities.TLDs.Add(".care"); DnsUtilities.TLDs.Add(".careers"); DnsUtilities.TLDs.Add(".cars"); DnsUtilities.TLDs.Add(".casa"); DnsUtilities.TLDs.Add(".cash"); DnsUtilities.TLDs.Add(".casino"); DnsUtilities.TLDs.Add(".catering"); DnsUtilities.TLDs.Add(".cc"); DnsUtilities.TLDs.Add(".center"); DnsUtilities.TLDs.Add(".ceo"); DnsUtilities.TLDs.Add(".ch"); DnsUtilities.TLDs.Add(".chat"); DnsUtilities.TLDs.Add(".cheap"); DnsUtilities.TLDs.Add(".christmas"); DnsUtilities.TLDs.Add(".church"); DnsUtilities.TLDs.Add(".city"); DnsUtilities.TLDs.Add(".claims"); DnsUtilities.TLDs.Add(".cleaning"); DnsUtilities.TLDs.Add(".click"); DnsUtilities.TLDs.Add(".clinic"); DnsUtilities.TLDs.Add(".clothing"); DnsUtilities.TLDs.Add(".cloud"); DnsUtilities.TLDs.Add(".club"); DnsUtilities.TLDs.Add(".cn"); DnsUtilities.TLDs.Add(".co"); DnsUtilities.TLDs.Add(".co.in"); DnsUtilities.TLDs.Add(".co.nz"); DnsUtilities.TLDs.Add(".co.uk"); DnsUtilities.TLDs.Add(".co.ve"); DnsUtilities.TLDs.Add(".co.za"); DnsUtilities.TLDs.Add(".coach"); DnsUtilities.TLDs.Add(".codes"); DnsUtilities.TLDs.Add(".coffee"); DnsUtilities.TLDs.Add(".college"); DnsUtilities.TLDs.Add(".com"); DnsUtilities.TLDs.Add(".com.ag"); DnsUtilities.TLDs.Add(".com.bz"); DnsUtilities.TLDs.Add(".com.co"); DnsUtilities.TLDs.Add(".com.es"); DnsUtilities.TLDs.Add(".com.mx"); DnsUtilities.TLDs.Add(".com.ph"); DnsUtilities.TLDs.Add(".com.pl"); DnsUtilities.TLDs.Add(".com.tw"); DnsUtilities.TLDs.Add(".com.ve"); DnsUtilities.TLDs.Add(".com.tr"); DnsUtilities.TLDs.Add(".community"); DnsUtilities.TLDs.Add(".company"); DnsUtilities.TLDs.Add(".computer"); DnsUtilities.TLDs.Add(".condos"); DnsUtilities.TLDs.Add(".construction"); DnsUtilities.TLDs.Add(".consulting"); DnsUtilities.TLDs.Add(".contractors"); DnsUtilities.TLDs.Add(".cooking"); DnsUtilities.TLDs.Add(".cool"); DnsUtilities.TLDs.Add(".country"); DnsUtilities.TLDs.Add(".coupons"); DnsUtilities.TLDs.Add(".courses"); DnsUtilities.TLDs.Add(".credit"); DnsUtilities.TLDs.Add(".creditcard"); DnsUtilities.TLDs.Add(".cruises"); DnsUtilities.TLDs.Add(".cz"); DnsUtilities.TLDs.Add(".dance"); DnsUtilities.TLDs.Add(".dating"); DnsUtilities.TLDs.Add(".de"); DnsUtilities.TLDs.Add(".deals"); DnsUtilities.TLDs.Add(".degree"); DnsUtilities.TLDs.Add(".delivery"); DnsUtilities.TLDs.Add(".democrat"); DnsUtilities.TLDs.Add(".dental"); DnsUtilities.TLDs.Add(".dentist"); DnsUtilities.TLDs.Add(".desi"); DnsUtilities.TLDs.Add(".design"); DnsUtilities.TLDs.Add(".diamonds"); DnsUtilities.TLDs.Add(".diet"); DnsUtilities.TLDs.Add(".digital"); DnsUtilities.TLDs.Add(".direct"); DnsUtilities.TLDs.Add(".directory"); DnsUtilities.TLDs.Add(".discount"); DnsUtilities.TLDs.Add(".doctor"); DnsUtilities.TLDs.Add(".dog"); DnsUtilities.TLDs.Add(".domains"); DnsUtilities.TLDs.Add(".earth"); DnsUtilities.TLDs.Add(".education"); DnsUtilities.TLDs.Add(".email"); DnsUtilities.TLDs.Add(".energy"); DnsUtilities.TLDs.Add(".engineer"); DnsUtilities.TLDs.Add(".engineering"); DnsUtilities.TLDs.Add(".enterprises"); DnsUtilities.TLDs.Add(".equipment"); DnsUtilities.TLDs.Add(".es"); DnsUtilities.TLDs.Add(".estate"); DnsUtilities.TLDs.Add(".eu"); DnsUtilities.TLDs.Add(".events"); DnsUtilities.TLDs.Add(".exchange"); DnsUtilities.TLDs.Add(".expert"); DnsUtilities.TLDs.Add(".exposed"); DnsUtilities.TLDs.Add(".express"); DnsUtilities.TLDs.Add(".fail"); DnsUtilities.TLDs.Add(".family"); DnsUtilities.TLDs.Add(".fans"); DnsUtilities.TLDs.Add(".farm"); DnsUtilities.TLDs.Add(".fashion"); DnsUtilities.TLDs.Add(".film"); DnsUtilities.TLDs.Add(".finance"); DnsUtilities.TLDs.Add(".financial"); DnsUtilities.TLDs.Add(".firm.in"); DnsUtilities.TLDs.Add(".fish"); DnsUtilities.TLDs.Add(".fishing"); DnsUtilities.TLDs.Add(".fit"); DnsUtilities.TLDs.Add(".fitness"); DnsUtilities.TLDs.Add(".flights"); DnsUtilities.TLDs.Add(".florist"); DnsUtilities.TLDs.Add(".flowers"); DnsUtilities.TLDs.Add(".fm"); DnsUtilities.TLDs.Add(".football"); DnsUtilities.TLDs.Add(".forsale"); DnsUtilities.TLDs.Add(".foundation"); DnsUtilities.TLDs.Add(".fun"); DnsUtilities.TLDs.Add(".fund"); DnsUtilities.TLDs.Add(".furniture"); DnsUtilities.TLDs.Add(".futbol"); DnsUtilities.TLDs.Add(".fyi"); DnsUtilities.TLDs.Add(".gallery"); DnsUtilities.TLDs.Add(".game"); DnsUtilities.TLDs.Add(".games"); DnsUtilities.TLDs.Add(".garden"); DnsUtilities.TLDs.Add(".gen.in"); DnsUtilities.TLDs.Add(".gift"); DnsUtilities.TLDs.Add(".gifts"); DnsUtilities.TLDs.Add(".gives"); DnsUtilities.TLDs.Add(".glass"); DnsUtilities.TLDs.Add(".global"); DnsUtilities.TLDs.Add(".gmbh"); DnsUtilities.TLDs.Add(".gold"); DnsUtilities.TLDs.Add(".golf"); DnsUtilities.TLDs.Add(".graphics"); DnsUtilities.TLDs.Add(".gratis"); DnsUtilities.TLDs.Add(".green"); DnsUtilities.TLDs.Add(".gripe"); DnsUtilities.TLDs.Add(".group"); DnsUtilities.TLDs.Add(".gs"); DnsUtilities.TLDs.Add(".guide"); DnsUtilities.TLDs.Add(".guitars"); DnsUtilities.TLDs.Add(".guru"); DnsUtilities.TLDs.Add(".haus"); DnsUtilities.TLDs.Add(".healthcare"); DnsUtilities.TLDs.Add(".help"); DnsUtilities.TLDs.Add(".hiphop"); DnsUtilities.TLDs.Add(".hiv"); DnsUtilities.TLDs.Add(".hockey"); DnsUtilities.TLDs.Add(".holdings"); DnsUtilities.TLDs.Add(".holiday"); DnsUtilities.TLDs.Add(".horse"); DnsUtilities.TLDs.Add(".host"); DnsUtilities.TLDs.Add(".hosting"); DnsUtilities.TLDs.Add(".house"); DnsUtilities.TLDs.Add(".idv.tw"); DnsUtilities.TLDs.Add(".immo"); DnsUtilities.TLDs.Add(".immobilien"); DnsUtilities.TLDs.Add(".in"); DnsUtilities.TLDs.Add(".ind.in"); DnsUtilities.TLDs.Add(".industries"); DnsUtilities.TLDs.Add(".info"); DnsUtilities.TLDs.Add(".info.pl"); DnsUtilities.TLDs.Add(".info.ve"); DnsUtilities.TLDs.Add(".ink"); DnsUtilities.TLDs.Add(".institute"); DnsUtilities.TLDs.Add(".insure"); DnsUtilities.TLDs.Add(".international"); DnsUtilities.TLDs.Add(".investments"); DnsUtilities.TLDs.Add(".io"); DnsUtilities.TLDs.Add(".irish"); DnsUtilities.TLDs.Add(".ist"); DnsUtilities.TLDs.Add(".istanbul"); DnsUtilities.TLDs.Add(".jetzt"); DnsUtilities.TLDs.Add(".jewelry"); DnsUtilities.TLDs.Add(".juegos"); DnsUtilities.TLDs.Add(".kaufen"); DnsUtilities.TLDs.Add(".kim"); DnsUtilities.TLDs.Add(".kitchen"); DnsUtilities.TLDs.Add(".kiwi"); DnsUtilities.TLDs.Add(".la"); DnsUtilities.TLDs.Add(".land"); DnsUtilities.TLDs.Add(".lawyer"); DnsUtilities.TLDs.Add(".lease"); DnsUtilities.TLDs.Add(".legal"); DnsUtilities.TLDs.Add(".lgbt"); DnsUtilities.TLDs.Add(".life"); DnsUtilities.TLDs.Add(".lighting"); DnsUtilities.TLDs.Add(".limited"); DnsUtilities.TLDs.Add(".limo"); DnsUtilities.TLDs.Add(".link"); DnsUtilities.TLDs.Add(".live"); DnsUtilities.TLDs.Add(".loans"); DnsUtilities.TLDs.Add(".lol"); DnsUtilities.TLDs.Add(".london"); DnsUtilities.TLDs.Add(".love"); DnsUtilities.TLDs.Add(".ltd"); DnsUtilities.TLDs.Add(".ltda"); DnsUtilities.TLDs.Add(".luxury"); DnsUtilities.TLDs.Add(".maison"); DnsUtilities.TLDs.Add(".management"); DnsUtilities.TLDs.Add(".market"); DnsUtilities.TLDs.Add(".marketing"); DnsUtilities.TLDs.Add(".mba"); DnsUtilities.TLDs.Add(".me"); DnsUtilities.TLDs.Add(".me.uk"); DnsUtilities.TLDs.Add(".media"); DnsUtilities.TLDs.Add(".melbourne"); DnsUtilities.TLDs.Add(".memorial"); DnsUtilities.TLDs.Add(".menu"); DnsUtilities.TLDs.Add(".miami"); DnsUtilities.TLDs.Add(".mobi"); DnsUtilities.TLDs.Add(".moda"); DnsUtilities.TLDs.Add(".moe"); DnsUtilities.TLDs.Add(".mom"); DnsUtilities.TLDs.Add(".money"); DnsUtilities.TLDs.Add(".mortgage"); DnsUtilities.TLDs.Add(".movie"); DnsUtilities.TLDs.Add(".ms"); DnsUtilities.TLDs.Add(".mx"); DnsUtilities.TLDs.Add(".nagoya"); DnsUtilities.TLDs.Add(".navy"); DnsUtilities.TLDs.Add(".net"); DnsUtilities.TLDs.Add(".net.ag"); DnsUtilities.TLDs.Add(".net.bz"); DnsUtilities.TLDs.Add(".net.co"); DnsUtilities.TLDs.Add(".net.in"); DnsUtilities.TLDs.Add(".net.nz"); DnsUtilities.TLDs.Add(".net.ph"); DnsUtilities.TLDs.Add(".net.pl"); DnsUtilities.TLDs.Add(".net.ve"); DnsUtilities.TLDs.Add(".network"); DnsUtilities.TLDs.Add(".news"); DnsUtilities.TLDs.Add(".ninja"); DnsUtilities.TLDs.Add(".nl"); DnsUtilities.TLDs.Add(".nom.co"); DnsUtilities.TLDs.Add(".nom.es"); DnsUtilities.TLDs.Add(".nyc"); DnsUtilities.TLDs.Add(".okinawa"); DnsUtilities.TLDs.Add(".one"); DnsUtilities.TLDs.Add(".onl"); DnsUtilities.TLDs.Add(".online"); DnsUtilities.TLDs.Add(".org"); DnsUtilities.TLDs.Add(".org.ag"); DnsUtilities.TLDs.Add(".org.es"); DnsUtilities.TLDs.Add(".org.in"); DnsUtilities.TLDs.Add(".org.nz"); DnsUtilities.TLDs.Add(".org.ph"); DnsUtilities.TLDs.Add(".org.pl"); DnsUtilities.TLDs.Add(".org.tw"); DnsUtilities.TLDs.Add(".org.uk"); DnsUtilities.TLDs.Add(".org.ve"); DnsUtilities.TLDs.Add(".paris"); DnsUtilities.TLDs.Add(".partners"); DnsUtilities.TLDs.Add(".parts"); DnsUtilities.TLDs.Add(".pet"); DnsUtilities.TLDs.Add(".ph"); DnsUtilities.TLDs.Add(".photo"); DnsUtilities.TLDs.Add(".photography"); DnsUtilities.TLDs.Add(".photos"); DnsUtilities.TLDs.Add(".pics"); DnsUtilities.TLDs.Add(".pictures"); DnsUtilities.TLDs.Add(".pink"); DnsUtilities.TLDs.Add(".pizza"); DnsUtilities.TLDs.Add(".pl"); DnsUtilities.TLDs.Add(".place"); DnsUtilities.TLDs.Add(".plumbing"); DnsUtilities.TLDs.Add(".plus"); DnsUtilities.TLDs.Add(".poker"); DnsUtilities.TLDs.Add(".press"); DnsUtilities.TLDs.Add(".pro"); DnsUtilities.TLDs.Add(".productions"); DnsUtilities.TLDs.Add(".promo"); DnsUtilities.TLDs.Add(".properties"); DnsUtilities.TLDs.Add(".property"); DnsUtilities.TLDs.Add(".protection"); DnsUtilities.TLDs.Add(".pub"); DnsUtilities.TLDs.Add(".qpon"); DnsUtilities.TLDs.Add(".recipes"); DnsUtilities.TLDs.Add(".red"); DnsUtilities.TLDs.Add(".rehab"); DnsUtilities.TLDs.Add(".reise"); DnsUtilities.TLDs.Add(".reisen"); DnsUtilities.TLDs.Add(".rent"); DnsUtilities.TLDs.Add(".rentals"); DnsUtilities.TLDs.Add(".repair"); DnsUtilities.TLDs.Add(".report"); DnsUtilities.TLDs.Add(".republican"); DnsUtilities.TLDs.Add(".rest"); DnsUtilities.TLDs.Add(".restaurant"); DnsUtilities.TLDs.Add(".reviews"); DnsUtilities.TLDs.Add(".rich"); DnsUtilities.TLDs.Add(".rip"); DnsUtilities.TLDs.Add(".rocks"); DnsUtilities.TLDs.Add(".rodeo"); DnsUtilities.TLDs.Add(".run"); DnsUtilities.TLDs.Add(".ryukyu"); DnsUtilities.TLDs.Add(".sale"); DnsUtilities.TLDs.Add(".salon"); DnsUtilities.TLDs.Add(".sarl"); DnsUtilities.TLDs.Add(".school"); DnsUtilities.TLDs.Add(".schule"); DnsUtilities.TLDs.Add(".security"); DnsUtilities.TLDs.Add(".services"); DnsUtilities.TLDs.Add(".sex"); DnsUtilities.TLDs.Add(".sexy"); DnsUtilities.TLDs.Add(".sg"); DnsUtilities.TLDs.Add(".shiksha"); DnsUtilities.TLDs.Add(".shoes"); DnsUtilities.TLDs.Add(".shop"); DnsUtilities.TLDs.Add(".shopping"); DnsUtilities.TLDs.Add(".show"); DnsUtilities.TLDs.Add(".singles"); DnsUtilities.TLDs.Add(".site"); DnsUtilities.TLDs.Add(".ski"); DnsUtilities.TLDs.Add(".soccer"); DnsUtilities.TLDs.Add(".social"); DnsUtilities.TLDs.Add(".software"); DnsUtilities.TLDs.Add(".solar"); DnsUtilities.TLDs.Add(".solutions"); DnsUtilities.TLDs.Add(".space"); DnsUtilities.TLDs.Add(".store"); DnsUtilities.TLDs.Add(".stream"); DnsUtilities.TLDs.Add(".studio"); DnsUtilities.TLDs.Add(".study"); DnsUtilities.TLDs.Add(".style"); DnsUtilities.TLDs.Add(".supplies"); DnsUtilities.TLDs.Add(".supply"); DnsUtilities.TLDs.Add(".support"); DnsUtilities.TLDs.Add(".surf"); DnsUtilities.TLDs.Add(".surgery"); DnsUtilities.TLDs.Add(".sydney"); DnsUtilities.TLDs.Add(".systems"); DnsUtilities.TLDs.Add(".tattoo"); DnsUtilities.TLDs.Add(".tax"); DnsUtilities.TLDs.Add(".taxi"); DnsUtilities.TLDs.Add(".tc"); DnsUtilities.TLDs.Add(".team"); DnsUtilities.TLDs.Add(".tech"); DnsUtilities.TLDs.Add(".technology"); DnsUtilities.TLDs.Add(".tel"); DnsUtilities.TLDs.Add(".tennis"); DnsUtilities.TLDs.Add(".theater"); DnsUtilities.TLDs.Add(".theatre"); DnsUtilities.TLDs.Add(".tienda"); DnsUtilities.TLDs.Add(".tips"); DnsUtilities.TLDs.Add(".tires"); DnsUtilities.TLDs.Add(".tk"); DnsUtilities.TLDs.Add(".today"); DnsUtilities.TLDs.Add(".tokyo"); DnsUtilities.TLDs.Add(".tools"); DnsUtilities.TLDs.Add(".tours"); DnsUtilities.TLDs.Add(".town"); DnsUtilities.TLDs.Add(".toys"); DnsUtilities.TLDs.Add(".trade"); DnsUtilities.TLDs.Add(".training"); DnsUtilities.TLDs.Add(".tube"); DnsUtilities.TLDs.Add(".tv"); DnsUtilities.TLDs.Add(".tw"); DnsUtilities.TLDs.Add(".university"); DnsUtilities.TLDs.Add(".uno"); DnsUtilities.TLDs.Add(".us"); DnsUtilities.TLDs.Add(".vacations"); DnsUtilities.TLDs.Add(".vegas"); DnsUtilities.TLDs.Add(".ventures"); DnsUtilities.TLDs.Add(".vet"); DnsUtilities.TLDs.Add(".viajes"); DnsUtilities.TLDs.Add(".video"); DnsUtilities.TLDs.Add(".villas"); DnsUtilities.TLDs.Add(".vin"); DnsUtilities.TLDs.Add(".vip"); DnsUtilities.TLDs.Add(".vision"); DnsUtilities.TLDs.Add(".vodka"); DnsUtilities.TLDs.Add(".vote"); DnsUtilities.TLDs.Add(".voting"); DnsUtilities.TLDs.Add(".voto"); DnsUtilities.TLDs.Add(".voyage"); DnsUtilities.TLDs.Add(".watch"); DnsUtilities.TLDs.Add(".web.ve"); DnsUtilities.TLDs.Add(".webcam"); DnsUtilities.TLDs.Add(".website"); DnsUtilities.TLDs.Add(".wedding"); DnsUtilities.TLDs.Add(".wiki"); DnsUtilities.TLDs.Add(".wine"); DnsUtilities.TLDs.Add(".work"); DnsUtilities.TLDs.Add(".works"); DnsUtilities.TLDs.Add(".world"); DnsUtilities.TLDs.Add(".ws"); DnsUtilities.TLDs.Add(".wtf"); DnsUtilities.TLDs.Add(".xn--6frz82g"); DnsUtilities.TLDs.Add(".xyz"); DnsUtilities.TLDs.Add(".yoga"); DnsUtilities.TLDs.Add(".yokohama"); DnsUtilities.TLDs.Add(".zone");
            }
            
            string largestMatchingTLDYet = "";
            foreach (string tld in DnsUtilities.TLDs)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(domain, tld, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    if (tld.Length > largestMatchingTLDYet.Length)
                    {
                        largestMatchingTLDYet = tld;
                    }
                }
            }

            domain = domain.Replace(largestMatchingTLDYet, "");

            if (domain.Split('.').Length > 1)
            {
                isNakedDomain = false;
            }
            else
            {
                isNakedDomain = true;
            }

            return isNakedDomain;
        }

        public static string getRecommendedDNSConfiguration(BindingInfo currBinding, System.Net.IPAddress CorrectIPToResolveTo, OperationContext<App> cxt)
        {
            //cxt.Resource.Stamp.Name, cxt.Resource.Name
            string output = "Replace any existing DNS configuration you have for "+ currBinding.HostName +" and follow the steps below (the exact steps to create DNS entries will differ based on your DNS provider). <br/><br/>";
            if(currBinding.SSLType == SSLTypes.None)
            {
                if(DnsUtilities.isNakedDomain(currBinding.HostName))
                {
                    //output = @"Create an A record for `currBinding.HostName` pointing to `" + CorrectIPToResolveTo.ToString() + "`<br/>Note: If your DNS provider allows you to create a CNAME for the naked domain, ";
                    output+= @"If your DNS provider allows creation of CNAME records for naked domains, create the DNS record as follows
                    ..* Create a <strong>CNAME</strong> record for `" + currBinding.HostName + @"` pointing to `" + cxt.Resource.Name.ToLower() + @".azurewebsites.net` alias
                    If your DNS provider does not allow creation on a CNAME for naked domains, configure your DNS as follows
                    ..*Create an <strong>A</strong> record for `" + currBinding.HostName + @"` pointing to `" + CorrectIPToResolveTo.ToString() + @"` IP";

                }
                else
                {
                    if(SiteCapabilites.isTrafficManagerEnabled)
                    {
                        if(SiteCapabilites.TMURIs !=null)
                        {
                            //Although if isTrafficManagerEnabled, TMURIs should always have a value. Doing so for sanity check
                            if(SiteCapabilites.TMURIs.Count > 1)
                            {
                                output+= @" Create a <strong>CNAME</strong> record for `" + currBinding.HostName + @"` pointing to one of the following Traffic Manager(TM) hostnames depending on the TM profile you want to utilize`" + cxt.Resource.Name.ToLower() + @".azurewebsites.net` alias ";
                                foreach(string tmuri in SiteCapabilites.TMURIs)
                                {
                                    output+= "..* `" + tmuri + "` ";
                                }
                            }
                            else
                            {
                                if(SiteCapabilites.TMURIs.Count < 1)
                                {
                                    //No Traffic Manager URI were added. Should never hit this but lets handle it...
                                    output+= @" Create a <strong>CNAME</strong> record for `" + currBinding.HostName + @"` pointing to the Traffic Manager hostname configured against this site ";
                                }
                                else
                                {
                                    output+= @" Create a <strong>CNAME</strong> record for `" + currBinding.HostName + @"` pointing to `" + SiteCapabilites.TMURIs[0] + @"` alias ";
                                }
                            }
                        }
                        else
                        {
                            output+= @" Create a <strong>CNAME</strong> record for `" + currBinding.HostName + @"` pointing to the Traffic Manager hostname configured against this site ";
                        }
                    }
                    else
                    {
                        output+= @" Create a <strong>CNAME</strong> record for `" + currBinding.HostName + @"` pointing to `" + cxt.Resource.Name.ToLower() + @".azurewebsites.net` alias ";
                    }
                }
            }
            else
            {
                //For IP SSL or SNI SSL, same DNS settings, it's the resulting IP that is different depending on whether the URL is a naked domain or not

                if(DnsUtilities.isNakedDomain(currBinding.HostName))
                    {                        
                        output+= @"If your DNS provider allows creation of CNAME records for naked domains, create the DNS records as follows
                        ..* Create a <strong>CNAME</strong> record for `" + currBinding.HostName + @"` pointing to `" + cxt.Resource.Name.ToLower() + @".azurewebsites.net` alias
                        If your DNS provider does not allow creation on a CNAME for naked domains, configure your DNS as follows
                        ..*Create an <strong>A</strong> record for `" + currBinding.HostName + @"` pointing to `" + CorrectIPToResolveTo.ToString() + @"` IP";

                    }
                    else
                    {
                        if(SiteCapabilites.isTrafficManagerEnabled)
                        {
                            if(SiteCapabilites.TMURIs !=null)
                            {
                                //Although if isTrafficManagerEnabled, TMURIs should always have a value. Doing so for sanity check
                                if(SiteCapabilites.TMURIs.Count > 1)
                                {
                                    output+= @" Create a <strong>CNAME</strong> record for `" + currBinding.HostName + @"` pointing to one of the following Traffic Manager(TM) hostnames depending on the TM profile you want to utilize`" + cxt.Resource.Name.ToLower() + @".azurewebsites.net` alias ";
                                    foreach(string tmuri in SiteCapabilites.TMURIs)
                                    {
                                        output+= "..* `" + tmuri + "` ";
                                    }
                                }
                                else
                                {
                                    if(SiteCapabilites.TMURIs.Count < 1)
                                    {
                                        //No Traffic Manager URI were added. Should never hit this but lets handle it...
                                        output+= @" Create a <strong>CNAME</strong> record for `" + currBinding.HostName + @"` pointing to the Traffic Manager hostname configured against this site ";
                                    }
                                    else
                                    {
                                        output+= @" Create a <strong>CNAME</strong> record for `" + currBinding.HostName + @"` pointing to `" + SiteCapabilites.TMURIs[0] + @"` alias ";
                                    }
                                }
                            }
                            else
                            {
                                output+= @" Create a <strong>CNAME</strong> record for `" + currBinding.HostName + @"` pointing to the Traffic Manager hostname configured against this site ";
                            }
                        }
                        else
                        {
                            output+= @" Create a <strong>CNAME</strong> record for `" + currBinding.HostName + @"` pointing to `" + cxt.Resource.Name.ToLower() + @".azurewebsites.net` alias ";
                        }  
                    }                
            }
            output = output + "<br/><br/>After updating the DNS entries, you might have to wait for up to " + currBinding.getMaxTTLString() + " for the DNS entries to propagate before you are able to validate the settings.";
            return output;
        }



        private const int ThrottlingSleep = 100;

        private static IPAddress GetIPv4Address(string hostName)
        {
            IPAddress tempAddress;
            if (IPAddress.TryParse(hostName, out tempAddress))
            {
                if (tempAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return tempAddress;
                }
            }

            IPHostEntry entry = Dns.GetHostEntry(hostName);
            foreach (IPAddress ipAddress in entry.AddressList)
            {
                if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ipAddress;
                }
            }

            return null;
        }

        public static double WaitForPropagation(string[] nsServers, string hostName, double threshold, TimeSpan timeout, bool waitForJustOneNameserver, out string verboseOutput)
        {
            if (nsServers == null || nsServers.Length == 0)
            {
                // No name server is set, let's use our local DNS server
                nsServers = GetDnsAddresses().Select(x => x.ToString()).ToArray();
            }
            DateTime expirationTime = DateTime.Now.Add(timeout);
            bool[] propagated = new bool[nsServers.Length];
            int[] iterations = new int[nsServers.Length];
            int[] errorCodes = new int[nsServers.Length];
            TimeSpan[] propagationTimes = new TimeSpan[nsServers.Length];

            for (int i = 0; i < propagated.Length; i++)
            {
                propagated[i] = false;
                iterations[i] = -1;
                propagationTimes[i] = TimeSpan.Zero;
                errorCodes[i] = 0;
            }

            double ratio = double.NaN;
            int iteration = 0;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            while (DateTime.Now < expirationTime)
            {
                iteration++;

                List<Task> tasks = new List<Task>();
                int goodHits = 0;
                int n = 0;

                for (int i = 0; i < nsServers.Length; i++)
                {
                    int ii = i;

                    if (!propagated[i])
                    {
                        string nsServer = nsServers[i];
                        IPAddress ipAddress = GetIPv4Address(nsServer);

                        if (ipAddress == null)
                        {
                            continue;
                        }

                        tasks.Add(Task.Factory.StartNew(() =>
                        {
                            if (VerifyHostnameIsPropagated(ipAddress, hostName, out errorCodes[ii]))
                            {
                                goodHits++;
                                propagated[ii] = true;
                                iterations[ii] = iteration;
                                propagationTimes[ii] = stopWatch.Elapsed;
                            }
                        }));
                    }
                    else
                    {
                        // We've already tested this one and it was fine
                        goodHits++;
                    }

                    n++;
                }

                if (n == 0)
                {
                    // We were not able to resolve any NS server, just bail out
                    verboseOutput = string.Empty;
                    return double.NaN;
                }

                if (tasks.Count > 0)
                {
                    if (waitForJustOneNameserver)
                    {
                        Task.WaitAny(tasks.ToArray());

                        if (goodHits > 0)
                        {
                            ratio = (double)goodHits / n;
                            break;
                        }

                        Task.WaitAll(tasks.ToArray());
                        ratio = (double)goodHits / n;
                    }
                    else
                    {
                        Task.WaitAll(tasks.ToArray());

                        ratio = (double)goodHits / n;

                        if (ratio >= threshold)
                        {
                            break;
                        }
                    }
                }

                Thread.Sleep(ThrottlingSleep);
            }

            stopWatch.Stop();

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < nsServers.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append("|");
                }
                // EC = Error Code
                sb.AppendFormat("{0}: {1} in {2}, {3}, EC {4}", nsServers[i], propagated[i], (propagated[i] ? propagationTimes[i] : stopWatch.Elapsed), (propagated[i] ? iterations[i] : iteration), errorCodes[i]);
            }

            if (waitForJustOneNameserver)
            {
                sb.Append("|JustOne:1");
            }

            verboseOutput = sb.ToString();

            return ratio;
        }

        public static IPAddress[] GetDnsAddresses()
        {
            IPAddress[] result = null;
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces().Where(i => i.OperationalStatus == OperationalStatus.Up))
            {
                IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();
                IPAddressCollection dnsAddresses = ipProperties.DnsAddresses;

                result = dnsAddresses.Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToArray();

                if (result.Length > 0)
                {
                    return result;
                }
            }

            throw new InvalidOperationException();
        }

        /// <summary>
        /// Gets the all CNAMEs in the chain
        /// </summary>
        /// <param name="startDomain"></param>
        /// <param name="stopDomain"></param>
        /// <returns></returns>
        public static List<CNameRecordInfo> GetCNameRecordsRecursive(string startDomain, string stopDomain = null, IPAddress ns = null)
        {
            List<CNameRecordInfo> ret = new List<CNameRecordInfo>();
            Queue<CNameRecordInfo> q = new Queue<CNameRecordInfo>(GetCNameRecords(startDomain, ns));
            HashSet<string> alreadyProcessed = new HashSet<string>();

            while (q.Count > 0)
            {
                CNameRecordInfo domain = q.Dequeue();
                if (!alreadyProcessed.Contains(domain.Name, StringComparer.OrdinalIgnoreCase))
                {
                    // To avoid some circular CNAME attack
                    alreadyProcessed.Add(domain.Name);
                    ret.Add(domain);

                    if (stopDomain == null || !domain.Name.Trim(new char[] { '.' }).EndsWith(stopDomain, StringComparison.OrdinalIgnoreCase))
                    {
                        // dot not pass the NS down, because it would be out of context - the NS variable is only for the starting domain
                        CNameRecordInfo[] domains = GetCNameRecords(domain.Name);

                        foreach (CNameRecordInfo s in domains)
                        {
                            q.Enqueue(s);
                        }
                    }
                }
            }

            return ret;
        }

        /// <summary>
        /// Returns a list of CNAMEs for a specified domain name (if it points somewhere)
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        public static CNameRecordInfo[] GetCNameRecords(string domain, IPAddress nameServer = null)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            IntPtr ptr1 = IntPtr.Zero;
            IntPtr ptr2 = IntPtr.Zero;
            CNameRecord cNameRecord;

            List<CNameRecordInfo> list1 = new List<CNameRecordInfo>();

            int errorCode;
            if (nameServer != null)
            {
                IP4_ARRAY dnsServers = new IP4_ARRAY();
                dnsServers.AddrCount = 1;
                dnsServers.AddrArray = new uint[1] { BitConverter.ToUInt32(nameServer.GetAddressBytes(), 0) };

                errorCode = DnsQueryExtra(ref domain, QueryTypes.DNS_TYPE_CNAME, QueryOptions.DNS_QUERY_BYPASS_CACHE, ref dnsServers, ref ptr1, 0);
            }
            else
            {
                errorCode = DnsQuery(ref domain, QueryTypes.DNS_TYPE_CNAME, QueryOptions.DNS_QUERY_BYPASS_CACHE, 0, ref ptr1, 0);
            }

            try
            {
                if (errorCode != 0)
                {
                    if (errorCode == ERROR_CODE_NO_RECORDS_FOUND)
                    {
                        return new CNameRecordInfo[] { };
                    }

                    if (errorCode == ERROR_CODE_DNS_SERVER_FAILURE)
                    {
                        return new CNameRecordInfo[] { };
                    }

                    if (errorCode == ERROR_CODE_DNS_NAME_ERROR)
                    {
                        return new CNameRecordInfo[] { };
                    }

                    throw new Win32Exception(errorCode);
                }
                for (ptr2 = ptr1; !ptr2.Equals(IntPtr.Zero); ptr2 = cNameRecord.pNext)
                {
                    cNameRecord = (CNameRecord)Marshal.PtrToStructure(ptr2, typeof(CNameRecord));
                    if (cNameRecord.wType == (short)QueryTypes.DNS_TYPE_CNAME)
                    {
                        string text1 = Marshal.PtrToStringAuto(cNameRecord.pNameHost);
                        list1.Add(new CNameRecordInfo(text1, cNameRecord.dwTtl, (int)stopwatch.ElapsedMilliseconds));
                    }
                }
            }
            finally
            {
                if (ptr1 != IntPtr.Zero)
                {
                    DnsRecordListFree(ptr1, 0);
                }
            }

            stopwatch.Stop();

            return list1.ToArray();
        }

        /// <summary>
        /// Returns a list of TXT records for a specified domain name (if it points somewhere)
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        public static string[] GetTxtRecords(string domain, IPAddress nameServer = null)
        {

            IntPtr ptr1 = IntPtr.Zero;
            IntPtr ptr2 = IntPtr.Zero;
            TxtRecord txtRecord;

            ArrayList list1 = new ArrayList();

            int errorCode;
            if (nameServer != null)
            {
                IP4_ARRAY dnsServers = new IP4_ARRAY();
                dnsServers.AddrCount = 1;
                dnsServers.AddrArray = new uint[1] { BitConverter.ToUInt32(nameServer.GetAddressBytes(), 0) };

                errorCode = DnsQueryExtra(ref domain, QueryTypes.DNS_TYPE_TXT, QueryOptions.DNS_QUERY_BYPASS_CACHE, ref dnsServers, ref ptr1, 0);
            }
            else
            {
                errorCode = DnsQuery(ref domain, QueryTypes.DNS_TYPE_TXT, QueryOptions.DNS_QUERY_BYPASS_CACHE, 0, ref ptr1, 0);
            }
            try
            {
                if (errorCode != 0)
                {
                    if (errorCode == ERROR_CODE_NO_RECORDS_FOUND)
                    {
                        return new string[] { };
                    }

                    if (errorCode == ERROR_CODE_DNS_SERVER_FAILURE)
                    {
                        return new string[] { };
                    }

                    if (errorCode == ERROR_CODE_DNS_NAME_ERROR)
                    {
                        return new string[] { };
                    }

                    throw new Win32Exception(errorCode);
                }
                for (ptr2 = ptr1; !ptr2.Equals(IntPtr.Zero); ptr2 = txtRecord.pNext)
                {
                    txtRecord = (TxtRecord)Marshal.PtrToStructure(ptr2, typeof(TxtRecord));
                    if (txtRecord.wType == (short)QueryTypes.DNS_TYPE_TXT && txtRecord.dwStringCount > 0)
                    {
                        // we take only the first string, for simplicity
                        string text1 = Marshal.PtrToStringAuto(txtRecord.pStringArray);
                        list1.Add(text1);
                    }
                }
            }
            finally
            {
                if (ptr1 != IntPtr.Zero)
                {
                    DnsRecordListFree(ptr1, 0);
                }
            }
            return (string[])list1.ToArray(typeof(string));
        }

        /// <summary>
        /// Retrieves A records for a domain using a specific name server
        /// </summary>
        /// <param name="domain">domain name, e.g. "www.microsoft.com"</param>
        /// <param name="nameServer">IP address of a name server</param>
        /// <returns></returns>
        public static string[] GetARecordsNonRecursively(string domain, IPAddress nameServer = null)
        {
            IntPtr ptr1 = IntPtr.Zero;
            IntPtr ptr2 = IntPtr.Zero;
            ARecord aRecord;

            List<string> list1 = new List<string>();

            int errorCode;
            if (nameServer != null)
            {
                IP4_ARRAY dnsServers = new IP4_ARRAY();
                dnsServers.AddrCount = 1;
                dnsServers.AddrArray = new uint[1] { BitConverter.ToUInt32(nameServer.GetAddressBytes(), 0) };

                errorCode = DnsQueryExtra(ref domain, QueryTypes.DNS_TYPE_ANY, QueryOptions.DNS_QUERY_BYPASS_CACHE, ref dnsServers, ref ptr1, 0);
            }
            else
            {
                errorCode = DnsQuery(ref domain, QueryTypes.DNS_TYPE_ANY, QueryOptions.DNS_QUERY_BYPASS_CACHE, 0, ref ptr1, 0);
            }

            try
            {
                if (errorCode != 0)
                {
                    if (errorCode == ERROR_CODE_NO_RECORDS_FOUND)
                    {
                        return null;
                    }

                    if (errorCode == ERROR_CODE_DNS_SERVER_FAILURE)
                    {
                        return null;
                    }

                    if (errorCode == ERROR_CODE_DNS_NAME_ERROR)
                    {
                        return null;
                    }

                    throw new Win32Exception(errorCode);
                }
                for (ptr2 = ptr1; !ptr2.Equals(IntPtr.Zero); ptr2 = aRecord.pNext)
                {
                    aRecord = (ARecord)Marshal.PtrToStructure(ptr2, typeof(ARecord));
                    if (aRecord.wType == (uint)QueryTypes.DNS_TYPE_A)
                    {
                        list1.Add((new IPAddress(aRecord.dwAddress)).ToString());
                    }
                }
            }
            finally
            {
                if (ptr1 != IntPtr.Zero)
                {
                    DnsRecordListFree(ptr1, 0);
                }
            }

            if (list1 != null && list1.Count > 0)
            {
                return list1.ToArray();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a list of NS records for a given domain and traverses up to the top-level record in the zone if needed
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        public static string[] GetNameServersRecursively(string domain)
        {
            while (true)
            {
                int f = domain.IndexOf('.');
                if (f == -1)
                {
                    break;
                }

                string[] ret = GetNameServers(domain);
                if (ret.Length > 0)
                {
                    return ret;
                }

                domain = domain.Substring(f + 1);
            }

            return null;
        }

        /// <summary>
        /// Gets a list of NS records for a given domain (does not traverse up)
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        public static string[] GetNameServers(string domain)
        {
            if (!domain.EndsWith("."))
            {
                domain += ".";
            }

            IntPtr ptr1 = IntPtr.Zero;
            IntPtr ptr2 = IntPtr.Zero;
            CNameRecord cNameRecord;

            ArrayList list1 = new ArrayList();

            int errorCode = DnsQuery(ref domain, QueryTypes.DNS_TYPE_NS, QueryOptions.DNS_QUERY_BYPASS_CACHE, 0, ref ptr1, 0);
            try
            {
                if (errorCode != 0)
                {
                    if (errorCode == ERROR_CODE_NO_RECORDS_FOUND)
                    {
                        return new string[] { };
                    }

                    if (errorCode == ERROR_CODE_DNS_SERVER_FAILURE)
                    {
                        return new string[] { };
                    }

                    if (errorCode == ERROR_CODE_DNS_NAME_ERROR)
                    {
                        return new string[] { };
                    }

                    throw new Win32Exception(errorCode);
                }
                for (ptr2 = ptr1; !ptr2.Equals(IntPtr.Zero); ptr2 = cNameRecord.pNext)
                {
                    cNameRecord = (CNameRecord)Marshal.PtrToStructure(ptr2, typeof(CNameRecord));
                    if (cNameRecord.wType == (short)QueryTypes.DNS_TYPE_NS)
                    {
                        string text1 = Marshal.PtrToStringAuto(cNameRecord.pNameHost);
                        list1.Add(text1);
                    }
                }
            }
            finally
            {
                if (ptr1 != IntPtr.Zero)
                {
                    DnsRecordListFree(ptr1, 0);
                }
            }
            return (string[])list1.ToArray(typeof(string));
        }

        /// <summary>
        /// Returns a list of NS for a given domain. If it does not find NS servers, it traverses up to parent domain and tries again.
        /// That is important e.g. for private environments which are not actual DNS zone, so their default DNS suffixes do not return any NS records.
        /// </summary>
        /// <param name="nameServer"></param>
        /// <param name="domain"></param>
        /// <returns></returns>
        public static string[] GetNSNameRecords(IPAddress nameServer, string domain)
        {
            return GetNSNameRecordsInternal(nameServer, domain, true, true);
        }

        /// <summary>
        /// Returns NS records for a given domain.
        /// </summary>
        /// <param name="nameServer">IP address of NS if one specific server should be used, NULL if local default DNS server should be used.</param>
        /// <param name="domain"></param>
        /// <param name="traverseUp"></param>
        /// <param name="throwOnError"></param>
        /// <returns></returns>
        private static string[] GetNSNameRecordsInternal(IPAddress nameServer, string domain, bool traverseUp, bool throwOnError)
        {
            IntPtr ptr1 = IntPtr.Zero;
            IntPtr ptr2 = IntPtr.Zero;
            CNameRecord cNameRecord;

            ArrayList list1 = new ArrayList();

            int errorCode;
            if (nameServer != null)
            {
                IP4_ARRAY dnsServers = new IP4_ARRAY();
                dnsServers.AddrCount = 1;
                dnsServers.AddrArray = new uint[1] { BitConverter.ToUInt32(nameServer.GetAddressBytes(), 0) };

                errorCode = DnsQueryExtra(ref domain, QueryTypes.DNS_TYPE_NS, QueryOptions.DNS_QUERY_BYPASS_CACHE, ref dnsServers, ref ptr1, 0);
            }
            else
            {
                errorCode = DnsQuery(ref domain, QueryTypes.DNS_TYPE_NS, QueryOptions.DNS_QUERY_BYPASS_CACHE, 0, ref ptr1, 0);
            }

            try
            {
                if (errorCode != 0)
                {
                    if (errorCode == ERROR_CODE_NO_RECORDS_FOUND)
                    {
                        return new string[] { };
                    }

                    if (errorCode == ERROR_CODE_DNS_SERVER_FAILURE || errorCode == ERROR_CODE_DNS_NAME_ERROR)
                    {
                        if (traverseUp)
                        {
                            int firstDot = domain.IndexOf('.');
                            if (firstDot != -1)
                            {
                                domain = domain.Substring(firstDot + 1);
                                return GetNSNameRecordsInternal(nameServer, domain, traverseUp, throwOnError);
                            }
                        }
                        else
                        {
                            return new string[] { };
                        }
                    }

                    if (throwOnError)
                    {
                        throw new Win32Exception(errorCode);
                    }
                    else
                    {
                        return new string[] { };
                    }
                }
                for (ptr2 = ptr1; !ptr2.Equals(IntPtr.Zero); ptr2 = cNameRecord.pNext)
                {
                    cNameRecord = (CNameRecord)Marshal.PtrToStructure(ptr2, typeof(CNameRecord));
                    if (cNameRecord.wType == (short)QueryTypes.DNS_TYPE_NS)
                    {
                        string text1 = Marshal.PtrToStringAuto(cNameRecord.pNameHost);
                        list1.Add(text1);
                    }
                }
            }
            finally
            {
                if (ptr1 != IntPtr.Zero)
                {
                    DnsRecordListFree(ptr1, 0);
                }
            }
            return (string[])list1.ToArray(typeof(string));
        }

        /// <summary>
        /// Resolves a domain using a specific name server
        /// </summary>
        /// <param name="domain">domain name, e.g. "www.microsoft.com"</param>
        /// <param name="nameServer">IP address of a name server</param>
        /// <returns></returns>
        public static IPAddress Resolve(string domain, IPAddress nameServer = null)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            IntPtr ptr1 = IntPtr.Zero;
            IntPtr ptr2 = IntPtr.Zero;
            ARecord aRecord;

            List<IPAddress> list1 = new List<IPAddress>();

            int errorCode;
            if (nameServer != null)
            {
                IP4_ARRAY dnsServers = new IP4_ARRAY();
                dnsServers.AddrCount = 1;
                dnsServers.AddrArray = new uint[1] { BitConverter.ToUInt32(nameServer.GetAddressBytes(), 0) };

                errorCode = DnsQueryExtra(ref domain, QueryTypes.DNS_TYPE_A, QueryOptions.DNS_QUERY_BYPASS_CACHE, ref dnsServers, ref ptr1, 0);
            }
            else
            {
                errorCode = DnsQuery(ref domain, QueryTypes.DNS_TYPE_A, QueryOptions.DNS_QUERY_BYPASS_CACHE, 0, ref ptr1, 0);
            }

            try
            {
                if (errorCode != 0)
                {
                    if (errorCode == ERROR_CODE_NO_RECORDS_FOUND)
                    {
                        return IPAddress.None;
                    }

                    if (errorCode == ERROR_CODE_DNS_SERVER_FAILURE)
                    {
                        return IPAddress.None;
                    }

                    if (errorCode == ERROR_CODE_DNS_NAME_ERROR)
                    {
                        return IPAddress.None;
                    }

                    throw new Win32Exception(errorCode);
                }
                for (ptr2 = ptr1; !ptr2.Equals(IntPtr.Zero); ptr2 = aRecord.pNext)
                {
                    aRecord = (ARecord)Marshal.PtrToStructure(ptr2, typeof(ARecord));
                    if (aRecord.wType == (uint)QueryTypes.DNS_TYPE_A)
                    {
                        list1.Add(new IPAddress(aRecord.dwAddress));
                    }
                }
            }
            finally
            {
                if (ptr1 != IntPtr.Zero)
                {
                    DnsRecordListFree(ptr1, 0);
                }
            }

            stopwatch.Stop();

            if (list1.Count == 0)
            {
                return IPAddress.None;
            }

            return list1[list1.Count - 1];
        }

        /// <summary>
        /// Verify the hostname
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        public static bool VerifyHostnameIsPropagated(IPAddress nameServer, string hostName, out int errorCode)
        {
            IntPtr ptr1 = IntPtr.Zero;
            IntPtr ptr2 = IntPtr.Zero;

            ArrayList list1 = new ArrayList();

            IP4_ARRAY dnsServers = new IP4_ARRAY();
            dnsServers.AddrCount = 1;
            dnsServers.AddrArray = new uint[1] { BitConverter.ToUInt32(nameServer.GetAddressBytes(), 0) };

            errorCode = DnsQueryExtra(ref hostName, QueryTypes.DNS_TYPE_A, QueryOptions.DNS_QUERY_BYPASS_CACHE, ref dnsServers, ref ptr1, 0);
            try
            {
                if (errorCode != 0)
                {
                    if (errorCode == ERROR_CODE_NO_RECORDS_FOUND)
                    {
                        return false;
                    }

                    if (errorCode == ERROR_CODE_DNS_SERVER_FAILURE)
                    {
                        // DNS5
                        return false;
                    }

                    if (errorCode == ERROR_CODE_DNS_NAME_ERROR)
                    {
                        // DNS6
                        return false;
                    }

                    // Any other exception, keep trying under assumption that the hostname is not there, but it will be
                    return false;
                }
            }
            finally
            {
                if (ptr1 != IntPtr.Zero)
                {
                    DnsRecordListFree(ptr1, 0);
                }
            }

            return true;
        }

        #region DNS CName Lookup helpers
        [DllImport("dnsapi", EntryPoint = "DnsQuery_W", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        private static extern int DnsQuery([MarshalAs(UnmanagedType.VBByRefStr)]ref string pszName, QueryTypes wType, QueryOptions options, int aipServers, ref IntPtr ppQueryResults, int pReserved);

        [DllImport("dnsapi", EntryPoint = "DnsQuery_W", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        private static extern int DnsQueryExtra([MarshalAs(UnmanagedType.VBByRefStr)]ref string pszName, QueryTypes wType, QueryOptions options, ref IP4_ARRAY dnsServers, ref IntPtr ppQueryResults, int pReserved);

        [DllImport("dnsapi", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void DnsRecordListFree(IntPtr pRecordList, int FreeType);

        private const int ERROR_CODE_NO_RECORDS_FOUND = 9501;
        private const int ERROR_CODE_DNS_SERVER_FAILURE = 9002;
        private const int ERROR_CODE_DNS_NAME_ERROR = 9003;

        private enum QueryOptions
        {
            DNS_QUERY_ACCEPT_TRUNCATED_RESPONSE = 1,
            DNS_QUERY_BYPASS_CACHE = 8,
            DNS_QUERY_DONT_RESET_TTL_VALUES = 0x100000,
            DNS_QUERY_NO_HOSTS_FILE = 0x40,
            DNS_QUERY_NO_LOCAL_NAME = 0x20,
            DNS_QUERY_NO_NETBT = 0x80,
            DNS_QUERY_NO_RECURSION = 4,
            DNS_QUERY_NO_WIRE_QUERY = 0x10,
            DNS_QUERY_RESERVED = -16777216,
            DNS_QUERY_RETURN_MESSAGE = 0x200,
            DNS_QUERY_STANDARD = 0,
            DNS_QUERY_TREAT_AS_FQDN = 0x1000,
            DNS_QUERY_USE_TCP_ONLY = 2,
            DNS_QUERY_WIRE_ONLY = 0x100
        }

        private enum QueryTypes
        {
            DNS_TYPE_A = 1,
            DNS_TYPE_NS = 2,
            DNS_TYPE_CNAME = 5,
            DNS_TYPE_SOA = 6,
            DNS_TYPE_PTR = 12,
            DNS_TYPE_HINFO = 13,
            DNS_TYPE_MX = 15,
            DNS_TYPE_TXT = 16,
            DNS_TYPE_AAAA = 28,
            DNS_TYPE_ANY = 255
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CNameRecord
        {
            public IntPtr pNext;
            public string pName;
            public short wType;
            public short wDataLength;
            public int flags;
            public int dwTtl;
            public int dwReserved;
            public IntPtr pNameHost;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ARecord
        {
            public IntPtr pNext;
            public string pName;
            public short wType;
            public short wDataLength;
            public int flags;
            public int dwTtl;
            public int dwReserved;
            public uint dwAddress;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TxtRecord
        {
            public IntPtr pNext;
            public string pName;
            public short wType;
            public short wDataLength;
            public int flags;
            public int dwTtl;
            public int dwReserved;
            public int dwStringCount;
            public IntPtr pStringArray;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct IP4_ARRAY
        {
            /// DWORD->unsigned int
            internal uint AddrCount;

            /// IP4_ADDRESS[1]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1, ArraySubType = UnmanagedType.U4)]
            internal uint[] AddrArray;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct IP6_ADDRESS
        {
            /// DWORD[4]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.U4)]
            [FieldOffset(0)]
            internal uint[] IP6Dword;

            /// WORD[8]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8, ArraySubType = UnmanagedType.U2)]
            [FieldOffset(0)]
            internal ushort[] IP6Word;

            /// BYTE[16]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16, ArraySubType = UnmanagedType.I1)]
            [FieldOffset(0)]
            internal byte[] IP6Byte;
        }

        #endregion
    }




private static string GetQuery(OperationContext<App> cxt)
{
    return
    $@"<YOUR_TABLE_NAME>
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | <YOUR_QUERY>";
}

public enum SSLTypes
{
    None,
    IP,
    SNI
}

public class BindingInfo
{
    public string HostName;
    public bool isNakedDomain = false;
    public string ThumbPrint;
    public SSLTypes SSLType;
    public System.Net.IPAddress IP;
    private List<DNSEntry> DNSChain = null;
    

    public bool isTMInChain
    {
        get{
            var result = this.getDNSChain().Where(dnsentry => dnsentry.Name.ToLower().Contains(".trafficmanager.net"));
            if(result.Any())
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public List<DNSEntry> getDNSChain()
    {
        if(this.DNSChain != null)
        {
            return this.DNSChain;
        }
        else
        {
            try{
                this.DNSChain = DnsUtilities.GetCNameRecordsRecursive(this.HostName).Select(cname => new DNSEntry(cname)).ToList();
                DNSEntry lastEntry;
                if(this.DNSChain.Count > 0)
                {
                    lastEntry = new DNSEntry(DnsUtilities.Resolve(DNSChain.Last().Name));
                }
                else
                {
                    //This will be true if the configured hostname has IP Based SSL and the DNS directly points to an A Record
                    lastEntry = new DNSEntry(DnsUtilities.Resolve(this.HostName));
                }
                this.DNSChain.Add(lastEntry);
            }
            catch(Exception ex)
            {
                // if(ex.GetType().Equals(typeof("System.ComponentModel.Win32Exception")))
                // {
                //     ;
                // }
                // else
                // {
                //     throw ex;
                // }
            }
            return this.DNSChain;
        }

    }

    public string getDisplayForDNSChain(string lineSeperator = "")
    {
        System.Text.StringBuilder markdownOutput = new System.Text.StringBuilder();        

        if(lineSeperator == "")
        {
            lineSeperator = "<br/>";
        }

        string parent = this.HostName;

        foreach (DNSEntry entry in this.getDNSChain())
        {
            markdownOutput.Append(string.Format("`{0}` <i>{1}</i> <strong>{2}</strong> `{3}` {4}", parent, entry.TTL.ToString(), entry.RecordType, entry.Name, lineSeperator));
            parent = entry.Name;
        }
       return markdownOutput.ToString();
    }

    public string getMaxTTLString()
    {
        int? maxTTL = (from y in this.getDNSChain()         
         select (int?)y.TTL).Max();
        
        if(!maxTTL.HasValue)
        {
            return @" `Error getting TTL from DNS chain`for this URL ";
        }
        else
        {
            if(maxTTL.Value <0)
            {
                return " `0 minutes` ";
            }
            else
            {
                return " `"+ Math.Ceiling((maxTTL.Value*1.0)/3600) +" hour(s)` ";
            }
        }
    }

}

public class StampInfo
{
    public string Name;
    public string URI;
    public System.Net.IPAddress IP;
    public string Site;
    public string TrafficManagerURL;
}

public static class SiteCapabilites
{
    public static bool isTrafficManagerEnabled = false;
    public static List<string>TMURIs = null;
    public static bool isSniSSLEnabled = false;
    public static bool isIpSSLEnabled = false;

    //This is true if anywhere in the DNS resolution chain for any hostname we find a CNAME entry for cloudapp.net but not containing waws-prod string
    public static bool isAppGatewayEnabled = false;

    //This is true if we find any hostname not resolving to the Stamp IP / possible AppGateway / Traffic Manager 
    public static bool isPossibleProxy = false;
}

[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "CustomDomainAndSSL", Name = "Custom Bindings", Author = "nmallick", Description = "Analyze the DNS and configured bindings for a site and detect possible misconfigurations")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    try
    {

        #region Initialize Statics
            #region SiteCapabilities
                SiteCapabilites.isTrafficManagerEnabled = false;
                SiteCapabilites.TMURIs = null;
                SiteCapabilites.isSniSSLEnabled = false;
                SiteCapabilites.isIpSSLEnabled = false;
                SiteCapabilites.isAppGatewayEnabled = false;
                SiteCapabilites.isPossibleProxy = false;
            #endregion
        #endregion
        //TEST A SCENARIO WHERE THE SAME DOMAIN NAME IS BOUND TO TWO DIFFERENT CERTIFICATES FOR SNI


        //TODO Get a list of TLD's ASYNC FROM the following and store it in a static Dictionary. It can then be looked up easily
        /*
        GET /v1/domains/tlds HTTP/1.1
        Host: api.ote-godaddy.com
        Authorization: sso-key UzQxLikm_46KxDFnbjN7cQjmw6wocia:46L26ydpkwMaKZV6uVdDWe
        Cache-Control: no-cache
        */


        //Haven't tested with ASE (ILB or regular)
        //Example IP Based SSL Site : OptimalWeb
        //Example SNI Based SSL Site : beymen
        //Example Traffic Manager configured Site : beymenclub
        //If a site has IP based SSL configured, then make sure that it does not have any other SNI bindings associated with it else even the SNI bindings will be served the IP SSL cert

        var siteData = (await dp.Observer.GetSite(cxt.Resource.Stamp.Name, cxt.Resource.Name))[0];
        bool isFree = siteData.sku == "Free" ;
        if(!isFree)
        {            
            #region Populate Stamp Details
                StampInfo Stamp = new StampInfo();
                Stamp.Name = (string)siteData.stamp.name;
                Stamp.URI = (string)siteData.stamp.name + ".cloudapp.net";
                Stamp.IP = DnsUtilities.Resolve(Stamp.URI);
                Stamp.Site = (string)siteData.name;
                Stamp.TrafficManagerURL = "";
            #endregion



            //Perform these checks only for NonFree webapps


            var hostNamesToProcess = new Dictionary<string, BindingInfo>(); 
            // bool trafficManagerEnabled = false;
            // bool ipSSLEnabled = false;
            // bool sniSSLEnabled = false;
            foreach(dynamic h in siteData.hostnames)
            {
                string currHostName = (string)h.hostname;
                if(currHostName.ToLower().Contains(".trafficmanager.net"))
                {
                    SiteCapabilites.isTrafficManagerEnabled = true;
                    if(SiteCapabilites.TMURIs == null)
                    {
                        SiteCapabilites.TMURIs = new List<string>();
                    }
                    SiteCapabilites.TMURIs.Add(currHostName.ToLower());
                }
                if(!currHostName.ToLower().Contains(".azurewebsites.net"))
                {
                    BindingInfo currBindingObj = new BindingInfo();
                    currBindingObj.HostName = currHostName.ToLower();
                    currBindingObj.isNakedDomain = DnsUtilities.isNakedDomain(currHostName);
                    if((string)h.ssl_enabled == "Sni")
                    {
                        SiteCapabilites.isSniSSLEnabled = true;
                        currBindingObj.SSLType = SSLTypes.SNI;
                        currBindingObj.ThumbPrint = (string)h.thumbprint;
                        //SNI bindings always resolves to the Stamp's IP. Hence update that here.
                        currBindingObj.IP = Stamp.IP;
                    }
                    else
                    {
                        if((string)h.ssl_enabled == "IpBased")
                        {
                            SiteCapabilites.isIpSSLEnabled = true;
                            currBindingObj.SSLType = SSLTypes.IP;
                            currBindingObj.ThumbPrint = (string)h.thumbprint;
                            currBindingObj.IP = IPAddress.Parse((string)h.vip_mapping.virtual_ip);
                        }
                        else
                        {
                            currBindingObj.SSLType = SSLTypes.None;
                            currBindingObj.ThumbPrint = "";
                            currBindingObj.IP = Stamp.IP;
                        }

                        var result = currBindingObj.getDNSChain().Where(dnsEntry => (dnsEntry.Name.ToLower().Contains(".cloudapp.net")) && (!(dnsEntry.Name.ToLower().Contains("waws-prod-"))));
                        if(result.Any())
                        {
                            //This means that somewhere in the DNS chain we resolved to a cloudapp.net but it was not a waws-prod endpoint. The only such combination I know of so far is for AppGateway.
                            SiteCapabilites.isAppGatewayEnabled = true;
                        }                        
                    }
                    hostNamesToProcess.Add(currHostName, currBindingObj);
                }
            }

            if(hostNamesToProcess.Count > 0)
            {
                //Website configured with Custom Domains to Process

                var Stamps = new List<StampInfo>();

                if(SiteCapabilites.isTrafficManagerEnabled)
                {
                    //There is a good chance that two app service web apps will be configured with the same TrafficManager HostName but on different stamps
                    //Get a list of Stamps and resolve their IP's. It will help check if the traffic manager IP resolves to any one of the stamp
                    
                    //Get a list of all the HostNames that are trafficManager URL's. Although rare, a customer might configure more than one TM profile to point to the same webapp
                    var TMUrls = hostNamesToProcess.Where(kvp => kvp.Value.HostName.Contains("trafficmanager.net"));
                    foreach(KeyValuePair<string, BindingInfo> binding in TMUrls)
                    {
                        string resourceURI = "https://wawsobserver.azurewebsites.windows.net/sites/" + binding.Value.HostName;

                        var currSiteData =  await dp.Observer.GetResource(resourceURI);
                        for(int i = 0; i < currSiteData.Count; i++)
                        {
                            StampInfo currStamp = new StampInfo();
                            currStamp.Name = (string)currSiteData[i].stamp.name;
                            currStamp.URI = (((string)currSiteData[i].stamp.name) + ".cloudapp.net");
                            currStamp.Site = (string)currSiteData[i].name;
                            currStamp.TrafficManagerURL = binding.Value.HostName;
                            currStamp.IP = DnsUtilities.Resolve(currStamp.URI);
                            

                            Stamps.Add(currStamp);

                        }
                        
                    }
                }
                else
                {
                    Stamps.Add(Stamp);
                }

                if(SiteCapabilites.isIpSSLEnabled)
                {
                    //The DNS for any hostname can resolve to the IP SSL IP as well so adding that IP to the stamps list
                    //This will make sure subsequent matches succeed
                    var IPSSL_Ips = (from BI in hostNamesToProcess.Values where BI.SSLType == SSLTypes.IP select BI.IP).Distinct();
                    foreach(System.Net.IPAddress currIP in IPSSL_Ips)
                    {
                        StampInfo currStamp = new StampInfo();
                        currStamp.Name = (string)siteData.stamp.name;
                        currStamp.URI = (((string)siteData.stamp.name) + ".cloudapp.net");
                        currStamp.Site = (string)siteData.name;
                        currStamp.TrafficManagerURL = "";
                        currStamp.IP =  currIP ;
                        Stamps.Add(currStamp);
                    }
                }


                if(SiteCapabilites.isIpSSLEnabled && SiteCapabilites.isSniSSLEnabled)
                {
                    
                    //If both IP SSL and SNI SSL are enabled then there is a good chance that even for SNI enabled bindings the IP based SSL certificate will be returned. We recommend not to configure SNI bindings where IP Based SSL is configured
                    //Check if each of the CERTIFICATES that are configured for SNI match the IP SSL or not, if they don't then it is likely that browsing to those URL's will result in the IP SSL cert being displayed instead of the configured CERT
                    //Display a warning only if the certs are different
                    // var body = new Dictionary<string,string>();
                    // body["Scenario"] = "When both IP SSL and SNI SSL are configured and if some of the clients are not SNI compatible, there is a chance that browsing to the SNI configured endpoints will return the certificate configured for IP SSL";
                    // body["Insight"] = "This happens because when clients that are non SNI compatible are the first ones to make a request to the site, the IP SSL certificate is associated with the website. Any request that hits the static IP is then served the configured certificate irrespective of what URL was used to browse to the website. We recommend the use of wildcard certificate if there is a need to browse over multiple URL's. Consider configuring multiple IP SSL hostnames if wild card certificates do not fit your scenario.";

                    // res.AddInsight(InsightStatus.Warning, "Both IP and SNI SSL are enabled for the site", body);
                }
                else
                {
                    if(SiteCapabilites.isIpSSLEnabled)
                    {
                        //Here, with IP based SSL, if non SNI compatible  clients are used to hit the website then browsing to *.azurewebsites.net URL will return the configured custom SSL certificate instead of the azurewebsites.net certificate
                        //Intentionally not showing this warning message as this is a rare scenario
                        //We need to have a way of flagging some message only internally. This would be a good message for folks to see internally if the customer is indeed running into this scenario
                        //TODO, make a call to the azurewebsites.net endpoint and inspect the returned certificate. Display this message only if the returned certificate is incorrect
                        //Check to see if the site is ever accessed over the azurewebsites.net URL. If it is, then give a warning

                        
                    }
                    else
                    {
                        //Either the site has only SNI enabled or no SSL and there is nothing that will always fail with such a configuration so NOOP
                    }
                }

   

                //Start processing each binding now

               foreach(BindingInfo currBinding in hostNamesToProcess.Values)
               {
                   if(!currBinding.HostName.Contains(".trafficmanager.net"))
                   {
                       //There is no need to process *.trafficmanager.net URL's since TM will automatically take care of mapping the correct IP. Look for every other hostname
                       
                       System.Net.IPAddress currentDNSIP = DnsUtilities.Resolve(currBinding.HostName);
                        if(currentDNSIP.Equals(IPAddress.None))
                        {
                            //DNS not configured for this Hostname
                            //Call a function that given the BindingInfo object returns the suggested DNS settings for this hostname
                            //Show a Critical Insight here..
                            var body = new Dictionary<string, string>();
                            body["Insight"] = "We were unable to resolve the URL " + currBinding.HostName + ". Unconfigured/Missing DNS entries for this URL is the most probable cause of this failure. Due to an unconfigured DNS, clients will fail to resolve the URL " + currBinding.HostName + " to this website and hence the website will not be accessible over this URL.";
                            body["Recommendation"] = "<br/> Please update your DNS settings so that the URL <strong><i>" + currBinding.HostName + "</i></strong> resolves to <strong>" + currBinding.IP.ToString() + "</strong> " + ((SiteCapabilites.isTrafficManagerEnabled)? " or to the traffic manager URL ":" ") + " as per below.";
                            body["Recommended DNS Configuration"] = "<markdown>" + DnsUtilities.getRecommendedDNSConfiguration(currBinding, currBinding.IP, cxt) + "</markdown>";
                            res.AddInsight(InsightStatus.Critical, "No DNS entries configured for URL  " + currBinding.HostName, body);
                        }
                        else
                        {
                            //Stamps contains the IP's for every single Stamp that the TM points to + the current Stamp VIP + IP SSL IP
                            //So As long as the current DNS resolution points to any of the IP's in the stamp, the URL will land on the correct site irrespective of what SSL was chosen.

                            System.Net.IPAddress correctIPToResolveTo = System.Net.IPAddress.None;
                            string IPSSL_IpsCSV = "";

                            if(SiteCapabilites.isIpSSLEnabled)
                            { 
                                var IPSSL_Ips = (from BI in hostNamesToProcess.Values where BI.SSLType == SSLTypes.IP select BI.IP).Distinct();                                
                                foreach(System.Net.IPAddress currIP in IPSSL_Ips)
                                {
                                    correctIPToResolveTo = currIP;
                                    IPSSL_IpsCSV+= currIP.ToString() + ", ";                                            
                                }
                                IPSSL_IpsCSV+= "_";
                                IPSSL_IpsCSV = IPSSL_IpsCSV.Replace(", _", " ");
                            }
                            else
                            {
                                correctIPToResolveTo = currBinding.IP;                                
                            }

                            var match = Stamps.Where(si => si.IP.Equals(currentDNSIP));
                            if(match.Any())
                            {
                                //The DNS resolution was fine..
                                //Check to see if the resolution occurred over CNAME / A Record
                                //If over CNAME, ensure that it has sitename.azurewebsites.net in the name resolution chain
                                //If over A, make sure that it is a naked domain

                                if(currBinding.getDNSChain().Count == 1)
                                {
                                    //The name resolution is over A record
                                    if(DnsUtilities.isNakedDomain(currBinding.HostName))
                                    {
                                        //It's fine for a naked domain to resolve via A record. All good
                                        var body = new Dictionary<string,string>();
                                        body["Insight"] = "The URL <strong>" + currBinding.HostName + "</strong> resolves to <strong> " + currentDNSIP.ToString() + "</strong> " + ((SiteCapabilites.isIpSSLEnabled)? " which is the static IP assigned to the site." : ".");
                                        body["Current DNS Configuration"] = "<markdown>Digweb output for `"+ currBinding.HostName + "` based on the current DNS settings.<br/><br/>" + currBinding.getDisplayForDNSChain() + "</markdown>";
                                        res.AddInsight(InsightStatus.Success,"DNS Name resolution check passed for "+ currBinding.HostName, body);
                                    }
                                    else
                                    {
                                        //Sub-optimal DNS configuration detected. 
                                        //Non-Naked domain resolving over A record
                                        var body = new Dictionary<string,string>();
                                        body["Insight"] = "The URL <strong>" + currBinding.HostName + "</strong> resolves to <strong> " + currentDNSIP.ToString() + "</strong> " + ((SiteCapabilites.isIpSSLEnabled)?" which is the static IP assigned to the site":" ") + ". However the name resolution is configured over an A record. <br/><br/>In case the IP changes due to any reason " + ((SiteCapabilites.isIpSSLEnabled)? "(e.g.. Someone accidentally deleting the binding, thereby releasing the static IP and then re-acquiring it by creating a new binding or switching to a non static IP binding)" : " (e.g.. Although rare, the IP may change under certain circumstances during an Azure platform maintenance) ") + " , it may cause the website to stop working.";
                                        body["Current DNS Configuration"] = "<markdown>Digweb output for `"+ currBinding.HostName + "` based on the current DNS settings.<br/><br/>" + currBinding.getDisplayForDNSChain() + "</markdown>";
                                        body["Recommendation"] = "<br/> Please update your DNS settings so that the URL <strong><i>" + currBinding.HostName + "</i></strong> resolves to <strong>" + correctIPToResolveTo.ToString() + "</strong> as per below.";
                                        body["Recommended DNS Configuration"] = "<markdown>" + DnsUtilities.getRecommendedDNSConfiguration(currBinding, correctIPToResolveTo, cxt) + "</markdown>";
                                        res.AddInsight(InsightStatus.Warning,"Sub-optimal DNS configuration observed for "+ currBinding.HostName, body);

                                    }
                                } //if(currBinding.getDNSChain().Count == 1)
                                else
                                {
                                    //Either the name resolution is over CNAME record OR there was likely an error with name resolution tree building process however the DNS is currently resolving fine.. 
                                    //Does the CNAME resolution have sitename.azurewebsites.net in the URL ? If not, then the name resolution is still sub-optimal else things are good

                                    //If the site has TrafficManager enable, then the CNAME resolution should have trafficmanager somewhere in the chain.

                                    if(SiteCapabilites.isTrafficManagerEnabled)
                                    {
                                        
                                        var tmmatch = currBinding.getDNSChain().Where(dnsentry => dnsentry.Name.ToLower().Contains(".trafficmanager.net"));
                                        if(tmmatch.Any())
                                        {
                                            var body =  new Dictionary<string,string>();
                                            body["Insight"] = "The URL <strong>" + currBinding.HostName + "</strong> resolves to <strong> " + currentDNSIP.ToString() + "</strong> and is making use of name resolution via Traffic Manager.";
                                            body["Current DNS Configuration"] = "<markdown>Digweb output for `"+ currBinding.HostName + "` based on the current DNS settings.<br/><br/>" + currBinding.getDisplayForDNSChain() + "</markdown>";                                            
                                            res.AddInsight(InsightStatus.Success,"Hostname "+ currBinding.HostName + " utilizing Traffic Manager", body);
                                        }
                                        else
                                        {
                                            var body =  new Dictionary<string,string>();
                                            body["Insight"] = "The URL <strong>" + currBinding.HostName + "</strong> resolves to <strong> " + currentDNSIP.ToString() + "</strong> and is not utilizing Traffic Manager for name resolution even though the website has a traffic manager URL configured on it." ;
                                            body["Current DNS Configuration"] = "<markdown>Digweb output for `"+ currBinding.HostName + "` based on the current DNS settings.<br/><br/>" + currBinding.getDisplayForDNSChain() + "</markdown>";
                                            body["Recommendation"] = "<br/> Please update your DNS settings so that the URL <strong><i>" + currBinding.HostName + "</i></strong> resolves to <strong>" + correctIPToResolveTo.ToString() + "</strong> as per below. Alternatively, if you are no longer using Traffic Manager, remove the traffic manager hostnames from this site. <br/><br/>If you are intentionally not utilizing the traffic manager only for this HostName, kindly ignore the warning.";
                                            body["Recommended DNS Configuration"] = "<markdown>" + DnsUtilities.getRecommendedDNSConfiguration(currBinding, correctIPToResolveTo, cxt) + "</markdown>";
                                            res.AddInsight(InsightStatus.Warning,"Hostname "+ currBinding.HostName + " not utilizing Traffic Manager", body);
                                        }
                                    }
                                    
                                    //If the site has Traffic Manager enabled, then the CNAME resolution may not necessarily be for this website. It may end up resolving to one of the other sites configured via TM
                                    //Hence, not checking specifically for this site, but making sure that it somehow resolves to an Azure web app
                                    //string tempStr =  cxt.Resource.Name.ToLower() + ".azurewebsites.net";
                                    
                                    var azmatch = currBinding.getDNSChain().Where(dnsentry => dnsentry.Name.ToLower().Contains(".azurewebsites.net"));
                                    if(azmatch.Any())
                                    {
                                        //CNAME resolution for the hostname resolving to the correct IP with sitename.azurewebsites.net in the name resolution path
                                        var body = new Dictionary<string,string>();
                                        body["Insight"] = "The URL <strong>" + currBinding.HostName + "</strong> resolves to <strong> " + currentDNSIP.ToString() + "</strong> " + ((SiteCapabilites.isIpSSLEnabled)?" which is the static IP assigned to the site.":".");
                                        body["Current DNS Configuration"] = "<markdown>Digweb output for `"+ currBinding.HostName + "` based on the current DNS settings.<br/><br/>" + currBinding.getDisplayForDNSChain() + "</markdown>";
                                        res.AddInsight(InsightStatus.Success,"DNS Name resolution check passed for "+ currBinding.HostName, body);
                                    }
                                    else
                                    {
                                        //CNAME resolution for the hostname resolving to the correct IP but WITHOUT sitename.azurewebsites.net in the name resolution path
                                        var body = new Dictionary<string,string>();
                                        body["Insight"] = "The URL <strong>" + currBinding.HostName + "</strong> resolves to <strong> " + currentDNSIP.ToString() + "</strong> " + ((SiteCapabilites.isIpSSLEnabled)?" which is the static IP assigned to the site":" ") + ". However the name resolution is configured without pointing to <strong>" + ((string)siteData.name) + ".azurewebsites.net</strong> anywhere in the name resolution chain. <br/><br/>In case the  IP changes due to any reason " + ((SiteCapabilites.isIpSSLEnabled)? " (e.g.. Someone accidentally deleting the binding, thereby releasing the static IP and then re-acquiring it by creating a new binding or switching to a non static IP binding) ":" (e.g.. Although rare, the IP may change under certain circumstances during an Azure platform maintenance ) ") + " , it may cause the website to stop working. Since you do not have your URL pointing to <strong>" + ((string)siteData.name) + ".azurewebsites.net</strong> over CNAME, such an accidental IP change will not be transparent to you and may lead to downtime.";
                                        body["Current DNS Configuration"] = "<markdown>Digweb output for `"+ currBinding.HostName + "` based on the current DNS settings.<br/><br/>" + currBinding.getDisplayForDNSChain() + "</markdown>";
                                        body["Recommendation"] = "<br/> Please update your DNS settings so that the URL <strong><i>" + currBinding.HostName + "</i></strong> resolves to <strong>" + correctIPToResolveTo.ToString() + "</strong> as per below.";
                                        body["Recommended DNS Configuration"] = "<markdown>" + DnsUtilities.getRecommendedDNSConfiguration(currBinding, correctIPToResolveTo, cxt) + "</markdown>";
                                        res.AddInsight(InsightStatus.Warning,"Sub-optimal DNS configuration observed for "+ currBinding.HostName, body);
                                    }


                                } //else if(currBinding.getDNSChain().Count == 1)

                            } //if(match.Any())
                            else
                            {
                                var body = new Dictionary<string,string>();
                                //Instead of  currBinding.IP.ToString(), show a comma seperated list of Distinct IP's from hostNamesToProcess list
                                System.Net.IPAddress correctIP = System.Net.IPAddress.None;

                                if(SiteCapabilites.isIpSSLEnabled)
                                {
                                    //body["Insight"] = "As per the current DNS settings, URL <strong><i>" + currBinding.HostName + "</i></strong> resolves to <strong>" + currentDNSIP.ToString() + "</strong>. The webapp on Azure however is configured to listen on <strong>" + hostNamesToProcess.Where(kvp => kvp.Value.SSLType == SSLTypes.IP).Select( ipkvp => ipkvp.Value.IP).FirstOrDefault().ToString() + "</strong>.";
                                    body["Insight"] = "As per the current DNS settings, URL <strong><i>" + currBinding.HostName + "</i></strong> resolves to <strong>" + currentDNSIP.ToString() + "</strong>. The webapp on Azure however is configured to listen on <strong>" + IPSSL_IpsCSV + "</strong>.";
                                }
                                else
                                {
                                    body["Insight"] = "As per the current DNS settings, URL <strong><i>" + currBinding.HostName + "</i></strong> resolves to <strong>" + currentDNSIP.ToString() + "</strong>. The webapp on Azure however is configured to listen on <strong>" + currBinding.IP.ToString() + "</strong>.";
                                }
                              
                                body["Consequence"] =" Depending on where the current DNS points to, you might see different behaviors. <br/><br/><li/>If it points to an Azure WebApp endpoint, you will get the default *.azurewebsites.net certificate in place of your configured certificate and upon proceeding further you will get a 404 as the platform will fail to locate the webapp. <br/><li/>If the IP points to a non Azure WebApp endpoint, you will either see a failure to connect to the website or a 404.";
                                body["Current DNS Configuration"] = "<markdown>Digweb output for `"+ currBinding.HostName + "` based on your current DNS settings.<br/><br/>" + currBinding.getDisplayForDNSChain() + "</markdown>";
                                body["Recommendation"] = "<br/> Please update your DNS settings so that the URL <strong><i>" + currBinding.HostName + "</i></strong> resolves to <strong>" + correctIPToResolveTo.ToString() + "</strong> as per below.";
                                body["Recommended DNS Configuration"] = "<markdown>" + DnsUtilities.getRecommendedDNSConfiguration(currBinding, correctIPToResolveTo, cxt) + "</markdown>";
                                res.AddInsight(InsightStatus.Critical, "DNS resolution Error for " + currBinding.HostName, body);
                            } //else if(match.Any())
                        }//if(currentDNSIP.Equals(IPAddress.None))
                   }

                   

               }//foreach(BindingInfo currBinding in hostNamesToProcess.Values)
               //res.AddInsight(InsightStatus.Success, "Found " + hostNamesToProcess.Count + " custom hostnames");
               //res.AddInsight(InsightStatus.Critical, "Stamp : " + Stamp.Name + " IP :" + Stamp.IP.ToString() );
            } // if(hostNamesToProcess.Count > 0)


        } //if(!isFree)
        else
        {
            //Show a message indicating that SSL & Custom Domains are supported only for non free apps.
            var body = new Dictionary<string,string>();
            body["Insight"] = "You are currently running the site on a Free SKU. Supported SKU's are as follows <br/> <li> Custom domains : Shared SKU's and higher <li/> SNI SSL: Basic SKU's and higher <li/> IP SSL : Standard SKU's and higher ";
            body["Learn more"] = @"<br/><a target=""_blank"" href=""https://azure.microsoft.com/en-us/pricing/details/app-service/"">Azure App Service Plan Pricing Information</a>";
            res.AddInsight(InsightStatus.Critical, "Unsupported SKU for Custom Domains and SSL", body);
        }

    }
    catch(Exception ex)
    {
        var body = new Dictionary<string,string>();
        body["Exception"] = ex.GetType().ToString();
        body["StackTrace"] = ex.StackTrace;
        body["InnerException"] = ex.HResult.ToString();
        res.AddInsight(InsightStatus.Critical, ex.Message, body);
    }

    return res;
}    