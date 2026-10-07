// Stable property IDs keep saved table layouts independent of display labels.
using System.Collections.Generic;
using System.Linq;
namespace AstroArchive {
 public sealed class ColumnLayout {
  public List<string> Order{get;set;}public List<string> Visible{get;set;}
  public static ColumnLayout Resolve(ColumnLayout saved,IEnumerable<string> available,IEnumerable<string> defaults){
   var ids=available.Distinct().ToList();var known=new HashSet<string>(ids);
   var order=(saved==null||saved.Order==null?ids:saved.Order).Where(known.Contains).Distinct().ToList();order.AddRange(ids.Where(id=>!order.Contains(id)));
   var visible=(saved==null||saved.Visible==null?defaults:saved.Visible).Where(known.Contains).Distinct().ToList();
   if(visible.Count==0)visible=defaults.Where(known.Contains).Distinct().ToList();
   if(visible.Count==0&&ids.Count>0)visible.Add(ids[0]);
   return new ColumnLayout{Order=order,Visible=visible};
  }
 }
}
