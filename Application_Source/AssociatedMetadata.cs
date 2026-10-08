using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
    public static class AssociatedMetadata {
        // Only recognised adjacent capture/session sidecars; never sweep arbitrary log trees.
        internal static IEnumerable<string> Names(string path){string stem=Path.GetFileNameWithoutExtension(path);return new[]{stem+".json",stem+".txt","session.json","capture.json","metadata.json","acquisition.log"}.Distinct(StringComparer.OrdinalIgnoreCase);}
        public static List<AssociatedFile> Discover(string path) {
            string directory=Path.GetDirectoryName(path);
            var result=new List<AssociatedFile>();
            foreach(string name in Names(path)) {
                string candidate=Path.Combine(directory,name);
                if(!File.Exists(candidate))continue;
                var stamp=FileStamp.Read(candidate);
                if(stamp.Size>16*1024*1024)continue;
                result.Add(new AssociatedFile {
                    SourcePath=candidate,Stamp=stamp,Role=name=="acquisition.log"?"Acquisition log":"Capture / session sidecar"
                });
            }
            return result;
        }
    }
    public sealed partial class Repository {
        void PreserveAssociated(Frame frame,CancellationToken ct,Dictionary<string,Tuple<FileStamp,string>> cache) {
            if(frame.AssociatedFiles==null)return;
            foreach(var associated in frame.AssociatedFiles) {
                ct.ThrowIfCancellationRequested();
                if(!string.IsNullOrEmpty(associated.RelativePath))continue;
                CheckManagedPath(associated.SourcePath,frame.SourceRoot??Path.GetDirectoryName(frame.SourcePath));
                var before=FileStamp.Read(associated.SourcePath);
                if(associated.Stamp==null||!before.ContentSame(associated.Stamp))throw new IOException("Associated metadata changed since scanning: "+associated.SourcePath);
                Tuple<FileStamp,string> saved;
                string hash,relative;
                if(cache.TryGetValue(associated.SourcePath,out saved)&&before.ContentSame(saved.Item1)) {
                    relative=saved.Item2;
                    hash=Path.GetFileNameWithoutExtension(relative);
                }
                else {
                    hash=Util.Hash(associated.SourcePath,ct);
                    relative=Path.Combine(".astroarchive","session-metadata",hash+Path.GetExtension(associated.SourcePath).ToLowerInvariant());
                    string destination=Path.Combine(Root,relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    if(File.Exists(destination)) {
                        if(Util.Hash(destination,ct)!=hash)throw new IOException("Associated archive copy failed checksum verification.");
                    }
                    else {
                        string temp=destination+"."+Guid.NewGuid().ToString("N")+".partial";
                        try {
                            CopyVerified(associated.SourcePath,temp,hash,ct);
                            File.Move(temp,destination);
                        }
                        finally {
                            TryRemove(temp);
                        }
                    }
                    if(!before.ContentSame(FileStamp.Read(associated.SourcePath)))throw new IOException("Associated metadata changed during preservation.");
                    cache[associated.SourcePath]=Tuple.Create(before,relative);
                }
                associated.Hash=hash;
                associated.RelativePath=relative;
            }
        }
    }
}
