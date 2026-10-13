using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace AstroArchive {
 // A small rolling log of what the app did, kept on this PC so a problem report can include
 // the lead-up to an error, even after a crash or restart. Paths are removed only when a
 // report is prepared; the local file keeps them for the user's own troubleshooting.
 public static class AppLog {
  const long Limit=512*1024;const int Memory=300;
  static readonly object gate=new object();static readonly Queue<string> recent=new Queue<string>();
  static string file;
  public static string FilePath{get{return file;}}
  public static void Start(string folder){
   lock(gate){try{Directory.CreateDirectory(folder);file=Path.Combine(folder,"astroarchive.log");var info=new FileInfo(file);if(info.Exists&&info.Length>Limit){string previous=Path.Combine(folder,"astroarchive.previous.log");File.Copy(file,previous,true);File.Delete(file);}}catch{file=null;}}
  }
  public static void Stop(){lock(gate)file=null;}
  public static void Info(string text){Write("INFO",text);}
  public static void Warn(string text){Write("WARN",text);}
  public static void Error(string context,Exception error){Write("ERROR",(context??"Error")+(error==null?"":": "+error.GetType().FullName+": "+error.Message+(error.StackTrace==null?"":"\n"+error.StackTrace)));}
  static void Write(string level,string text){
   string stamp=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture);
   string line=stamp+" "+level+" "+(text??"").Trim().Replace("\r\n","\n").Replace("\n","\n    ");
   lock(gate){recent.Enqueue(line);while(recent.Count>Memory)recent.Dequeue();if(file==null)return;try{File.AppendAllText(file,line+Environment.NewLine,Encoding.UTF8);}catch{}}
  }
  // The newest lines, oldest first: from the log file when there is one (so earlier sessions
  // count), otherwise from this session's memory.
  public static string Tail(int lines){
   lock(gate){
    var text=new List<string>();
    try{if(file!=null&&File.Exists(file)){string previous=Path.Combine(Path.GetDirectoryName(file),"astroarchive.previous.log");if(File.Exists(previous))text.AddRange(File.ReadAllLines(previous,Encoding.UTF8));text.AddRange(File.ReadAllLines(file,Encoding.UTF8));}}catch{text.Clear();}
    if(text.Count==0)text.AddRange(recent.SelectMany(l=>l.Split('\n')));
    return string.Join("\n",text.Skip(Math.Max(0,text.Count-lines)));
   }
  }
 }
 // One part of a problem report. The user chooses which parts to send; long parts keep their
 // newest text when the report must be shortened to fit a GitHub link.
 public sealed class ReportSection {
  public string Title,Text;public bool Include=true;public bool KeepEnd;public int Cap=4000;
 }
 internal static class AppErrorReporting {
  const string IssueBase="https://github.com/arijguest/AstroArchive/issues/new";
  // GitHub and browsers handle links up to about 8,000 characters; stay well inside that.
  internal const int UrlLimit=7200;

  internal static string Version{get{var assembly=Assembly.GetExecutingAssembly();var file=assembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute),false).OfType<AssemblyFileVersionAttribute>().FirstOrDefault();return assembly.GetName().Version+(file==null?"":" (package "+file.Version+")");}}

  internal static string Diagnostics(Exception error,string context){
   var report=new StringBuilder();
   report.AppendLine("Application: AstroArchive "+Assembly.GetExecutingAssembly().GetName().Version);
   report.AppendLine("Operating system: "+Environment.OSVersion.VersionString);
   report.AppendLine("Context: "+Sanitize(context??"User-submitted problem"));
   if(error==null)report.AppendLine("Exception: None captured");
   else{
    report.AppendLine("Exception details:");
    report.Append(Sanitize(error.ToString()));
   }
   string result=report.ToString().Trim();
   return result.Length>6000?result.Substring(0,6000)+"\n[diagnostics truncated]":result;
  }

  // The parts of a report that need no window: system details, the error, the app log and
  // the installer and updater error files. The window adds activity and import reports.
  internal static List<ReportSection> Sections(Exception error,string context,IDictionary<string,string> details){
   var sections=new List<ReportSection>();var system=new StringBuilder();
   system.AppendLine("AstroArchive "+Version);
   system.AppendLine("Windows: "+Environment.OSVersion.VersionString+(Environment.Is64BitOperatingSystem?" (64-bit)":" (32-bit)"));
   system.AppendLine(".NET runtime: "+Environment.Version);
   system.AppendLine("Culture: "+System.Globalization.CultureInfo.CurrentCulture.Name+" / UI "+System.Globalization.CultureInfo.CurrentUICulture.Name);
   if(details!=null)foreach(var pair in details)system.AppendLine(pair.Key+": "+pair.Value);
   sections.Add(new ReportSection{Title="System details",Text=system.ToString().Trim(),Cap=1500});
   sections.Add(new ReportSection{Title="Error",Text="Context: "+(context??"User-submitted problem")+"\n"+(error==null?"No error was captured.":error.ToString()),Cap=3000,Include=error!=null});
   sections.Add(new ReportSection{Title="Recent app log",Text=AppLog.Tail(80),KeepEnd=true,Cap=3500});
   var failures=new StringBuilder();
   foreach(var candidate in new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AstroArchive","updates","last-error.txt"),Path.Combine(Path.GetTempPath(),"AstroArchive-setup-error.txt")}){
    try{var info=new FileInfo(candidate);if(info.Exists&&info.Length>0)failures.AppendLine(info.Name+" ("+info.LastWriteTime.ToString("yyyy-MM-dd HH:mm",System.Globalization.CultureInfo.InvariantCulture)+"):\n"+File.ReadAllText(candidate).Trim());}catch{}
   }
   if(failures.Length>0)sections.Add(new ReportSection{Title="Install and update errors",Text=failures.ToString().Trim(),KeepEnd=true,Cap=1500});
   return sections;
  }

  // The included sections as plain text, each shortened to scale x its cap.
  internal static string Compose(IEnumerable<ReportSection> sections,double scale=1){
   var text=new StringBuilder();
   foreach(var section in sections.Where(s=>s.Include&&!string.IsNullOrWhiteSpace(s.Text))){
    string body=Sanitize(section.Text).Trim();int cap=Math.Max(200,(int)(section.Cap*scale));
    if(body.Length>cap)body=section.KeepEnd?"[earlier lines omitted]\n"+body.Substring(body.Length-cap):body.Substring(0,cap)+"\n[shortened]";
    text.Append("### ").AppendLine(section.Title).AppendLine(body).AppendLine();
   }
   return text.ToString().Trim();
  }

  internal static string IssueUrl(string diagnostics){bool truncated;return IssueUrl(null,null,new[]{new ReportSection{Title="Diagnostics",Text=diagnostics??"No diagnostics available"}},out truncated);}
  // A prefilled issue: the user's own title and description, then the chosen diagnostics.
  // When everything will not fit in a link, sections are shortened (newest log lines kept)
  // and the issue asks the user to paste the full report, which the window copies for them.
  internal static string IssueUrl(string title,string description,IList<ReportSection> sections,out bool truncated){
   truncated=false;string url=null;
   foreach(double scale in new[]{1,0.7,0.5,0.35,0.25,0.15,0.08}){
    string diagnostics=Compose(sections,scale);truncated=scale<1&&diagnostics!=Compose(sections);
    url=Url(title,description,diagnostics,truncated);if(url.Length<=UrlLimit)return url;truncated=true;
   }
   return Url(title,description,"",true);
  }
  static string Url(string title,string description,string diagnostics,bool truncated){
   string what=string.IsNullOrWhiteSpace(description)?"Please describe what you were doing and what you expected to happen.":Sanitize(description.Trim());
   string body="## What happened?\n\n"+what+"\n\n## Diagnostic details\n\n"+(truncated?"_Shortened to fit. The full report is on your clipboard: paste it below this line if you can._\n\n":"")+
    (diagnostics.Length==0?"":"```text\n"+diagnostics.Replace("```","'''")+"\n```\n")+"\n_Prepared by AstroArchive "+Assembly.GetExecutingAssembly().GetName().Version+". Images and archive files are never attached._\n";
   string heading=string.IsNullOrWhiteSpace(title)?"AstroArchive problem report":Sanitize(title.Trim());
   return IssueBase+"?title="+Uri.EscapeDataString(heading)+"&body="+Uri.EscapeDataString(body);
  }

  internal static string Sanitize(string text){
   return Regex.Replace(text??"",@"(?i)(?:[a-z]:\\|\\\\)[^\r\n\t""<>|]*","[local path]");
  }

 }
}
