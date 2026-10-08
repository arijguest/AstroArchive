// One active worker and one replaceable pending request: no backlog while typing.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AstroArchive {
 public sealed class LatestSearch<T>:IDisposable {
  sealed class Request {public long Version;public Func<CancellationToken,T> Work;public readonly CancellationTokenSource Cancel=new CancellationTokenSource();public readonly TaskCompletionSource<T> Completion=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);}
  readonly object gate=new object();Request active,pending;long version;bool running,disposed;
  public Task<T> Submit(Func<CancellationToken,T> work){
   lock(gate){if(disposed)throw new ObjectDisposedException("LatestSearch");CancelLocked();var request=new Request{Version=version,Work=work};pending=request;if(!running){running=true;Task.Run((Action)Process);}return request.Completion.Task;}
  }
  public void Cancel(){lock(gate)CancelLocked();}
  void CancelLocked(){version++;if(active!=null)active.Cancel.Cancel();if(pending!=null){pending.Completion.TrySetCanceled();pending.Cancel.Dispose();pending=null;}}
  void Process(){
   while(true){Request request;lock(gate){request=pending;pending=null;if(request==null){running=false;return;}active=request;}
    try{request.Cancel.Token.ThrowIfCancellationRequested();T result=request.Work(request.Cancel.Token);lock(gate){if(request.Version==version&&!disposed&&!request.Cancel.IsCancellationRequested)request.Completion.TrySetResult(result);else request.Completion.TrySetCanceled();}}
    catch(OperationCanceledException){request.Completion.TrySetCanceled();}catch(Exception error){lock(gate){if(request.Version!=version||disposed||request.Cancel.IsCancellationRequested)request.Completion.TrySetCanceled();else request.Completion.TrySetException(error);}}
    finally{lock(gate){active=null;request.Cancel.Dispose();}}
   }
  }
  public void Dispose(){lock(gate){disposed=true;CancelLocked();}}
 }
 // Owned by a serialized worker. Replaced whenever metadata/source rows change.
 public sealed class SearchIndex<T> where T:class {
  readonly Dictionary<T,SearchDocument> documents=new Dictionary<T,SearchDocument>();readonly Func<T,SearchDocument> create;
  public int PreparedCount{get{return documents.Count;}}
  public SearchIndex(Func<T,SearchDocument> create){this.create=create;}
  public List<T> Find(IEnumerable<T> rows,FileSearch query,Func<T,bool> predicate,CancellationToken token){
   var result=new List<T>();foreach(T row in rows){token.ThrowIfCancellationRequested();if(query.Error!=null||!predicate(row))continue;if(query.IsEmpty){result.Add(row);continue;}
    SearchDocument document;if(!documents.TryGetValue(row,out document)){document=create(row);documents[row]=document;}
    if(query.Matches(document))result.Add(row);
   }return result;
  }
 }
 public sealed class SearchSort {public string Property;public bool Descending;}
 public static class SearchOrdering {
  sealed class Entry<T>{public T Row;public object[] Keys;public int Position;}
  public static List<T> Order<T>(IEnumerable<T> rows,IEnumerable<SearchSort> sorting,CultureInfo culture,CancellationToken token){
   var sorts=sorting.ToArray();var properties=sorts.Select(s=>Path(typeof(T),s.Property)).ToArray();int position=0;
   var entries=new List<Entry<T>>();foreach(var row in rows){token.ThrowIfCancellationRequested();entries.Add(new Entry<T>{Row=row,Position=position++,Keys=properties.Select(p=>Value(row,p)).ToArray()});}
   var comparer=new Comparer(culture);try{entries.Sort((a,b)=>{token.ThrowIfCancellationRequested();for(int i=0;i<sorts.Length;i++){int comparison=sorts[i].Descending?comparer.Compare(b.Keys[i],a.Keys[i]):comparer.Compare(a.Keys[i],b.Keys[i]);if(comparison!=0)return comparison;}return a.Position.CompareTo(b.Position);});}catch(InvalidOperationException error){if(error.InnerException is OperationCanceledException)throw new OperationCanceledException(token);throw;}
   token.ThrowIfCancellationRequested();return entries.Select(e=>e.Row).ToList();
  }
  static PropertyInfo[] Path(Type type,string path){var properties=new List<PropertyInfo>();foreach(string part in path.Split('.')){var property=type.GetProperty(part);if(property==null)throw new ArgumentException("Unknown sort property: "+path);properties.Add(property);type=property.PropertyType;}return properties.ToArray();}
  static object Value(object row,IEnumerable<PropertyInfo> path){foreach(var property in path){if(row==null)return null;row=property.GetValue(row,null);}return row;}
 }
}
