using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AstroArchive.Installation;

namespace AstroArchive {
 public sealed class InformationCoordinator {
  readonly object gate=new object();readonly Dictionary<string,Task<DownloadedInformation>> requests=new Dictionary<string,Task<DownloadedInformation>>();
  readonly Dictionary<string,DateTime> retry=new Dictionary<string,DateTime>();readonly Dictionary<string,DateTime> providerRetry=new Dictionary<string,DateTime>();
  readonly InformationCache cache;CancellationTokenSource lifetime=new CancellationTokenSource();
  public InformationCoordinator(InformationCache cache){this.cache=cache;}
  public static string Provider(TargetInfoCard card){return card.domain=="solar_system"&&OrbitalCatalog.ProviderId(card.id)!=null?"JPL":card.domain=="deep_sky"?"Wikipedia":null;}
  public DownloadedInformation Saved(TargetInfoCard card,string language){string provider=Provider(card);var record=provider==null?null:cache.Read(provider,card.id,language);InformationMedia.DecodeSaved(record);return record;}
  public async Task<DownloadedInformation> Enrich(TargetInfoCard card,string language,bool refresh=false){
   var saved=await Task.Run(()=>Saved(card,language));return await Request(card,language,refresh,saved);
  }
  Task<DownloadedInformation> Request(TargetInfoCard card,string language,bool refresh,DownloadedInformation saved){
   string provider=Provider(card);if(provider==null||!ConnectivityPolicy.AllowsInternet)return Task.FromResult<DownloadedInformation>(null);
   DateTime expiry;if(!refresh&&saved!=null&&DateTime.TryParse(saved.expires_at,out expiry)&&expiry.ToUniversalTime()>DateTime.UtcNow)return Task.FromResult(saved);
   if(!refresh){
    var bundled=provider=="JPL"?OrbitalCatalog.For(card):null;DateTime retrieved;
    if(bundled!=null&&DateTime.TryParse(bundled.rights.retrieved_at,out retrieved)&&retrieved.ToUniversalTime().AddDays(7)>DateTime.UtcNow)return Task.FromResult(saved);
    var prose=TargetDescriptions.For(card.id);if(provider=="Wikipedia"&&prose!=null&&!string.IsNullOrWhiteSpace(prose.Overview)&&card.Thumbnail!=null)return Task.FromResult(saved);
   }
   string key=InformationCache.Lookup(provider,card.id,language);lock(gate){
    DateTime due;if(retry.TryGetValue(key,out due)&&due>DateTime.UtcNow||providerRetry.TryGetValue(provider,out due)&&due>DateTime.UtcNow)return Task.FromResult(saved);
    Task<DownloadedInformation> task;if(requests.TryGetValue(key,out task))return task;if(requests.Count>=4)return Task.FromResult(saved);
    int generation=cache.Generation;var cancellation=CancellationTokenSource.CreateLinkedTokenSource(ConnectivityPolicy.Token,lifetime.Token);
    task=Task.Run(()=>{
     try{cancellation.Token.ThrowIfCancellationRequested();ConnectivityPolicy.RequireInternet();var record=provider=="JPL"?InformationProviders.Sbdb(card.id,language,cancellation.Token):InformationProviders.Wikipedia(card,language,cancellation.Token);
      if(record==null&&provider=="Wikipedia")record=InformationProviders.ReviewedMedia(card,language,cancellation.Token);
      cancellation.Token.ThrowIfCancellationRequested();if(record!=null){cache.Write(record,generation);return record;}lock(gate)retry[key]=DateTime.UtcNow.AddHours(6);return saved;
     }catch(InformationRateLimit error){lock(gate){providerRetry[provider]=error.RetryUtc;retry[key]=error.RetryUtc;}return saved;}
     catch(OperationCanceledException){return saved;}
     catch(Exception){lock(gate)retry[key]=DateTime.UtcNow.AddMinutes(15);return saved;}
     finally{cancellation.Dispose();}
    });requests[key]=task;
    task.ContinueWith(done=>{lock(gate){Task<DownloadedInformation> current;if(requests.TryGetValue(key,out current)&&object.ReferenceEquals(current,done))requests.Remove(key);if(retry.Count>1000)retry.Clear();}},TaskScheduler.Default);return task;
   }
  }
  public void Cancel(){CancellationTokenSource old;lock(gate){old=lifetime;lifetime=new CancellationTokenSource();requests.Clear();}old.Cancel();}
  public void Clear(){Cancel();cache.Clear();lock(gate)retry.Clear();}
  public static TargetInfoCard Merge(TargetInfoCard local,DownloadedInformation saved){
   var card=Clone(local);if(saved==null||!saved.Valid(local.id))return card;var curated=TargetDescriptions.For(local.id);
   if(saved.summary!=null&&(curated==null||string.IsNullOrWhiteSpace(curated.Overview))&&(local.review_status=="draft"||string.IsNullOrWhiteSpace(local.summary)||local.content_tier=="basic"&&local.summary.Contains("(IAU constellation code)"))){card.summary=saved.summary;card.text_rights=saved.text_rights;card.identity_rights=saved.identity_rights;}
   if(saved.orbit!=null)card.orbit=saved.orbit;
   if(saved.measurements!=null){var fields=new HashSet<string>((card.measurements??new TargetInfoMeasurement[0]).Select(m=>m.field));card.measurements=(card.measurements??new TargetInfoMeasurement[0]).Concat(saved.measurements.Where(m=>!fields.Contains(m.field))).ToArray();card.citations=(card.citations??new TargetInfoCitation[0]).Concat(saved.citations??new TargetInfoCitation[0]).GroupBy(c=>c.id).Select(g=>g.First()).ToArray();card.measurement_rights=saved.measurement_rights;}
   if(card.Thumbnail==null&&saved.thumbnail!=null){card.image=saved.image;card.Thumbnail=saved.thumbnail;card.DecodedThumbnail=saved.DecodedThumbnail;}
   card.downloaded_at=saved.retrieved_at;return card;
  }
  public static TargetInfoCard Clone(TargetInfoCard c){return new TargetInfoCard{id=c.id,name=c.name,domain=c.domain,type_label=c.type_label,constellation=c.constellation,parent_body=c.parent_body,summary=c.summary,fact=c.fact,review_status=c.review_status,identity_status=c.identity_status,content_tier=c.content_tier,aliases=c.aliases,source_urls=c.source_urls,measurements=c.measurements,citations=c.citations,image=c.image,Thumbnail=c.Thumbnail,DecodedThumbnail=c.DecodedThumbnail,orbit=c.orbit,measurement_rights=c.measurement_rights,text_rights=c.text_rights,identity_rights=c.identity_rights,downloaded_at=c.downloaded_at};}
 }
}
