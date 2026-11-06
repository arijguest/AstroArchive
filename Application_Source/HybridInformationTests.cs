using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using AstroArchive.Installation;

namespace AstroArchive {
 public partial class Tests {
  static OrbitRecord TestOrbit(double eccentricity=0){return new OrbitRecord{object_id="fixture",central_body="Sun",reference_plane="ecliptic",coordinate_frame="J2000",time_standard="TDB",epoch=2451545,e=eccentricity,q=1,i=0,ascending_node=0,argument_of_perihelion=0,perihelion_time=2451545,valid_from=2400000,valid_to=2500000,source="https://ssd.jpl.nasa.gov/planets/approx_pos.html",solution_id="analytic fixture",rights=OrbitalCatalog.Rights("https://ssd.jpl.nasa.gov/planets/approx_pos.html","analytic numerical fixture",DateTime.UtcNow,"planetary-approximation")};}
  static void Near(double value,double expected,double tolerance,string label){Check(Math.Abs(value-expected)<=tolerance,label+": "+value+" expected "+expected);}
  static object SbdbFixture(){using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SbdbFixture.json"))using(var reader=new StreamReader(stream))return new JavaScriptSerializer().DeserializeObject(reader.ReadToEnd());}
  static void RejectInformation(Action action){bool denied=false;try{action();}catch(InvalidDataException){denied=true;}Check(denied,"Invalid provider data was accepted");}
  static void HybridInformationTests(){
   Test("Downloaded media credits preserve creator and acknowledgement links without changing prose normalization",()=>{
    var metadata=new System.Collections.Generic.Dictionary<string,object>{{"Artist",new System.Collections.Generic.Dictionary<string,object>{{"value","<a href='https://authors.example/Alice'>Alice</a>"}}},{"Credit",new System.Collections.Generic.Dictionary<string,object>{{"value","<div>Acknowledgement: <a href='https://observatory.example/team?a=1&amp;b=2'>Carol &amp; Team</a></div>"}}}};
    string shown=InformationMedia.Attribution(metadata,"Reviewed source attribution.");
    Check(shown.Contains("Reviewed source attribution.")&&shown.Contains("Alice (https://authors.example/Alice)")&&shown.Contains("Carol & Team (https://observatory.example/team?a=1&b=2)"),"Downloaded attribution lost a required contributor link");
    Check(!shown.Contains("<a")&&!shown.Contains("&amp;")&&InformationProviders.Plain("<a href='https://example.test'>Plain article label</a>")=="Plain article label","Media formatting changed general article prose handling");
   });
   Test("SBDB validates exact identity, fragments, API version, units and full precision",()=>{
    var fixture=InformationProviders.Map(SbdbFixture());var result=InformationProviders.ParseSbdb(fixture,"1P",DateTime.UtcNow);Near(result.e,0.9679359956953211,1e-15,"JPL full precision");Check(result.provider_id=="1000036"&&result.solution_id=="75"&&result.uncertainties.Count>=7,"Solution provenance lost");
    RejectInformation(()=>InformationProviders.ParseSbdb(fixture,"12P",DateTime.UtcNow));InformationProviders.Map(fixture["signature"])["version"]="99";RejectInformation(()=>InformationProviders.ParseSbdb(fixture,"1P",DateTime.UtcNow));
    fixture=InformationProviders.Map(SbdbFixture());var orbit=InformationProviders.Map(fixture["orbit"]);var elements=(object[])orbit["elements"];InformationProviders.Map(elements.Single(e=>InformationProviders.Text(InformationProviders.Map(e),"name")=="q"))["units"]="km";RejectInformation(()=>InformationProviders.ParseSbdb(fixture,"1P",DateTime.UtcNow));
    var obj=InformationProviders.Map(fixture["object"]);obj["des"]="73P-B";Check(InformationProviders.SbdbIdentity("73P-B",obj)&&!InformationProviders.SbdbIdentity("73P",obj),"Comet fragment identity collapsed");
   });
   Test("Circular/elliptic dated positions agree with independent analytic apsides",()=>{
    var o=TestOrbit();double period=2*Math.PI/Math.Sqrt(OrbitalModel.SunMu);OrbitPoint p;Check(OrbitalModel.Position(o,o.epoch+period/4,out p),"Circular marker missing");Near(p.X,0,1e-9,"quarter period X");Near(p.Y,1,1e-9,"quarter period Y");
    o=TestOrbit(0.5);period=2*Math.PI*Math.Sqrt(8/OrbitalModel.SunMu);Check(OrbitalModel.Position(o,o.epoch+period/2,out p),"Elliptic marker missing");Near(p.X,-3,1e-9,"aphelion X");Near(p.Y,0,1e-9,"aphelion Y");
    o.perihelion_time=null;o.mean_anomaly=90;o.e=0;Check(OrbitalModel.Position(o,o.epoch,out p,true),"Mean anomaly marker missing");Near(p.Y,1,1e-9,"mean anomaly 90");
   });
   Test("Parabolic and near-parabolic positions retain eccentricity and Barker reference",()=>{
    double elapsed=Math.Sqrt(2/OrbitalModel.SunMu)*4/3;OrbitPoint p;
    foreach(double eccentricity in new[]{1.0,1-1e-9,1+1e-9}){var o=TestOrbit(eccentricity);Check(OrbitalModel.Position(o,o.epoch+elapsed,out p),"Near-parabolic position missing");Near(p.X,0,1e-7,"Barker x");Near(p.Y,2,1e-7,"Barker y");Check(o.e==eccentricity,"Eccentricity rounded to a different conic");}
    var hyper=TestOrbit(1.5);Check(OrbitalModel.Position(hyper,hyper.epoch,out p),"Hyperbolic perihelion missing");Near(p.X,1,1e-10,"hyperbolic q");
   });
   Test("3D orbit planes preserve inclination and retrograde orientation",()=>{
    var o=TestOrbit();o.i=90;var p=OrbitalModel.AtAnomaly(o,Math.PI/2);Near(p.Z,1,1e-10,"inclined z");Near(p.Y,0,1e-10,"inclined y");o.i=180;p=OrbitalModel.AtAnomaly(o,Math.PI/2);Near(p.Y,-1,1e-10,"retrograde y");
    o.i=0;o.ascending_node=90;p=OrbitalModel.AtAnomaly(o,0);Near(p.Y,1,1e-10,"ascending node");
   });
   Test("Bounded paths close ellipses, leave open conics open, and fit extreme extents",()=>{
    foreach(double e in new[]{0.0,0.75,1-1e-12,1.0,1+1e-12,4.0}){var o=TestOrbit(e);var full=OrbitalModel.Path(o,false,CancellationToken.None);var inner=OrbitalModel.Path(o,true,CancellationToken.None);
     Check(full.Points.Length>=65&&full.Points.Length<=8192&&full.Points.All(p=>OrbitalModel.Finite(p.X)&&OrbitalModel.Finite(p.Y)&&OrbitalModel.Finite(p.Z)),"Unbounded/non-finite path");Check(full.Closed==(e<1),"Conic closed incorrectly");Check(inner.Extent<=40,"Inner view lost perihelion detail");if(e<1)Near(full.Points.First().X,full.Points.Last().X,1e-8,"ellipse closure");else Check(full.Clipped&&!full.Closed,"Open path lacks continuation state");}
    using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();bool stopped=false;try{OrbitalModel.Path(TestOrbit(),false,cancelled.Token);}catch(OperationCanceledException){stopped=true;}Check(stopped,"Sampling ignored cancellation");}
   });
   Test("Missing frames, invalid elements, unsupported dates and unknown rights fail closed",()=>{
    var o=TestOrbit();o.coordinate_frame="ICRF";Check(!OrbitalModel.Valid(o),"Unknown frame accepted");o.coordinate_frame="J2000";o.time_standard="UTC";Check(!OrbitalModel.Valid(o),"UTC elements treated as TDB");o.time_standard="TDB";o.q=double.NaN;Check(!OrbitalModel.Valid(o),"NaN accepted");o.q=1;o.rights=null;Check(!OrbitalModel.Valid(o),"Unlicensed orbit accepted");
    o=TestOrbit();o.valid_from=o.valid_to=null;OrbitPoint p;Check(!OrbitalModel.Position(o,o.epoch+1,out p)&&OrbitalModel.Position(o,o.epoch,out p,true),"Unsupported current position invented");Check(OrbitalModel.Dynamical(new DateTime(2100,1,1,0,0,0,DateTimeKind.Utc))==null,"Future UTC leap seconds assumed");
    Near((OrbitalModel.Dynamical(new DateTime(2026,10,13,0,0,0,DateTimeKind.Utc)).Value-OrbitalModel.Julian(new DateTime(2026,10,13,0,0,0,DateTimeKind.Utc)))*86400,69.184,0.00005,"UTC to TT");
   });
   Test("Audited essential solutions use complete elements and epoch-only small bodies",()=>{
    foreach(string id in new[]{"1P","12P","67P","C/2020 F3","C/2023 A3","2I","3I","CERES","PLUTO","MOON"}){var orbit=OrbitalCatalog.For(new TargetInfoCard{id=id,domain="solar_system"});Check(orbit!=null&&OrbitalModel.Valid(orbit)&&orbit.perihelion_time.HasValue&&orbit.rights.source_revision.Length>0,"Incomplete snapshot: "+id);OrbitPoint p;Check(OrbitalModel.Position(orbit,orbit.epoch,out p,true),"Epoch marker missing: "+id);Check(!OrbitalModel.Position(orbit,orbit.epoch+1,out p),"Current validity assumed: "+id);}
    Check(OrbitalCatalog.For(new TargetInfoCard{id="IO",domain="solar_system"})==null,"Satellite orientation invented");Check(OrbitalCatalog.For(new TargetInfoCard{id="1P",domain="solar_system"}).i>90,"Halley's retrograde orientation lost");
    Check(CometInfo.InformationDesignation("73P-B/Schwassmann-Wachmann")=="73P-B"&&CometInfo.InformationDesignation("C/2020 F3-A (fragment)")=="C/2020 F3-A","Comet fragment merged with parent");
   });
   Test("Moon epoch marker agrees with the independent Horizons true-anomaly reference",()=>{
    var moon=OrbitalCatalog.For(new TargetInfoCard{id="MOON",domain="solar_system"});OrbitPoint p;Check(OrbitalModel.Position(moon,moon.epoch,out p,true),"Moon marker missing");
    Near(p.X,-0.0018083600149854848,1e-12,"Horizons Moon X");
    Near(p.Y,-0.0019238922721514964,1e-12,"Horizons Moon Y");
    Near(p.Z,-0.00023077883532085408,1e-12,"Horizons Moon Z");
   });
   Test("Orbit viewer helpers choose round rings, show the right units and keep frames, easing and playback pace sound",()=>{
    foreach(double raw in new[]{1e-7,0.00031,0.0149,0.7,1,1.49,1.5,2.9,3.5,7.4,7.5,9.99,35,4100,2.2e6}){double step=OrbitMath.NiceStep(raw),power=Math.Pow(10,Math.Floor(Math.Log10(step)+1e-9)),mantissa=step/power;Check(Math.Abs(mantissa-1)<1e-9||Math.Abs(mantissa-2)<1e-9||Math.Abs(mantissa-5)<1e-9,"Ring step is not 1, 2 or 5 times a power of ten: "+raw+" -> "+step);Check(step/raw>0.55&&step/raw<1.5,"Ring step strays from the wanted spacing: "+raw+" -> "+step);}
    Near(OrbitMath.NiceStep(0.7),0.5,1e-12,"Nice step");Near(OrbitMath.NiceStep(0.0003),0.0002,1e-15,"Nice step small");Check(OrbitMath.NiceStep(0)==1&&OrbitMath.NiceStep(-3)==1&&OrbitMath.NiceStep(double.NaN)==1,"Nice step lost its fallback");
    Check(OrbitMath.Distance(1)=="1 AU"&&OrbitMath.Distance(0.5)=="0.5 AU"&&OrbitMath.Distance(30.07)=="30.07 AU"&&OrbitMath.Distance(250)=="250 AU","AU distances misformatted");
    Check(OrbitMath.Distance(0.0001)=="14,960 km"&&OrbitMath.Distance(0.00000005)=="7.5 km"&&OrbitMath.Kilometres(0.0026)&&!OrbitMath.Kilometres(0.02),"Earth-scale distances not shown in km");
    var pole=OrbitMath.Ecliptic(0,-Math.Sin(OrbitMath.Obliquity),Math.Cos(OrbitMath.Obliquity));Near(pole.X,0,1e-12,"Ecliptic pole X");Near(pole.Y,0,1e-12,"Ecliptic pole Y");Near(pole.Z,1,1e-12,"Ecliptic pole Z");
    var equinox=OrbitMath.Ecliptic(1,0,0);Check(equinox.X==1&&equinox.Y==0&&equinox.Z==0,"The equinox moved between frames");var turned=OrbitMath.Ecliptic(0.3,-0.5,0.8);Near(turned.Length,Math.Sqrt(0.09+0.25+0.64),1e-12,"Frame change altered length");
    Check(OrbitMath.Ease(0)==0&&OrbitMath.Ease(1)==1&&OrbitMath.Ease(-1)==0&&OrbitMath.Ease(2)==1,"Easing is not clamped");Near(OrbitMath.Ease(0.5),0.875,1e-12,"Ease-out midpoint");double last=-1;for(int i=0;i<=20;i++){double e=OrbitMath.Ease(i/20.0);Check(e>=last,"Easing went backwards");last=e;}
    Near(OrbitMath.Angle(0,90,0.5),45,1e-12,"Angle midpoint");Near(Math.IEEERemainder(OrbitMath.Angle(350,10,0.5),360),0,1e-9,"Angle went the long way round");Near(Math.IEEERemainder(OrbitMath.Angle(10,350,0.5),360),0,1e-9,"Angle went the long way back");Near(OrbitMath.Angle(30,200,1),200,1e-9,"Angle end");
    Near(OrbitMath.Period(TestOrbit(0)),365.2568983,1e-3,"Circular period at 1 AU");Check(double.IsNaN(OrbitMath.Period(TestOrbit(1)))&&double.IsNaN(OrbitMath.Period(TestOrbit(4)))&&double.IsNaN(OrbitMath.Period(null)),"An open orbit was given a period");
    var moon=OrbitalCatalog.For(new TargetInfoCard{id="MOON",domain="solar_system"});double month=OrbitMath.Period(moon);Check(month>26&&month<29,"Moon period is not about a month: "+month);
    Check(OrbitalCatalog.PlanetName("MARS")=="Mars"&&OrbitalCatalog.Planets.Length==8&&OrbitalCatalog.Planets[2]=="EARTH","Planet names changed");
   });
   Test("Planet positions at a date match the planetary record built for that date",()=>{
    foreach(var when in new[]{new DateTime(1985,3,9,6,0,0,DateTimeKind.Utc),new DateTime(2026,10,13,12,0,0,DateTimeKind.Utc)})foreach(string id in OrbitalCatalog.Planets){
     OrbitPoint direct,viaRecord;var record=PlanetEphemeris.Orbit(id,when);Check(PlanetEphemeris.Position(id,when,out direct)&&record!=null&&OrbitalModel.Position(record,OrbitalModel.Julian(when),out viaRecord),"Planet position missing: "+id);
     OrbitalModel.Position(record,OrbitalModel.Julian(when),out viaRecord);Near(direct.X,viaRecord.X,2e-6,id+" X");Near(direct.Y,viaRecord.Y,2e-6,id+" Y");Near(direct.Z,viaRecord.Z,2e-6,id+" Z");}
    OrbitPoint earth,mars,none;PlanetEphemeris.Position("earth",new DateTime(2026,10,13,12,0,0,DateTimeKind.Utc),out earth);PlanetEphemeris.Position("MARS",new DateTime(2026,10,13,12,0,0,DateTimeKind.Utc),out mars);Near(earth.Length,1,0.02,"Earth distance from the Sun");Check(mars.Length>1.3&&mars.Length<1.7,"Mars distance from the Sun");
    Check(!PlanetEphemeris.Position("MOON",DateTime.UtcNow,out none)&&!PlanetEphemeris.Position("MARS",new DateTime(1799,12,31,0,0,0,DateTimeKind.Utc),out none)&&!PlanetEphemeris.Position("MARS",new DateTime(2051,1,1,0,0,0,DateTimeKind.Utc),out none),"Unsupported planet or date produced a position");
   });
   Test("Provider security rejects local URLs, unsupported rights and oversized rasters",()=>{
    foreach(string url in new[]{"http://ssd-api.jpl.nasa.gov/sbdb.api","https://ssd-api.jpl.nasa.gov:444/","https://ssd-api.jpl.nasa.gov.evil.example/","https://localhost/","https://127.0.0.1/","https://user@en.wikipedia.org/"})Check(!InformationHttp.Trusted(new Uri(url)),"Unsafe URL accepted: "+url);
    foreach(string ip in new[]{"127.0.0.1","10.1.1.1","172.16.0.1","192.168.1.1","169.254.0.1","::1","fc00::1","::ffff:127.0.0.1"})Check(InformationHttp.Private(System.Net.IPAddress.Parse(ip)),"Private address accepted");
    Check(!InformationHttp.Raster(new byte[25])&&!InformationRights.Permitted(new InformationRights{provider="SIMBAD",dataset="unapproved"},true),"Unsafe content accepted");
    DateTime now=DateTime.UtcNow;Check(InformationHttp.RetryAfter("3600",now)==now.AddHours(1)&&InformationHttp.RetryAfter("broken",now)>=now.AddMinutes(1),"Retry-After not honored");
   });
   Test("Rights-approved cache retains revision, recovers corruption and blocks late writes after clear",()=>{
    string folder=Path.Combine(root,"information-cache-fixture");var cache=new InformationCache(folder);var orbit=TestOrbit();var record=new DownloadedInformation{provider="JPL",object_id=orbit.object_id,provider_id="fixture",language="en",revision="solution:1",retrieved_at=DateTime.UtcNow.ToString("o"),expires_at=DateTime.UtcNow.AddDays(7).ToString("o"),orbit=orbit};int generation=cache.Generation;cache.Write(record,generation);
    Check(cache.Read("JPL","fixture","en").orbit.rights.source_revision==orbit.rights.source_revision,"Cached rights lost");File.WriteAllText(Directory.GetFiles(folder,"*.json").Single(),"damaged");Check(cache.Read("JPL","fixture","en")==null,"Corrupt cache displayed");cache.Clear();cache.Write(record,generation);Check(cache.Usage==0,"Late response repopulated cleared cache");
    record.orbit.rights=null;bool rejected=false;try{cache.Write(record,cache.Generation);}catch(InvalidDataException){rejected=true;}Check(rejected,"Unknown permission cached");
   });
   Test("Offline/Pending information selection causes zero transport calls",()=>{
    var original=InformationHttp.Fetch;var previous=ConnectivityPolicy.Mode;int calls=0;InformationHttp.Fetch=(uri,limit,token)=>{calls++;return new byte[0];};try{
     var cache=new InformationCache(Path.Combine(root,"offline-cache"));var coordinator=new InformationCoordinator(cache);var card=new TargetInfoCard{id="12P",domain="solar_system"};
     foreach(var mode in new[]{ConnectivityMode.Pending,ConnectivityMode.Offline}){ConnectivityPolicy.Apply(mode);Check(coordinator.Enrich(card,"en",true).Result==null,"Offline enrichment returned remote content");bool blocked=false;try{InformationHttp.Json(new Uri("https://ssd-api.jpl.nasa.gov/sbdb.api"),CancellationToken.None);}catch(InvalidOperationException){blocked=true;}Check(blocked&&calls==0,"Pending/Offline fetched information");}
    }finally{InformationHttp.Fetch=original;ConnectivityPolicy.Apply(previous);}
   });
   Test("Concurrent enrichment deduplicates transport and provider failures back off",()=>{
    var original=InformationHttp.Fetch;var previous=ConnectivityPolicy.Mode;try{ConnectivityPolicy.Apply(ConnectivityMode.Hybrid);int calls=0;string fixture=new JavaScriptSerializer().Serialize(SbdbFixture());
     InformationHttp.Fetch=(uri,limit,token)=>{Interlocked.Increment(ref calls);if(token.WaitHandle.WaitOne(200))token.ThrowIfCancellationRequested();return System.Text.Encoding.UTF8.GetBytes(fixture);};
     var cache=new InformationCache(Path.Combine(root,"deduplicated-cache"));var coordinator=new InformationCoordinator(cache);var card=new TargetInfoCard{id="1P",domain="solar_system"};var tasks=Enumerable.Range(0,4).Select(i=>coordinator.Enrich(card,"en",true)).ToArray();Task.WaitAll(tasks);Check(calls==1&&tasks.All(t=>t.Result!=null&&t.Result.orbit.object_id=="1P"),"Concurrent requests were not shared");
     coordinator.Clear();calls=0;InformationHttp.Fetch=(uri,limit,token)=>{calls++;throw new InformationRateLimit(DateTime.UtcNow.AddHours(1));};Check(coordinator.Enrich(card,"en",true).Result==null,"Failed provider replaced local information");coordinator.Enrich(card,"en",true).Wait();Check(calls==1,"Retry-After was ignored on repeated selection");
    }finally{InformationHttp.Fetch=original;ConnectivityPolicy.Apply(previous);}
   });
   Test("LRU evicts old data within the cap while protecting the displayed card",()=>{
    string folder=Path.Combine(root,"lru-cache");var cache=new InformationCache(folder){LimitMiB=10};var orbit=TestOrbit();var record=new DownloadedInformation{provider="JPL",object_id=orbit.object_id,provider_id="fixture",language="en",revision="active",retrieved_at=DateTime.UtcNow.ToString("o"),expires_at=DateTime.UtcNow.AddDays(7).ToString("o"),orbit=orbit};cache.Write(record,cache.Generation);cache.Protect(InformationCache.Lookup("JPL","fixture","en"),true);cache.Protect(InformationCache.Lookup("JPL","fixture","en"),true);cache.Protect(InformationCache.Lookup("JPL","fixture","en"),false);File.SetLastWriteTimeUtc(Directory.GetFiles(folder,"*.json").Single(),DateTime.UtcNow.AddDays(-3));
    string older=Path.Combine(folder,Util.HashText("older")+".json"),newer=Path.Combine(folder,Util.HashText("newer")+".json");File.WriteAllBytes(older,new byte[5*1048576]);File.WriteAllBytes(newer,new byte[6*1048576]);File.SetLastWriteTimeUtc(older,DateTime.UtcNow.AddDays(-2));File.SetLastWriteTimeUtc(newer,DateTime.UtcNow.AddDays(-1));cache.Trim();Check(!File.Exists(older)&&File.Exists(newer)&&cache.Usage<=10*1048576&&cache.Read("JPL","fixture","en")!=null,"LRU/cap/active protection failed");
   });
  }
 }
}
