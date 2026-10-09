using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AstroArchive {
    public partial class MainUi {
        void PreviewFile(Frame original) {
            if(repo==null||RepositoryOperationBlocked||original==null)return;
            var d=new FormWindow(Window,"Choose image / frame",880,780);
            d.Text(original.OriginalName,true);
            d.Text(original.CapabilityText+"\nPreview stretch changes display only. Choose the image to use for analysis and export.");
            Frame inspected=original.Clone();
            string path=repo.FilePath(original);
            try {
                if(inspected.Images==null) {
                    var info=Assets.Inspect(path);
                    inspected.Images=info.Images;
                }
            }
            catch(Exception ex) {
                MessageBox.Show(Window,ex.Message,"Preview unavailable");
                return;
            }
            var images=inspected.Images??new List<ImageDescriptor>();
            if(images.Count==0) {
                ShowReport("File metadata",MetadataReport(original));
                return;
            }
            var choice=new ComboBox {
                ItemsSource=images,DisplayMemberPath="Label",SelectedItem=Assets.Selected(inspected),Margin=new Thickness(0,8,0,8)
            };
            d.Add(choice);
            TextBox index=d.Input("Frame / slice number (1-based)",((original.ImageIndex??0)+1).ToString());
            var status=new TextBlock {
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)
            };
            d.Add(status);
            var view=new Image {
                Height=380,Stretch=Stretch.Uniform
            };
            d.Add(view);
            var load=new Button {
                Content="Load preview",HorizontalAlignment=HorizontalAlignment.Left
            };
            d.Add(load);
            var token=new CancellationTokenSource();
            bool busy=false;
            Frame chosen=null;
            load.Click+=async(s,e)=> {
                if(busy)return;
                var descriptor=choice.SelectedItem as ImageDescriptor;
                int frame;
                if(descriptor==null||!int.TryParse(index.Text,out frame)||frame<1||frame>descriptor.Count) {
                    status.Text="Choose a frame between 1 and "+(descriptor==null?1:descriptor.Count)+".";
                    return;
                }
                busy=true;
                load.IsEnabled=false;
                status.Text="Reading image…";
                var snapshot=inspected.Clone();
                snapshot.ImageKey=descriptor.Key;
                snapshot.ImageIndex=frame-1;
                try {
                    var bitmap=await Task.Run(()=> {
                        repo.ValidateCapture(original,token.Token);
                        var pixels=Assets.Display(snapshot,path,frame-1,token.Token);
                        byte[] rgb=pixels.Render(settings.PreviewStretch??"Auto",token.Token);
                        var result=BitmapSource.Create(pixels.Width,pixels.Height,96,96,PixelFormats.Rgb24,null,rgb,pixels.Width*3);
                        result.Freeze();
                        return result;
                    },token.Token);
                    if(!token.IsCancellationRequested) {
                        view.Source=bitmap;
                        chosen=snapshot;
                        status.Text=descriptor.Width+" × "+descriptor.Height+" · "+descriptor.Channels+" channel(s) · frame "+frame+" / "+descriptor.Count;
                    }
                }
                catch(OperationCanceledException) {
                }
                catch(Exception ex) {
                    if(!token.IsCancellationRequested)status.Text=ex.Message;
                }
                finally {
                    busy=false;
                    if(!token.IsCancellationRequested)load.IsEnabled=true;
                }
            };
            d.Button("View metadata and evidence",()=>ShowReport("Metadata and evidence",MetadataReport(original)));
            bool select=false;
            d.Button("Use previewed image for analysis and export",()=> {
                if(chosen==null||busy) {
                    status.Text="Load an image first.";
                    return;
                }
                select=true;
                d.Window.DialogResult=true;
            });
            d.CloseOnly();
            d.Window.Loaded+=(s,e)=>load.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            d.Window.Closed+=(s,e)=>token.Cancel();
            d.Show();
            if(select) {
                Run(ct=> {
                    repo.ValidateCapture(original,ct);
                    var candidate=original.Clone();
                    candidate.ImageKey=chosen.ImageKey;
                    candidate.ImageIndex=chosen.ImageIndex;
                    candidate=Assets.InspectExisting(candidate,path,ct,true);
                    Assets.UserFact(candidate,"ImageKey");
                    Assets.UserFact(candidate,"ImageIndex");
                    repo.Refile(candidate,ct);
                    return "Image selection saved.";
                },message=>L("StatusLabel").Text=message);
            }
        }
        static string CalibrationReport(List<Frame> lights,List<Frame> calibrations) {
            var lines=new List<string> {
                "Only accepted candidates are supplied automatically. Missing settings require metadata review.","Dark flats are evaluated against raw flats after matching those flats to lights.",""
            };
            foreach(var light in Exporter.StackingGroups(lights,calibrations,false,true,true).Select(g=>g.Inputs.First()).Take(20)) {
                lines.Add(light.OriginalName+" · "+light.Telescope+" / "+light.Camera);
                var candidates=calibrations.Where(c=>c.Telescope==light.Telescope&&c.Camera==light.Camera).Take(200).ToList();
                foreach(var cal in candidates) {
                    var decision=CalibrationMatching.Evaluate(light,cal);
                    lines.Add(decision.Status+" · "+cal.Kind+" · "+cal.OriginalName+": "+string.Join(" ",decision.Reasons));
                }
                if(candidates.Count==0)lines.Add("No calibration files for this physical device and camera channel.");
                lines.Add("");
            }
            lines.Add("Review shows up to 20 input groups and 200 candidates per group. Export evaluates every candidate.");
            return string.Join("\n",lines);
        }
        const string FormatGuide="Import preserves original FITS, TIFF, PNG, JPEG, GIF, XISF, SER, supported video and camera RAW files. Preview support and scientific export eligibility are separate.\n\nFITS: image HDUs, gzip, RGB/Bayer data and selected cube slices. Tile-compressed .fz needs optional CFITSIO.\nTIFF/PNG/XISF: supported numeric layouts. Confirm linearity in Edit metadata before explicit FITS conversion. XISF Zstandard needs its optional codec; complex/subblock layouts remain original-file exports.\nJPEG/GIF: display and original-file export. GIFs animate in Preview.\nSER: supported 8/16-bit mono, Bayer and RGB/BGR recordings, with playback and frame selection. Export the recording to planetary software.\nAVI/MP4/MOV/M4V/WMV/MKV: playback depends on installed Windows codecs. Pause / Play appears below moving images.\nCR2/CR3/NEF/ARW/DNG: original-file import/export; external preview needs a compatible Windows image codec.\n\nReady-to-stack exports can explicitly convert eligible linear images without a display stretch. Originals remain intact. Numeric decoding/conversion is bounded to 32 million channel samples per image; 64-bit integer conversion is refused to avoid precision loss.\n\nInstall compatible x64 cfitsio.dll/libzstd.dll and dependencies in %LOCALAPPDATA%\\AstroArchive\\codecs, then restart. Optional codecs are not bundled and survive app updates.";
        static string MetadataReport(Frame f) {
            return f.OriginalName+"\n"+f.CapabilityText+"\n\n"+string.Join("\n",typeof(Frame).GetProperties().Where(p=>p.CanRead&&(p.PropertyType==typeof(string)||p.PropertyType==typeof(double?)||p.PropertyType==typeof(bool?))).Select(p=>p.Name+": "+Convert.ToString(p.GetValue(f,null),CultureInfo.InvariantCulture)))+"\n\nEvidence\n"+(f.Facts==null?"Legacy record; re-detect metadata to populate evidence.":string.Join("\n",f.Facts.OrderBy(p=>p.Key).Select(p=>p.Key+": "+p.Value.Value+" · "+p.Value.Source+(string.IsNullOrEmpty(p.Value.Unit)?"":" · "+p.Value.Unit)+" · raw: "+p.Value.Raw)))+"\n\nConflicts\n"+string.Join("\n",f.MetadataConflicts??new List<string>());
        }
        void ReviewMetadata(List<Frame> selected) {
            var candidates=new List<Frame>();
            Run(ct=> {
                foreach(var frame in selected) {
                    repo.ValidateCapture(frame,ct);
                    candidates.Add(Assets.InspectExisting(frame,repo.FilePath(frame),ct));
                }
                return "";
            },ignored=> {
                var changes=new List<MetadataChange>();
                for(int i=0;i<selected.Count;i++)changes.AddRange(MetadataReview.Compare(selected[i],candidates[i]));
                var d=new FormWindow(Window,"Review detected metadata",920,650);
                d.Text("Review "+selected.Count+" files",true);
                d.Text("Recorded user overrides stay in place. Unchecked fields keep indexed values. Older, unattributed settings are kept by default and flagged for review. Image geometry follows the decoded container.");
                var grid=new DataGrid {
                    ItemsSource=changes,AutoGenerateColumns=false,Height=350
                };
                var checkStyle=new Style(typeof(CheckBox),Window.TryFindResource(typeof(CheckBox)) as Style);
                checkStyle.Setters.Add(new Setter(UIElement.IsEnabledProperty,new Binding("CanRetain")));
                grid.Columns.Add(new DataGridCheckBoxColumn {
                    Header="APPLY",Binding=new Binding("Apply") {
                        Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged
                    },ElementStyle=checkStyle,EditingElementStyle=checkStyle
                });
                foreach(string field in new[] {
                    "File","Field","Before","After"
                })grid.Columns.Add(new DataGridTextColumn {
                    Header=field,Binding=new Binding(field),IsReadOnly=true,Width=new DataGridLength(1,DataGridLengthUnitType.Star)
                });
                d.Add(grid);
                d.Accept("Apply reviewed metadata",()=> {
                    grid.CommitEdit(DataGridEditingUnit.Cell,true);
                    grid.CommitEdit(DataGridEditingUnit.Row,true);
                    return true;
                });
                if(d.Show())Run(ct=> {
                    MetadataReview.Apply(changes);
                    foreach(var candidate in candidates) {
                        repo.ValidateCapture(candidate,ct);
                        repo.Refile(candidate,ct);
                    }
                    return candidates.Count+" metadata records updated.";
                },message=>L("StatusLabel").Text=message);
            });
        }
    }
}
