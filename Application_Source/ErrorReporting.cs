using System;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace AstroArchive {
 internal static class AppErrorReporting {
  const string IssueTemplate="bug_report.md";
  const string IssueBase="https://github.com/arijguest/AstroArchive/issues/new?template="+IssueTemplate;

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

  internal static string IssueUrl(string diagnostics){
   string body="## What happened?\n\nPlease describe what you were doing and what you expected to happen.\n\n## Diagnostic details\n\n```\n"+
    (diagnostics??"No diagnostics available").Replace("```","'''")+"\n```\n";
   string title="AstroArchive problem report";
   return IssueBase+"&title="+Uri.EscapeDataString(title)+"&body="+Uri.EscapeDataString(body);
  }

  static string Sanitize(string text){
   return Regex.Replace(text??"",@"(?i)(?:[a-z]:\\|\\\\)[^\r\n\t""<>|]*","[local path]");
  }

 }
}
