using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace AstroArchive {
 public partial class MainUi {
  // The refresh after a process. The index read and the Edited folder scan happen on a worker; only the cheap list updates run on the UI thread.
  async Task RefreshInBackground(){
   var repository=repo;if(repository==null||Window.Dispatcher.HasShutdownStarted){Refresh();return;}
   System.Collections.Generic.List<Frame> frames=null;EditedGallery gallery=null;bool edited=editedReady;
   try{await Task.Run(()=>{frames=repository.All();if(edited)gallery=EditedGallery.Read(repository,frames.Select(f=>f.Target).Distinct().ToArray(),CancellationToken.None);});}
   catch(Exception){frames=null;gallery=null;}
   if(closing||Window.Dispatcher.HasShutdownStarted)return;
   // Another repository may have been opened meanwhile, or the read failed: fall back to the plain refresh.
   if(repo!=repository||frames==null){Refresh();return;}
   all=frames;ApplyAnnotations(all);ScheduleTotalityTags();ScheduleRepositorySize();repositoryRows.Clear();for(int i=0;i<all.Count;i++)if(!string.IsNullOrEmpty(all[i].Hash))repositoryRows[all[i].Hash]=i;
   Filter(true);RefreshEdited(null,null,gallery);SetBusy(cancel!=null);
  }
 }
}
