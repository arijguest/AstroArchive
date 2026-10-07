// Screening reports file failures without deleting or guessing image quality.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed class ScreeningResult {public int Checked;public int Problems;public List<string> Errors=new List<string>();}
 public static class CaptureScreening {
  public static bool Importable(Frame frame){return frame.Status=="New"||frame.Status=="Restore"||frame.Status=="Failed";}
  public static bool NeedsReview(Frame frame){return frame.Rejected||FileProblem(frame)||!string.IsNullOrEmpty(frame.ScreeningIssue)||new[]{"Failed","Unreadable","Missing","Changed"}.Contains(frame.Status);}
  public static bool FileProblem(Frame frame){return !string.IsNullOrEmpty(frame.IntegrityIssue)||new[]{"Unreadable","Missing","Changed"}.Contains(frame.Status)||(!frame.Rejected&&!string.IsNullOrEmpty(frame.ScreeningIssue)&&frame.Status!="Failed");}
  public static string Category(Frame frame){
   var types=new List<string>();if(frame.Rejected)types.Add("Telescope rejected / reference");if(FileProblem(frame))types.Add("File integrity problem");if(frame.Status=="Failed")types.Add("Transfer failure");
   return types.Count>0?string.Join("; ",types):frame.Screened?"Screened, no issues":"Not screened";
  }
  public static string Reason(Frame frame){
   var reasons=new List<string>();if(frame.Rejected)reasons.Add(FirstReason(frame.RejectionReason,FileProblem(frame)?null:frame.ScreeningIssue,"Capture is marked rejected/reference."));
   if(FileProblem(frame))reasons.Add(FirstReason(frame.IntegrityIssue,frame.ScreeningIssue,"Archive status: "+frame.Status));
   if(frame.Status=="Failed")reasons.Add(FirstReason(frame.TransferIssue,frame.Notes,"Previous transfer failed. Retry after resolving the source problem."));
   return string.Join("\n",reasons.Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct());
  }
  static string FirstReason(params string[] reasons){return reasons.FirstOrDefault(s=>!string.IsNullOrWhiteSpace(s))??"";}
  public static string Rejection(FitsHeader header,string path){
   string location=(path??"").Replace('\\','/');
   if(System.Text.RegularExpressions.Regex.IsMatch(location,@"(?:^|[/_ .-])(failed|failure|rejected|reject|reference|weights?)(?:[/_ .-]|$)|solving_failed",System.Text.RegularExpressions.RegexOptions.IgnoreCase))return "Filename or folder marks a failed, rejected, reference or weight capture.";
   foreach(string key in new[]{"STATUS","CAPSTAT","IMGSTAT"}){
    string state=header.Get(key);
    if(System.Text.RegularExpressions.Regex.IsMatch(state,@"\b(failed|failure|rejected|reject|error)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase))return "FITS "+key+": "+state+".";
   }
   if(new[]{"T","TRUE","1","YES"}.Contains(header.Get("REJECTED").ToUpperInvariant()))return "FITS header marks this capture rejected.";
   return "";
  }
 }
 public sealed partial class Repository {
  public ScreeningResult Screen(IEnumerable<Frame> selection,bool imports,CancellationToken ct,Action<ProgressInfo> progress){
   var result=new ScreeningResult();var frames=selection.Where(f=>f.Status!="Deleted").ToList();
   var indexed=All().ToDictionary(f=>f.Hash);
   try{for(int i=0;i<frames.Count;i++){
    ct.ThrowIfCancellationRequested();Frame frame=frames[i];
    bool source=imports&&(CaptureScreening.Importable(frame)||frame.Status=="Unreadable"||frame.Status=="Duplicate in source");
    string path=frame.SourcePath;string issue="";
    if(progress!=null)progress(new ProgressInfo{Done=i,Total=frames.Count,Stage="Screening files",Text=frame.OriginalName});
    try{
     path=source?frame.SourcePath:FilePath(frame);
     var before=FileStamp.Read(path);
     if(source&&frame.SourceStamp!=null&&!before.ContentSame(frame.SourceStamp))throw new InvalidDataException("Source changed since scanning. Scan the folder again.");
     var header=Fits.Validate(path,ct);
     if(!source&&Util.Hash(path,ct)!=frame.Hash){frame.Status="Changed";throw new InvalidDataException("Archive checksum differs from the imported file.");}
     var after=FileStamp.Read(path);if(!before.ContentSame(after))throw new InvalidDataException("File changed during screening. Screen again.");
     issue=CaptureScreening.Rejection(header,source?path:frame.OriginalName);
     if(frame.Rejected&&issue.Length==0)issue="Capture is marked rejected/reference; excluded from stacking by default.";
     if(issue.Length>0){frame.Rejected=true;frame.RejectionReason=issue;}
     if(source&&frame.Status=="Unreadable"){
      string sourceRoot=frame.SourceRoot;var recovered=Classifier.Read(path,sourceRoot,frame.TelescopeIdentity??frame.Telescope,frame.Model);recovered.Telescope=frame.Telescope;
      // Recover complete metadata before offering a formerly unreadable file for import.
      foreach(var property in typeof(Frame).GetProperties().Where(p=>p.CanWrite))property.SetValue(frame,property.GetValue(recovered,null),null);
      frame.SourceRoot=sourceRoot;
     }
     frame.IntegrityIssue=null;if(!source){frame.Status="Verified";frame.RepositoryStamp=after;}
    }catch(OperationCanceledException){throw;}catch(Exception e){issue=FileRetry.Detail(path,e);frame.IntegrityIssue=issue;if(!source&&frame.Status!="Changed")frame.Status=File.Exists(path)?"Unreadable":"Missing";if(source&&frame.Status!="Failed")frame.Status="Unreadable";}
    frame.Screened=true;frame.ScreeningIssue=issue;result.Checked++;
    if(CaptureScreening.NeedsReview(frame)){result.Problems++;result.Errors.Add(frame.OriginalName+": "+(issue.Length>0?issue:"Previous import failed. Retry after resolving the import error."));}
    if(source)Manifest(new SourceManifest{Root=frame.SourceRoot,Path=frame.SourcePath,Hash=frame.Hash,Source=frame.SourceStamp,Status=frame.Status=="Unreadable"||frame.Status=="Failed"?"Failed":"Screened",Metadata=frame});
    else if(!string.IsNullOrEmpty(frame.Hash)&&indexed.ContainsKey(frame.Hash))Save(frame);
   }}finally{Checkpoint(CancellationToken.None);}
   if(progress!=null)progress(new ProgressInfo{Done=frames.Count,Total=frames.Count,Stage="Screening files",Text=result.Problems+" files need review"});return result;
  }
 }
}
