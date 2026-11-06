using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace AstroArchive {
 public partial class MainUi {
  // The refresh after a process. The index read and the Edited folder scan happen on a worker; only the cheap list updates run on the UI thread.
  async Task<string> RefreshInBackground(){
   var repository=repo;if(closing||Window.Dispatcher.HasShutdownStarted)return null;
   if(repository==null){ReconcileImportRows();Filter(true);if(editedReady)RefreshEdited(null,null,new EditedGallery());return null;}
   System.Collections.Generic.List<Frame> frames=null;EditedGallery gallery=null;bool edited=editedReady;
   try{await Task.Run(()=>{frames=repository.All();if(edited)gallery=EditedGallery.Read(repository,frames.Select(f=>f.Target).Distinct().ToArray(),CancellationToken.None);});}
   catch(Exception error){return error.Message;}
   if(closing||Window.Dispatcher.HasShutdownStarted||repo!=repository)return null;
   all=frames;ApplyAnnotations(all);ScheduleTotalityTags();ScheduleSolarMetadata();ScheduleRepositorySize();repositoryRows.Clear();for(int i=0;i<all.Count;i++)if(!string.IsNullOrEmpty(all[i].Hash))repositoryRows[all[i].Hash]=i;
   ReconcileImportRows();Filter(true);if(gallery!=null)RefreshEdited(null,null,gallery);return null;
  }
 }
}
