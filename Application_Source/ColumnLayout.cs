// Stable property IDs keep saved table layouts independent of display labels.
using System.Collections.Generic;
using System.Linq;
namespace AstroArchive {
 public sealed class ColumnLayout {
  public List<string> Order{get;set;}public List<string> Visible{get;set;}
  public bool RepositoryGainShown{get;set;}
  public static ColumnLayout UpgradeRepository(ColumnLayout saved){
   if(saved==null||saved.RepositoryGainShown)return saved;
   var order=saved.Order==null?null:saved.Order.ToList();
   if(order!=null&&!saved.VisibleContains("GainText")){order.Remove("GainText");order.Insert(order.Contains("ExposureText")?order.IndexOf("ExposureText")+1:order.Count,"GainText");}
   var visible=saved.Visible==null?null:saved.Visible.ToList();if(visible!=null&&!visible.Contains("GainText"))visible.Add("GainText");
   return new ColumnLayout{Order=order,Visible=visible,RepositoryGainShown=true};
  }
  bool VisibleContains(string id){return Visible!=null&&Visible.Contains(id);}
  public static ColumnLayout UpgradeEdited(ColumnLayout saved){
   if(saved==null||saved.Order!=null&&saved.Order.Contains("FileType"))return saved;
   var order=(saved.Order??new List<string>{"Filename"}).ToList();order.Insert(order.IndexOf("Filename")+1,"FileType");
   var visible=saved.Visible==null?null:saved.Visible.Where(id=>id!="Metadata.TotalExposureText").ToList();if(visible!=null&&!visible.Contains("FileType"))visible.Add("FileType");
   return new ColumnLayout{Order=order,Visible=visible,RepositoryGainShown=saved!=null&&saved.RepositoryGainShown};
  }
  public static ColumnLayout Resolve(ColumnLayout saved,IEnumerable<string> available,IEnumerable<string> defaults){
   var ids=available.Distinct().ToList();var known=new HashSet<string>(ids);
   var order=(saved==null||saved.Order==null?ids:saved.Order).Where(known.Contains).Distinct().ToList();order.AddRange(ids.Where(id=>!order.Contains(id)));
   var visible=(saved==null||saved.Visible==null?defaults:saved.Visible).Where(known.Contains).Distinct().ToList();
   if(visible.Count==0)visible=defaults.Where(known.Contains).Distinct().ToList();
   if(visible.Count==0&&ids.Count>0)visible.Add(ids[0]);
   return new ColumnLayout{Order=order,Visible=visible,RepositoryGainShown=saved!=null&&saved.RepositoryGainShown};
  }
 }
}
