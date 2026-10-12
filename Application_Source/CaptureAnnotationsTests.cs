using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void CaptureAnnotationsTests(){
   Test("Tags are trimmed, de-duplicated without case and bounded",()=>{
    var tags=CaptureAnnotations.Tags(" #Widefield, widefield;Reprocess\n  needs   flats ,,");Check(tags.SequenceEqual(new[]{"Widefield","Reprocess","needs flats"}),"Tags not normalised: "+string.Join("|",tags));
    Check(CaptureAnnotations.Tags(string.Join(",",Enumerable.Range(0,50).Select(i=>"t"+i))).Count==CaptureAnnotations.MaxTags&&CaptureAnnotations.Tags(new string('x',80))[0].Length==CaptureAnnotations.MaxTagLength,"Tag limits not applied");
    Check(CaptureAnnotations.Note("  \r\n ")==null&&CaptureAnnotations.Note(" Clouds\r\nafter 1am ")=="Clouds\nafter 1am","Notes not normalised");
   });
   Test("Starring, tags and notes edit batches without disturbing other values",()=>{
    var store=new Dictionary<string,CaptureAnnotation>(StringComparer.OrdinalIgnoreCase);
    var a=new Frame{Hash="aa",Target="M31"};var b=new Frame{Hash="bb",Target="M31"};var copy=new Frame{Hash="AA",Target="M31"};var unhashed=new Frame{Target="M31"};
    var changes=new List<string>();a.PropertyChanged+=(s,e)=>changes.Add(e.PropertyName);
    Check(CaptureAnnotations.Edit(store,new[]{a,b,copy,unhashed},new AnnotationEdit{Starred=true})==2&&a.IsStarred&&b.IsStarred&&copy.IsStarred&&!unhashed.IsStarred&&a.StarText=="★"&&changes.Contains("IsStarred"),"Star not applied to every copy of a capture");
    var edit=new AnnotationEdit{Note="Clouds after 1am"};edit.AddTags.Add("Reprocess");CaptureAnnotations.Edit(store,new[]{a},edit);
    edit=new AnnotationEdit();edit.AddTags.Add("Widefield");CaptureAnnotations.Edit(store,new[]{a,b},edit);
    Check(a.Tags.SequenceEqual(new[]{"Reprocess","Widefield"})&&b.Tags.SequenceEqual(new[]{"Widefield"})&&a.UserNote=="Clouds after 1am"&&b.UserNote==null,"Batch tags or untouched notes changed");
    Check(CaptureAnnotations.CommonTags(new[]{a,b}).SequenceEqual(new[]{"Widefield"}),"Shared tags wrong");
    edit=new AnnotationEdit{Starred=false,Note=""};edit.RemoveTags.Add("widefield");edit.RemoveTags.Add("reprocess");CaptureAnnotations.Edit(store,new[]{a,b},edit);
    Check(!a.IsStarred&&a.Tags==null&&a.UserNote==null&&store.Count==0,"Clearing every value left an annotation behind");
    Check(CaptureAnnotations.Edit(store,new[]{a},new AnnotationEdit())==0,"An empty edit reported changes");
   });
   Test("Annotations persist with the repository and Edited copies keep theirs by project path",()=>{
    string meta=Path.Combine(root,"annotation-meta");var store=new Dictionary<string,CaptureAnnotation>(StringComparer.OrdinalIgnoreCase);
    var frame=new Frame{Hash="cafe",Target="M42"};var project=new EditedProject{Id=Guid.NewGuid().ToString("N"),Name="Final"};var image=new EditedImage{Project=project,RelativePath=@"Output\M42_final.tif",Filename="M42_final.tif"};
    var edit=new AnnotationEdit{Starred=true,Note="Best version"};edit.AddTags.Add("Portfolio");CaptureAnnotations.Edit(store,new IAnnotated[]{frame,image},edit);CaptureAnnotations.Save(meta,store);
    var loaded=CaptureAnnotations.Load(meta);var again=new Frame{Hash="CAFE",Target="M42"};var resaved=new EditedImage{Project=project,RelativePath="output/m42_final.tif",Filename="M42_final.tif"};
    CaptureAnnotations.Apply(new IAnnotated[]{again,resaved},loaded);
    Check(loaded.Count==2&&again.IsStarred&&again.UserNote=="Best version"&&resaved.IsStarred&&resaved.TagsText=="Portfolio","Annotations lost across reload or Edited path spelling");
    Check(CaptureAnnotations.KnownTags(loaded).SequenceEqual(new[]{"Portfolio"})&&CaptureAnnotations.Load(Path.Combine(root,"annotation-missing")).Count==0,"Known tags or missing store wrong");
    Check(again.SearchText.Contains("starred")&&again.SearchText.Contains("Portfolio")&&again.SearchText.Contains("Best version")&&FileSearch.Parse("tag:portfolio note:best").Matches(again)&&!FileSearch.Parse("tag:widefield").Matches(again),"Stars, tags or notes are not searchable");
    var plain=new Frame{Hash="beef",Target="M42"};var filters=new CaptureFilters();filters.Values["Starred"]="Starred";Check(filters.Apply(new[]{again,plain},"").Single()==again,"Starred filter wrong");
    filters.Values.Clear();filters.Values["Tag"]="portfolio";Check(filters.Apply(new[]{again,plain},"").Single()==again&&CaptureFilters.Options(new[]{again,plain},"Tag").SequenceEqual(new[]{"Portfolio","No tags"}),"Tag filter or choices wrong");
    var clone=again.Clone();var notified=false;clone.PropertyChanged+=(s,e)=>notified=true;again.IsStarred=false;Check(clone.IsStarred&&!notified,"Clones share stars or change notifications");
   });
  }
 }
}
