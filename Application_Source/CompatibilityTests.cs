using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
namespace AstroArchive {
    public partial class Tests {
        static void CompatibilityCases() {
            if(NativeFits.Available)Test("Optional CFITSIO backend decodes an independently compressed FITS",()=> {
                string path=Path.Combine(root,"rice-compressed.fz");
                IntPtr file;
                int status=0;
                ffinit(out file,"!"+path+"[compress]",ref status);
                try {
                    ffcrimll(file,16,2,new long[] {
                        2,2
                    },ref status);
                    ffppr(file,21,1,4,new short[] {
                        -4,20,300,32000
                    },ref status);
                }
                finally {
                    ffclos(file,ref status);
                }
                Check(status==0,"Fixture encoder failed: "+status);
                var frame=Classifier.Read(path,root,"Rig","Auto");
                Check(Assets.Selected(frame).Compression=="cfitsio"&&Assets.Read(frame,path,0,ct).Pixels.SequenceEqual(new double[] {
                    -4,20,300,32000
                }),"Native decompression changed samples");
            });
            else {
                skipped++;
                Console.WriteLine("SKIP (optional codec required) CFITSIO backend");
            }
            if(PixelCodecs.ZstdAvailable)Test("Optional Zstandard backend decodes an independent reference block",()=> {
                byte[] block= {
                    0x28,0xb5,0x2f,0xfd,0x20,0x08,0x41,0x00,0x00,0x0c,0x00,0x2c,0x01,0x00,0x7d,0xff,0xff
                };
                string path=MakeXisf("zstandard.xisf",block,"UInt16","zstd:8","");
                Check(Assets.Read(Classifier.Read(path,root,"Rig","Auto"),path,0,ct).Pixels.SequenceEqual(new double[] {
                    12,300,32000,65535
                }),"Zstandard decode wrong");
            });
            else {
                skipped++;
                Console.WriteLine("SKIP (optional codec required) Zstandard backend");
            }
            Test("Explicit exposure and temperature units normalize with provenance",()=> {
                string path=Path.Combine(root,"units.fits");
                Write(path,4,4,(x,y)=>100,new Dictionary<string,string> {
                    {
                        "EXPTIME","500 / Exposure [ms]"
                    }, {
                        "CCD-TEMP","263.15 / Sensor [K]"
                    }, {
                        "GAIN","0.25 / e-/ADU"
                    }
                });
                var f=Classifier.Read(path,root,"Rig","Auto");
                Check(f.Exposure==0.5&&Math.Abs(f.Temperature.Value+10)<0.001&&!f.Gain.HasValue&&f.ElectronsPerAdu==0.25,"Explicit units not normalized");
                Check(f.Facts["Exposure"].Raw=="500"&&f.Facts["Exposure"].Unit=="s","Raw evidence lost");
                string output=path+".derived.fits";
                ScientificFits.Write(output,Assets.Read(f,path,0,ct),f,ct);
                var header=Fits.Header(output);
                Check(header.Number("EXPTIME")==0.5&&Math.Abs(header.Number("CCD-TEMP").Value+10)<0.001&&!header.Number("GAIN").HasValue&&header.Number("EGAIN")==0.25,"Derived header units wrong");
            });
            Test("Scientific export preflight rejects processed data before creating output",()=> {
                using(var repo=new Repository(Path.Combine(root,"processed-repo"))) {
                    var frame=CalibrationFixture("Light",30);
                    frame.Format="JPEG";
                    frame.LinearData=true;
                    frame.Hash=new string('a',64);
                    Expect(()=>Exporter.Create(repo,new List<Frame> {
                        frame
                    },new ExportOptions {
                        Parent=root,Name="processed-project"
                    },ct,NoProgress),"Processed image accepted");
                    Check(!Directory.Exists(Path.Combine(root,"processed-project")),"Failed preflight created a project");
                }
            });
            Test("Format discovery does not misidentify non-FITS gzip",()=> {
                foreach(string extension in new[] {
                    "fits","fits.gz","fz","xisf","tiff","png","ser","avi","cr3","dng"
                })Check(Assets.Supported("sample."+extension),extension);
                Check(!Assets.Supported("sample.png.gz")&&!Assets.Supported("sample.txt"),"Unsupported compression accepted");
            });
            Test("FITS HDUs retain their own metadata and explicit selection",()=> {
                string p=Path.Combine(root,"multi-image.fits");
                Write(p,4,3,(x,y)=>100+x,new Dictionary<string,string> {
                    {
                        "OBJECT","'M45'"
                    }, {
                        "EXPTIME","10"
                    }
                });
                using(var stream=new FileStream(p,FileMode.Append)) {
                    Header(stream,new List<string> {
                        Card("XTENSION","'IMAGE'"),Card("BITPIX","8"),Card("NAXIS","2"),Card("NAXIS1","2"),Card("NAXIS2","2"),Card("PCOUNT","0"),Card("GCOUNT","1"),Card("EXPTIME","20")
                    });
                    stream.Write(new byte[] {
                        1,2,3,4
                    },0,4);
                    while(stream.Length%2880!=0)stream.WriteByte(0);
                }
                var info=Assets.Inspect(p);
                Check(info.Images.Count==2&&info.Images[1].Headers["OBJECT"]=="M45"&&info.Images[1].Headers["EXPTIME"]=="20","Wrong inheritance");
                var f=Classifier.Read(p,root,"Rig","Auto",imageKey:"hdu:1");
                Check(f.Width==2&&f.Exposure==20&&f.ImageKey=="hdu:1","Selected metadata wrong");
                Check(Assets.Read(f,p,0,ct).Pixels.SequenceEqual(new double[] {
                    1,2,3,4
                }),"Selected payload wrong");
            });
            Test("Large ordinary FITS retains streaming preview and original stacking export",()=> {
                var frame=new Frame {
                    Format="FITS",OriginalName="large.fits",LinearData=true,Images=new List<ImageDescriptor> {
                        new ImageDescriptor {
                            Key="hdu:0",Numeric=true,Width=10000,Height=10000,Channels=1,Count=1,Bitpix=16
                        }
                    }
                };
                Check(Assets.CanDecode(frame)&&Assets.CanStack(frame)&&!Exporter.RequiresConversion(frame),"Large original FITS lost existing capabilities");
                frame.Images[0].Count=2;
                frame.ImageIndex=0;
                Check(Assets.CanDecode(frame)&&!Assets.CanStack(frame),"Oversize cube conversion escaped preflight");
            });
            Test("FITS cube axes are slices unless explicitly RGB",()=> {
                string p=Path.Combine(root,"slices.fits");
                WriteFloat(p,4,4,3);
                var f=Classifier.Read(p,root,"Rig","Auto");
                Check(Assets.Selected(f).Count==3&&f.Channels==1,"Unlabelled cube misclassified RGB");
                Check(!Assets.CanStack(f),"Unselected cube offered for stacking");
                f.ImageIndex=2;
                Check(Assets.CanStack(f),"Explicit slice ineligible");
                Check(Assets.Read(f,p,2,ct).Pixels.All(v=>v==30),"Slice offset wrong");
                Check(Assets.Display(f,p,2,ct).Pixels.All(v=>v==30),"Streamed selected preview wrong");
                Expect(()=>Assets.Read(f,p,3,ct),"Out-of-range slice accepted");
                string gz=p+".gz";
                using(var input=File.OpenRead(p))using(var output=File.Create(gz))using(var zip=new GZipStream(output,CompressionMode.Compress))input.CopyTo(zip);
                var compressed=Classifier.Read(gz,root,"Rig","Auto");
                Check(Assets.Read(compressed,gz,1,ct).Pixels.All(v=>v==20),"Gzip slice offset wrong");
            });
            Test("Scientific FITS conversion preserves physical values and NaNs",()=> {
                var source=new Frame {
                    Hash=new string('a',64),Images=new List<ImageDescriptor> {
                        new ImageDescriptor {
                            Key="image:0",Bitpix=-64,Headers=new Dictionary<string,string> {
                                {
                                    "BSCALE","2"
                                }, {
                                    "BZERO","32768"
                                }, {
                                    "OBJECT","M45"
                                }
                            }
                        }
                    },ImageKey="image:0"
                };
                var pixels=new PixelImage {
                    Width=2,Height=2,Channels=1,Pixels=new[] {
                        -4.5,32768.5,double.NaN,100000.25
                    }
                };
                string p=Path.Combine(root,"derived-physical.fits");
                ScientificFits.Write(p,pixels,source,ct);
                var output=Classifier.Read(p,root,"Rig","Auto");
                var decoded=Assets.Read(output,p,0,ct);
                Check(decoded.Pixels[0]==-4.5&&decoded.Pixels[1]==32768.5&&double.IsNaN(decoded.Pixels[2])&&decoded.Pixels[3]==100000.25,"Conversion changed physical values");
                Check(!Assets.Selected(output).Headers.ContainsKey("BZERO"),"Scaling applied twice");
                source.Images[0].Bitpix=64;
                Expect(()=>ScientificFits.Write(p+".integer",pixels,source,ct),"Inexact integer conversion accepted");
            });
            Test("XISF unsigned samples, properties and zlib checksum",()=> {
                byte[] raw= {
                    12,0,44,1,0,125,255,255
                };
                string p=MakeXisf("numeric.xisf",raw,"UInt16",null,"<Property id=\"PixInsight:Image:Linear\" value=\"true\"/><Property id=\"Instrument:Camera:Name\" value=\"ASI2600MC\"/><FITSKeyword name=\"IMAGETYP\" value=\"'Light'\"/>");
                var frame=Classifier.Read(p,root,"Rig","Auto");
                Check(frame.LinearData==true&&frame.CameraModel=="ASI2600MC"&&frame.Camera=="Primary","XISF properties missing");
                Check(Assets.Read(frame,p,0,ct).Pixels.SequenceEqual(new double[] {
                    12,300,32000,65535
                }),"Unsigned samples wrong");
                byte[] encoded=ZlibFixture(raw);
                string zip=MakeXisf("compressed.xisf",encoded,"UInt16","zlib:8","");
                var zipped=Classifier.Read(zip,root,"Rig","Auto");
                Check(Assets.Read(zipped,zip,0,ct).Pixels[3]==65535,"zlib decode wrong");
                encoded[encoded.Length-1]^=1;
                string bad=MakeXisf("checksum.xisf",encoded,"UInt16","zlib:8","");
                Expect(()=>Assets.Read(Classifier.Read(bad,root,"Rig","Auto"),bad,0,ct),"Bad checksum accepted");
            });
            Test("XISF LZ4 byte shuffle and malformed block rejection",()=> {
                byte[] shuffled= {
                    12,44,0,255,0,1,125,255
                };
                byte[] literal=new byte[9];
                literal[0]=128;
                Buffer.BlockCopy(shuffled,0,literal,1,8);
                string p=MakeXisf("shuffled.xisf",literal,"UInt16","lz4+sh:8:2","");
                var frame=Classifier.Read(p,root,"Rig","Auto");
                Check(Assets.Read(frame,p,0,ct).Pixels.SequenceEqual(new double[] {
                    12,300,32000,65535
                }),"Shuffle reversed incorrectly");
                Expect(()=>PixelCodecs.Lz4(new byte[] {
                    0,0,0
                },4),"Zero-distance LZ4 accepted");
                Check(PixelCodecs.Lz4(new byte[] {
                    0x40,1,2,3,4,4,0
                },8).SequenceEqual(new byte[] {
                    1,2,3,4,1,2,3,4
                }),"LZ4 backreference wrong");
            });
            Test("XISF inline and embedded images import and convert through shared readers",()=> {
                foreach(string location in new[] {
                    "inline:base64","embedded"
                }) {
                    string path=MakeXisf("reference-"+(location=="embedded"?"embedded":"inline")+".xisf",new byte[0],"UInt16",null,"");
                    byte[] raw= {
                        12,0,44,1,0,125,255,255
                    };
                    string metadata="<Property id=\"PixInsight:Image:Linear\" value=\"true\"/><Property id=\"PCL:CFAPattern\" value=\"RGGB\"/>";
                    string data=Convert.ToBase64String(raw);
                    string xml="<?xml version=\"1.0\"?><xisf version=\"1.0\"><Image geometry=\"2:2:1\" sampleFormat=\"UInt16\" location=\""+location+"\">"+metadata+(location=="embedded"?"<Data encoding=\"base64\">"+data+"</Data>":data)+"</Image></xisf>";
                    byte[] header=Encoding.UTF8.GetBytes(xml);
                    using(var stream=File.Create(path)) {
                        stream.Write(Encoding.ASCII.GetBytes("XISF0100"),0,8);
                        stream.Write(BitConverter.GetBytes(header.Length),0,4);
                        stream.Write(new byte[4],0,4);
                        stream.Write(header,0,header.Length);
                    }
                    var frame=Classifier.Read(path,root,"Rig","Auto");
                    Check(frame.Bayer=="RGGB"&&Assets.CanStack(frame)&&Assets.Read(frame,path,0,ct).Pixels.SequenceEqual(new double[] {
                        12,300,32000,65535
                    }),"Inline samples/properties lost");
                    Check(Assets.Display(frame,path,0,ct).Channels==3,"Existing CFA colour preview lost");
                }
            });
            Test("XISF unsupported pixel layout remains safely archiveable",()=> {
                string p=MakeXisf("complex.xisf",new byte[8],"Complex32",null,"");
                var f=Classifier.Read(p,root,"Rig","Auto");
                Check(!Assets.CanDecode(f)&&!Assets.CanStack(f),"Unsupported decoder advertised");
                using(var repo=new Repository(Path.Combine(root,"complex-archive"))) {
                    f.Status="New";
                    f.SourceRoot=root;
                    repo.Import(new[] {
                        f
                    },ct,NoProgress);
                    Check(repo.All().Count==1&&Util.Hash(repo.FilePath(repo.All()[0]),ct)==Util.Hash(p,ct),"Unsupported source lost");
                }
            });
            Test("XISF rejects escaping attachments and XML entities",()=> {
                string p=MakeXisf("invalid-attachment.xisf",new byte[8],"UInt16",null,"");
                byte[] file=File.ReadAllBytes(p);
                string xml=Encoding.UTF8.GetString(file,16,BitConverter.ToInt32(file,8));
                xml=xml.Replace("attachment:4096:8","attachment:0001:8");
                Buffer.BlockCopy(Encoding.UTF8.GetBytes(xml),0,file,16,Encoding.UTF8.GetByteCount(xml));
                File.WriteAllBytes(p,file);
                Expect(()=>Assets.Inspect(p),"Escaping attachment accepted");
                string hostile="<!DOCTYPE xisf [<!ENTITY capture 'expanded'>]><xisf version=\"1.0\"><Image geometry=\"2:2:1\" sampleFormat=\"UInt16\" location=\"attachment:4096:8\">&capture;</Image></xisf>";
                byte[] malicious=new byte[4104],header=Encoding.UTF8.GetBytes(hostile);
                Buffer.BlockCopy(Encoding.ASCII.GetBytes("XISF0100"),0,malicious,0,8);
                Buffer.BlockCopy(BitConverter.GetBytes(header.Length),0,malicious,8,4);
                Buffer.BlockCopy(header,0,malicious,16,header.Length);
                File.WriteAllBytes(p,malicious);
                Expect(()=>Assets.Inspect(p),"XML entity declaration accepted");
            });
            Test("SER mono sequence frame selection and little endian",()=> {
                string p=MakeSer("sequence.ser",0,0,16,2,new byte[] {
                    12,0,44,1,0,125,255,255,13,0,45,1,1,125,254,255
                });
                var f=Classifier.Read(p,root,"Rig","Auto");
                Check(f.Format=="SER"&&Assets.Selected(f).Count==2&&!Assets.CanStack(f)&&f.ObservedUtc!=null,"Sequence capabilities/time incorrect");
                Check(Assets.Read(f,p,1,ct).Pixels.SequenceEqual(new double[] {
                    13,301,32001,65534
                }),"Sequence endian/offset wrong");
                Expect(()=>Assets.Read(f,p,-1,ct),"Negative frame accepted");
            });
            Test("SER RGB/BGR and big endian preserve channel order",()=> {
                string p=MakeSer("bgr.ser",101,1,16,1,new byte[] {
                    0,30,0,20,0,10,0,31,0,21,0,11,0,32,0,22,0,12,0,33,0,23,0,13
                });
                var f=Classifier.Read(p,root,"Rig","Auto");
                Check(Assets.Read(f,p,0,ct).Pixels.SequenceEqual(new double[] {
                    10,11,12,13,20,21,22,23,30,31,32,33
                }),"BGR endian/channel order wrong");
                File.WriteAllBytes(p,new byte[179]);
                Expect(()=>Assets.Inspect(p),"Truncated SER accepted");
            });
            Test("Metadata separates camera gain from electrons per ADU",()=> {
                string p=Path.Combine(root,"egain.fits");
                Write(p,4,4,(x,y)=>100,new Dictionary<string,string> {
                    {
                        "INSTRUME","'ASI2600MC'"
                    }, {
                        "SWCREATE","'N.I.N.A.'"
                    }, {
                        "CCDGAIN","0.25"
                    }, {
                        "OFFSET","50"
                    }, {
                        "CAM-SER","'serial-01'"
                    }, {
                        "READMODE","'High gain'"
                    }
                });
                var frame=Classifier.Read(p,root,"Rig","Auto");
                Check(!frame.Gain.HasValue&&frame.ElectronsPerAdu==0.25&&frame.Offset==50,"CCDGAIN treated as acquisition gain");
                Check(frame.AcquisitionProfile=="nina-1"&&frame.CameraId=="serial-01"&&frame.Facts["ElectronsPerAdu"].Unit=="e-/ADU","Profile/provenance missing");
            });
            Test("Explicit Vaonis and Unistellar profiles avoid folder guesses",()=> {
                foreach(string model in new[] {
                    "Vespera Pro","Odyssey Pro"
                }) {
                    string p=Path.Combine(root,model+".fits");
                    Write(p,4,4,(x,y)=>100,new Dictionary<string,string> {
                        {
                            "TELESCOP","'"+model+"'"
                        }
                    });
                    var frame=Classifier.Read(p,root,"Physical-01","Auto");
                    Check(frame.Make==(model.StartsWith("Vespera")?"Vaonis":"Unistellar")&&frame.Telescope=="Physical-01"&&frame.TelescopeModel==model,"Device profile incorrect");
                }
            });
            Test("Timezone evidence excludes filename times from rotation",()=> {
                string p=Path.Combine(root,"Light_M45_10s_2026-10-07-01-00-00.fits");
                Write(p,4,4,(x,y)=>100,new Dictionary<string,string>());
                var frame=Classifier.Read(p,root,"Rig","Auto");
                Check(frame.Observed.Length>0&&frame.ObservedUtc==null,"Guessed filename UTC");
                var result=Rotation.Analyze(null,Enumerable.Repeat(frame,6).ToList(),null,null,ct,NoProgress);
                Check(result.Points.Count==0&&result.Mount=="Unknown","Unknown timezone used for mount classification");
            });
            Test("Re-detection preserves user overrides and archive identity",()=> {
                string p=Path.Combine(root,"review.fits");
                Write(p,4,4,(x,y)=>100,new Dictionary<string,string> {
                    {
                        "OBJECT","'M45'"
                    }, {
                        "GAIN","50"
                    }
                });
                var previous=Classifier.Read(p,root,"Rig","Auto");
                previous.Hash=Util.Hash(p,ct);
                previous.RelativePath="original.fits";
                previous.Target="M33";
                previous.Gain=100;
                Assets.UserFact(previous,"Target");
                Assets.UserFact(previous,"Gain");
                var candidate=Assets.InspectExisting(previous,p,ct);
                Check(candidate.Target=="M33"&&candidate.Gain==100&&candidate.Hash==previous.Hash&&candidate.RelativePath==previous.RelativePath&&candidate.Facts["Gain"].Source=="User","Override/identity lost");
                var legacy=Util.Deserialize<Frame>("{\"Hash\":\"abc\",\"Kind\":\"Light\",\"Camera\":\"Telephoto\"}");
                Check(legacy.Images==null&&Assets.CanStack(legacy),"Legacy records broke");
            });
            Test("Legacy metadata review preserves unattributed edits and applies selected fields",()=> {
                var previous=CalibrationFixture("Light",30);
                previous.Gain=100;
                previous.Facts=null;
                var candidate=previous.Clone();
                candidate.Gain=50;
                candidate.CameraModel="Detected model";
                candidate.Format="FITS";
                candidate.MetadataConflicts=new List<string>();
                var changes=MetadataReview.Compare(previous,candidate);
                Check(!changes.Single(c=>c.Field=="Gain").Apply&&changes.Single(c=>c.Field=="CameraModel").Apply,"Legacy defaults wrong");
                MetadataReview.Apply(changes);
                Check(candidate.Gain==100&&candidate.Facts["Gain"].Source!="User"&&candidate.MetadataConflicts.Any(c=>c.StartsWith("Gain:")),"Unattributed edit lost or guessed as user evidence");
                var before=CalibrationFixture("Light",30);
                var detected=before.Clone();
                detected.ReadoutMode="Different";
                var review=MetadataReview.Compare(before,detected);
                var choice=review.Single(c=>c.Field=="ReadoutMode");
                choice.Apply=false;
                MetadataReview.Apply(review);
                Check(detected.ReadoutMode=="Normal"&&detected.Facts["ReadoutMode"].Source=="User","Explicit retain decision lost");
            });
            Test("Calibration decisions explain offset, readout and identity mismatch",()=> {
                Frame light=CalibrationFixture("Light",30),dark=CalibrationFixture("Dark",30);
                Check(CalibrationMatching.Evaluate(light,dark).Accepted,"Matching calibration rejected");
                dark.Offset=11;
                Check(CalibrationMatching.Evaluate(light,dark).Reasons.Any(r=>r.Contains("Offset differs")),"Offset not checked");
                dark.Offset=null;
                Check(CalibrationMatching.Evaluate(light,dark).Status=="Needs review","Missing offset auto-matched");
                dark.Offset=10;
                dark.CameraId="Other";
                Check(!Exporter.MatchesCalibration(light,dark),"Different serial matched");
                dark.CameraId=light.CameraId;
                dark.ReadoutMode="Other";
                Check(!Exporter.MatchesCalibration(light,dark),"Different readout matched");
            });
            Test("Dark-flat recipe uses flat exposure and excludes it from light darks",()=> {
                var light=CalibrationFixture("Light",30);
                light.Calibration="Uncalibrated";
                Frame flat=CalibrationFixture("Flat",0.5),darkFlat=CalibrationFixture("Dark flat",0.5),dark=CalibrationFixture("Dark",30);
                darkFlat.Hash="flat-dark";
                flat.Hash="flat";
                dark.Hash="dark";
                var matches=Exporter.CalibrationFor(new[] {
                    light
                },new List<Frame> {
                    flat,darkFlat,dark
                },false);
                Check(matches.Count==3&&matches.Contains(darkFlat),"Two-stage recipe missing");
                Check(!Exporter.MatchesCalibration(light,darkFlat)&&Exporter.MatchesCalibration(flat,darkFlat),"Dark flat applied to lights");
                darkFlat.Exposure=1;
                Check(!Exporter.CalibrationFor(new[] {
                    light
                },new List<Frame> {
                    flat,darkFlat,dark
                },false).Contains(darkFlat),"Wrong flat exposure accepted");
            });
            Test("Mixed flat exposures receive separate compatible dark-flat sets",()=> {
                var light=CalibrationFixture("Light",30);
                light.Calibration="Uncalibrated";
                Frame flatA=CalibrationFixture("Flat",0.5),flatB=CalibrationFixture("Flat",1),darkA=CalibrationFixture("Dark flat",0.5),darkB=CalibrationFixture("Dark flat",1);
                flatA.Hash="flat-a";
                flatB.Hash="flat-b";
                darkA.Hash="dark-a";
                darkB.Hash="dark-b";
                var matched=Exporter.CalibrationFor(new[] {
                    light
                },new List<Frame> {
                    flatA,flatB,darkA,darkB
                },false);
                Check(matched.Count==4&&matched.Contains(darkA)&&matched.Contains(darkB),"An exposure-specific flat recipe was dropped");
            });
            Test("Calibration conflicts require review until a user resolves the affected field",()=> {
                var light=CalibrationFixture("Light",30);
                var dark=CalibrationFixture("Dark",30);
                dark.MetadataConflicts=new List<string> {
                    "CameraModel: CAMMODEL=ASI2600MC; INSTRUME=Other"
                };
                Check(CalibrationMatching.Evaluate(light,dark).Status=="Needs review","Unresolved conflict accepted");
                Assets.UserFact(dark,"CameraModel");
                Check(CalibrationMatching.Evaluate(light,dark).Accepted,"Explicit override did not resolve matching");
                dark.IntegrityIssue="Checksum mismatch";
                Check(!CalibrationMatching.Evaluate(light,dark).Accepted,"Integrity-failed calibration accepted");
            });
            Test("Converted export records source and output identity without modifying originals",()=> {
                string source=Path.Combine(root,"converted-mirror");
                Directory.CreateDirectory(source);
                string p=MakeXisf("converted-source.xisf",new byte[] {
                    12,0,44,1,0,125,255,255
                },"UInt16",null,"<Property id=\"PixInsight:Image:Linear\" value=\"true\"/><FITSKeyword name=\"IMAGETYP\" value=\"'Light'\"/>");
                File.Copy(p,Path.Combine(source,"Light_M45.xisf"));
                using(var repo=new Repository(Path.Combine(root,"converted-repository"))) {
                    var plan=repo.Scan(source,"Rig","Auto",ct,NoProgress);
                    repo.Import(plan.Frames,ct,NoProgress);
                    var selection=repo.All();
                    var options=new ExportOptions {
                        Parent=root,Name="converted-project",IncludeCalibration=false,ConvertToFits=true
                    };
                    string folder=Exporter.Create(repo,selection,options,ct,NoProgress);
                    string converted=Directory.GetFiles(folder,"*.fits",SearchOption.AllDirectories).Single();
                    Check(Fits.ReadPixels(converted,Fits.Inspect(converted).Images[0],0,ct).Pixels[3]==65535,"Derived export values wrong");
                    Check(Util.Hash(repo.FilePath(selection[0]),ct)==selection[0].Hash,"Archive modified");
                    string manifest=File.ReadAllText(Path.Combine(folder,"manifest.json"));
                    Check(manifest.Contains("SourceHash")&&manifest.Contains("OutputHash")&&manifest.Contains("Decoded physical values"),"Conversion provenance absent");
                    options.Name="unconverted-project";
                    options.ConvertToFits=false;
                    Expect(()=>Exporter.Create(repo,selection,options,ct,NoProgress),"Implicit conversion accepted");
                }
            });
            Test("Recognised associated metadata survives source removal and verified export",()=> {
                string source=Path.Combine(root,"associated-mirror");
                Directory.CreateDirectory(source);
                string p=Path.Combine(source,"Light_M45.fits");
                Write(p,4,4,(x,y)=>100,new Dictionary<string,string>());
                File.WriteAllText(Path.Combine(source,"session.json"),"{\"session\":\"example\"}");
                using(var repo=new Repository(Path.Combine(root,"associated-repo"))) {
                    var plan=repo.Scan(source,"Rig","Auto",ct,NoProgress);
                    repo.Import(plan.Frames,ct,NoProgress);
                    var frame=repo.All().Single();
                    Check(frame.AssociatedFiles.Count==1&&File.Exists(Path.Combine(repo.Root,frame.AssociatedFiles[0].RelativePath)),"Sidecar not preserved");
                    Directory.Delete(source,true);
                    string folder=Exporter.Create(repo,new List<Frame> {
                        frame
                    },new ExportOptions {
                        Parent=root,Name="associated-export",Mode="Files"
                    },ct,NoProgress);
                    Check(Directory.GetFiles(Path.Combine(folder,"session-metadata")).Length==1,"Sidecar not exported");
                }
            });
        }
        [DllImport("cfitsio.dll",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Ansi)]static extern int ffinit(out IntPtr file,string name,ref int status);
        [DllImport("cfitsio.dll",CallingConvention=CallingConvention.Cdecl)]static extern int ffcrimll(IntPtr file,int bitpix,int axes,long[] lengths,ref int status);
        [DllImport("cfitsio.dll",CallingConvention=CallingConvention.Cdecl)]static extern int ffppr(IntPtr file,int type,long first,long count,short[] values,ref int status);
        [DllImport("cfitsio.dll",CallingConvention=CallingConvention.Cdecl)]static extern int ffclos(IntPtr file,ref int status);
        static Frame CalibrationFixture(string kind,double exposure) {
            return new Frame {
                Telescope="Rig",Camera="Primary",CameraId="Cam-01",CameraModel="ASI2600MC",Make="Other",Kind=kind,Width=2,Height=2,Channels=1,BinX=1,BinY=1,Bayer="RGGB",Gain=100,Offset=10,ReadoutMode="Normal",GainUnit="camera units",Temperature=-10,Exposure=exposure,Filter="L",Night="2026-10-06",OpticalConfiguration="Rig-L-01",Calibration="Calibration frame"
            };
        }
        static string MakeXisf(string name,byte[] data,string format,string compression,string metadata) {
            string xml="<?xml version=\"1.0\"?><xisf version=\"1.0\"><Image geometry=\"2:2:1\" sampleFormat=\""+format+"\" location=\"attachment:4096:"+data.Length+"\""+(compression==null?"":" compression=\""+compression+"\"")+">"+metadata+"</Image></xisf>";
            byte[] header=Encoding.UTF8.GetBytes(xml);
            string path=Path.Combine(root,name);
            using(var stream=File.Create(path)) {
                stream.Write(Encoding.ASCII.GetBytes("XISF0100"),0,8);
                byte[] length=BitConverter.GetBytes(header.Length);
                stream.Write(length,0,4);
                stream.Write(new byte[4],0,4);
                stream.Write(header,0,header.Length);
                stream.SetLength(4096);
                stream.Position=4096;
                stream.Write(data,0,data.Length);
            }
            return path;
        }
        static byte[] ZlibFixture(byte[] raw) {
            using(var result=new MemoryStream()) {
                result.WriteByte(0x78);
                result.WriteByte(0x9c);
                using(var compressed=new MemoryStream()) {
                    using(var zip=new DeflateStream(compressed,CompressionMode.Compress,true))zip.Write(raw,0,raw.Length);
                    var bytes=compressed.ToArray();
                    result.Write(bytes,0,bytes.Length);
                }
                uint a=1,b=0;
                foreach(byte value in raw) {
                    a=(a+value)%65521;
                    b=(b+a)%65521;
                }
                uint checksum=b<<16|a;
                for(int i=3;i>=0;i--)result.WriteByte((byte)(checksum>>(i*8)));
                return result.ToArray();
            }
        }
        static string MakeSer(string name,int color,int endian,int bits,int frames,byte[] data) {
            byte[] header=new byte[178];
            Buffer.BlockCopy(Encoding.ASCII.GetBytes("LUCAM-RECORDER"),0,header,0,14);
            int[] values= {
                color,endian,2,2,bits,frames
            };
            for(int i=0;i<values.Length;i++)Buffer.BlockCopy(BitConverter.GetBytes(values[i]),0,header,18+i*4,4);
            Buffer.BlockCopy(BitConverter.GetBytes(new DateTime(2026,10,6,21,0,0,DateTimeKind.Utc).Ticks),0,header,170,8);
            string path=Path.Combine(root,name);
            using(var stream=File.Create(path)) {
                stream.Write(header,0,header.Length);
                stream.Write(data,0,data.Length);
            }
            return path;
        }
    }
}
