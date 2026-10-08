using System;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  const string SearchTip="Search names, filenames and metadata. Use quotes for phrases, - to exclude, OR for alternatives, and fields such as target:, file:, filter:, date: or exposure:>20. Click ? for examples.";
  void InitializeSearch(){
   foreach(string name in new[]{"SearchBox","ImportSearchBox","EditedSearchBox"})UiHelp.Tip(T(name),SearchTip);
   foreach(string name in new[]{"SearchHelpButton","ImportSearchHelpButton","EditedSearchHelpButton"}){B(name).Click+=(s,e)=>ShowSearchGuide();UiHelp.Tip(B(name),"Search syntax and examples");}
  }
  void ShowSearchError(string name,FileSearch query){T(name).ToolTip=query.Error==null?SearchTip:query.Error+" No files match until the query is complete.";System.Windows.Automation.AutomationProperties.SetHelpText(T(name),query.Error??SearchTip);}
  void ShowSearchGuide(){
   var dialog=new FormWindow(Window,"Find files",660,600);
   dialog.Text("Search across your files",true);dialog.Text("Words combine to narrow results. Catalogue IDs and common names resolve to the same target, including spaced IDs such as NGC 6888. Case, accents and filename separators are handled consistently.");
   dialog.Text("Examples",true);
   dialog.Text("Crescent Nebula filter:dual\ntarget:M45 -type:Stack\nfile:*.fits OR file:*.fit\n\"wide field\" -failed\nexposure:>=20 gain:80\ndate:2026-10 device:Seestar\nproject:\"Orion edit\" (Edited)");
   dialog.Text("Search fields",true);dialog.Text("target, file, path, device, camera, filter, type, format, date, mount, notes, status, review, session and hash. Edited also supports project and source. Exposure, gain and temperature accept =, >, >=, < and <= with numbers; exposure is in seconds.");
   dialog.Text("Quoted phrases stay together. Prefix a term with - to exclude it. OR or | separates alternative groups. * matches any characters and ? matches one. Existing table filters apply to every search group. An incomplete quote or comparison shows no results, preventing a broad accidental import.");dialog.CloseOnly();dialog.Show();
  }
  static SearchDocument EditedSearchDocument(EditedImage image){
   var d=new SearchDocument{Target=image.Metadata.Object,Text=image.Filename+" "+image.RelativePath+" "+image.FileType+" "+image.Kind+" "+image.Source+" "+image.Metadata.ImageClass+" "+image.Metadata.ObjectLabel+" "+Catalog.Aliases(image.Metadata.Object)+" "+image.Metadata.Filters+" "+image.Project.Name};
   d.Fields["target"]=image.Metadata.ObjectLabel+" "+Catalog.Aliases(image.Metadata.Object);d.Fields["file"]=image.Filename;d.Fields["path"]=image.RelativePath;d.Fields["format"]=image.FileType;d.Fields["type"]=image.Metadata.ImageClass+" "+image.Kind;d.Fields["filter"]=image.Metadata.Filters;d.Fields["project"]=image.Project.Name;d.Fields["source"]=image.Source;
   d.Numbers["exposure"]=image.Metadata.SubExposure;return d;
  }
 }
}
