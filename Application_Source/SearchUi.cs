using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace AstroArchive {
 public partial class MainUi {
  const string SearchTip="Search files and metadata. Combine a target, device and file type, for example M45 S50 Pro stack.";
  void InitializeSearch(){
   foreach(string name in new[]{"SearchBox","ImportSearchBox","EditedSearchBox"}){string field=name;UiHelp.ClearTip(T(name));UiHelp.Describe(T(name),SearchTip);T(name).PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Enter){e.Handled=true;ScheduleSearch(field,false,true);}else if(e.Key==Key.Escape){e.Handled=true;T(field).Clear();ScheduleSearch(field,false,true);}};}
  }
  void ShowSearchError(string name,FileSearch query){UiHelp.Tip(T(name),query.Error==null?null:query.Error+" No results until corrected.");UiHelp.Describe(T(name),query.Error??SearchTip);}
  static SearchDocument EditedSearchDocument(EditedImage image){
   var d=new SearchDocument{Target=image.Metadata.Object,Text=image.Filename+" "+image.RelativePath+" "+image.FileType+" "+image.Kind+" "+image.Source+" "+image.Metadata.ImageClass+" "+image.Metadata.ObjectLabel+" "+Catalog.Aliases(image.Metadata.Object)+" "+image.Metadata.Filters};
   d.Fields["target"]=image.Metadata.ObjectLabel+" "+Catalog.Aliases(image.Metadata.Object);d.Fields["file"]=image.Filename;d.Fields["path"]=image.RelativePath;d.Fields["format"]=image.FileType;d.Fields["type"]=image.Metadata.ImageClass+" "+image.Kind;d.Fields["filter"]=image.Metadata.Filters;d.Fields["source"]=image.Source;d.Fields["device"]=image.Filename+" "+image.RelativePath+" "+image.Source;
   d.Numbers["exposure"]=image.Metadata.SubExposure;return d;
  }
 }
}
