// User assignments live with Edited records; saving never rewrites image pixels.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed partial class Repository {
  static readonly string[] EditedFields={"Object","Filters","ImageClass","Subs","SubExposure","TotalExposure","RA","Dec"};
  static void ValidateEditedMetadata(EditedMetadata changes){
   if(changes==null)throw new ArgumentNullException("changes");
   if(changes.Subs.HasValue&&changes.Subs.Value<=0)throw new ArgumentException("Sub count must be a positive whole number.");
   foreach(double? number in new[]{changes.SubExposure,changes.TotalExposure})if(number.HasValue&&(number<=0||double.IsNaN(number.Value)||double.IsInfinity(number.Value)))throw new ArgumentException("Exposure must be a positive, finite number of seconds.");
   if(!string.IsNullOrEmpty(changes.ImageClass)&&!new[]{"Edited image","Starless","Stars only","GIF","Meteor","Unknown (conflicting labels)"}.Contains(changes.ImageClass))throw new ArgumentException("Choose a supported image class.");
  }
  static EditedMetadata ApplyEditedMetadata(EditedMetadata current,EditedMetadata changes,string filename){
   var result=current.Clone();foreach(string field in EditedFields){var property=typeof(EditedMetadata).GetProperty(field);object value=property.GetValue(changes,null);if(value!=null&&(!(value is string)||!string.IsNullOrWhiteSpace((string)value)))property.SetValue(result,value,null);}
   if(!string.IsNullOrEmpty(result.Object))result.Object=Catalog.Normalize(result.Object);
   if(Util.MeteorFilename(filename)||result.ImageClass=="Meteor"){result.ImageClass="Meteor";result.Object=null;}
   result.Evidence=(current.Evidence??"")+"\nUser-assigned metadata in Edited.";return result;
  }
  public void SaveEditedMetadata(EditedProject selected,IEnumerable<EditedImage> selection,EditedMetadata changes,CancellationToken ct){
   ValidateEditedMetadata(changes);ct.ThrowIfCancellationRequested();var project=ReadEditedProject(selected);var images=selection.ToList();if(images.Count==0)throw new ArgumentException("Select edited images.");
   if(!EditedFields.Any(field=>typeof(EditedMetadata).GetProperty(field).GetValue(changes,null)!=null))return;
   if(project.MetadataEdits==null)project.MetadataEdits=new Dictionary<string,EditedMetadata>(StringComparer.OrdinalIgnoreCase);
   foreach(var image in images){ct.ThrowIfCancellationRequested();if(!File.Exists(EditedPath(project,image.RelativePath)))throw new IOException("The edited image is unavailable: "+image.Filename);
    string key=project.MetadataEdits.Keys.FirstOrDefault(k=>k.Equals(image.RelativePath,StringComparison.OrdinalIgnoreCase))??image.RelativePath;EditedMetadata previous;project.MetadataEdits.TryGetValue(key,out previous);project.MetadataEdits[key]=ApplyEditedMetadata(previous??new EditedMetadata(),changes,image.Filename);
   }SaveEditedProject(project,ct);
  }
  static EditedMetadata UserEditedMetadata(EditedProject project,string relative,EditedMetadata detected){
   if(project.MetadataEdits==null)return detected;var saved=project.MetadataEdits.FirstOrDefault(pair=>pair.Key.Equals(relative,StringComparison.OrdinalIgnoreCase));return saved.Value==null?detected:ApplyEditedMetadata(detected,saved.Value,relative);
  }
 }
}
