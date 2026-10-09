namespace AstroArchive {
 public partial class MainUi {
  bool sourceRemovalConfirming;
  bool ConfirmSourceDeletion(string source,bool dump=false){
   if(RepositoryOperationBlocked)return false;
   sourceRemovalConfirming=true;SetBusy(true);
   try{return SourceDeletionConfirmation(source,dump).Show();}
   finally{sourceRemovalConfirming=false;SetBusy(false);}
  }
  FormWindow SourceDeletionConfirmation(string source,bool dump=false){
   var dialog=new FormWindow(Window,"Confirm source removal",680,500);
   dialog.Text("Delete after import is ON",true);
   dialog.Text("Source folder:\n"+source);
   dialog.Text("Eligible source originals will be deleted after their archive copies are saved and verified. The archive copies will remain.");
   dialog.Text("Calibration originals always stay on the source/telescope. Changed or failed files also stay.");
   dialog.Text(dump?"Dump processing also removes verified duplicate science files from Dump.":"Existing duplicates are kept on the source.");
   dialog.Text("Deletion in a cloud-synced source folder propagates to the cloud and other devices.");
   dialog.Text("Cancel to keep all source files and return without starting this import.");
   dialog.Accept("Import and delete eligible originals",()=>true,true);return dialog;
  }
 }
}
