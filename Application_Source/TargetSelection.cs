using System;
using System.Collections.Generic;
using System.Linq;
namespace AstroArchive {
 // Replace selection only for visible rows; retain rows belonging to other targets.
 public sealed class TargetSelection<T> {
  readonly Func<T,string> key;readonly Dictionary<string,T> selected=new Dictionary<string,T>(StringComparer.OrdinalIgnoreCase);
  public TargetSelection(Func<T,string> identity){key=identity;}
  public int Count{get{return selected.Count;}}
  public List<T> Items{get{return selected.Values.ToList();}}
  public bool Contains(T item){return selected.ContainsKey(key(item));}
  public void Clear(){selected.Clear();}
  public void Add(T item){selected[key(item)]=item;}
  public void ChangeVisible(IEnumerable<T> visible,IEnumerable<T> current){var keep=current.ToList();foreach(var item in visible)selected.Remove(key(item));foreach(var item in keep)Add(item);}
  public void Reconcile(IEnumerable<T> source){if(selected.Count==0)return;var available=source.GroupBy(key,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase);foreach(var id in selected.Keys.ToArray()){T item;if(available.TryGetValue(id,out item))selected[id]=item;else selected.Remove(id);}}
 }
}
