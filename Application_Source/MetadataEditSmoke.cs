// Native WPF coverage for current values, mixed selections and theme/accessibility.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeMetadataEditor(string output){
   var first=new Frame{OriginalName="Light_M33.fit",Target="M33",Telescope="Unit-01",Model="Custom telescope",Camera="Telephoto",Kind="Light",Mount="Unknown",Exposure=30.123456789,Gain=0,Temperature=-12.75,Filter="Broadband",Calibration="Custom calibration",BinX=2,BinY=1,TelescopeModel="Reflector",CameraModel="Camera 1",CameraId="Serial 123",Offset=0,ReadoutMode="Slow",Roi="0,0,128,96",OpticalConfiguration="Reducer",TimeZoneId="UTC",LinearData=false,GainUnit="dB",Bayer="RGGB",RegistrationState="Unregistered",CalibrationSteps="Dark, flat",Notes="Recorded notes",Facts=new Dictionary<string,MetadataFact>{{"Exposure",new MetadataFact{Value="30.123456789",Source="FITS header",Unit="s"}}}};
   var second=first.Clone();second.OriginalName="Light_M45.fit";second.Target="M45";second.Exposure=60;second.CameraId=null;second.LinearData=true;
   string originalTheme=settings.ThemeMode;int originalScale=settings.TextScalePercent;
   try{
    foreach(string mode in new[]{"Dark","Light"})foreach(double scale in new[]{1.0,1.5}){
     settings.ThemeMode=mode;settings.TextScalePercent=(int)(scale*100);ApplyAppearance();
     foreach(bool batch in new[]{false,true}){
      var editor=new MetadataEditor(Window,batch?new List<Frame>{first,second}:new List<Frame>{first});
      try{
       editor.Form.Window.Show();PumpPopupLayout();
       if(editor.Model.Patch().Count!=0)throw new Exception("Opening metadata editor generated overrides.");
       foreach(var entry in editor.Inputs){var box=entry.Value as TextBox;var combo=entry.Value as ComboBox;string shown=box!=null?box.Text:Convert.ToString(combo.SelectedItem);var value=editor.Model[entry.Key];string expected=value.Field.Options.Length>0&&value.Initial.Length==0?value.KeepLabel:value.Initial;if(shown!=expected)throw new Exception("Current metadata missing: "+entry.Key);Readable(entry.Value.Foreground,entry.Value.Background,mode+" metadata "+entry.Key);}
       if(!batch&&((ComboBox)editor.Inputs["Mount"]).SelectedItem.ToString()!="EQ?")throw new Exception("Unknown mount suggestion hidden.");
       if(batch&&(!editor.Model["Exposure"].Mixed||!editor.Model["LinearData"].Mixed||!editor.Model["CameraId"].Mixed))throw new Exception("Batch mixed metadata not identified.");
       var tabs=PopupChildren<TabControl>(editor.Form.Window).Single();
       for(int tab=0;tab<4;tab++){
        tabs.SelectedIndex=tab;PumpPopupLayout();
        foreach(var control in editor.Inputs.Values.Where(c=>c.IsVisible)){if(control.ActualWidth<120||control.ActualHeight<=0)throw new Exception("Metadata input clipped: "+control.Name);}
        foreach(var label in PopupChildren<TextBlock>(editor.Form.Window).Where(t=>t.IsVisible&&!string.IsNullOrWhiteSpace(t.Text))){
         Brush background=editor.Form.Window.Background;
         for(DependencyObject parent=VisualTreeHelper.GetParent(label);parent!=null;parent=VisualTreeHelper.GetParent(parent)){
          // A Control.Background need not be painted behind its text (CheckBox).
          // Use the rendered container surfaces in the visual tree instead.
          var border=parent as Border;var panel=parent as Panel;Brush surface=border!=null?border.Background:panel!=null?panel.Background:null;var solid=surface as SolidColorBrush;
          if(solid!=null&&solid.Color.A==255){background=surface;break;}
         }
         Readable(label.Foreground,background,mode+" metadata label: "+label.Text+" ("+label.Foreground+" on "+background+")");
        }
        if(scale==1.0&&!batch)CapturePopup(editor.Form.Window,Path.Combine(output,"AstroArchive_Metadata_"+mode+"_"+tab+".png"));
       }
       var picker=PopupChildren<ComboBox>(tabs).Single();picker.SelectedIndex=batch?1:0;PumpPopupLayout();var table=PopupChildren<DataGrid>(tabs).Single();if(!table.Items.Cast<MetadataDetail>().Any(r=>r.Field=="Original Name"&&r.Value==(batch?second:first).OriginalName))throw new Exception("Current metadata viewer did not follow selected file.");
       tabs.SelectedIndex=0;((TextBox)editor.Inputs["Filter"]).Text="Ha";PumpPopupLayout();if(editor.Model.Patch().Count!=1||editor.Model.Patch().Apply(second).Exposure!=60)throw new Exception("Native edit copied another field.");
       var reset=PopupChildren<Button>(editor.Form.Window).Single(b=>Convert.ToString(b.Content)=="Reset changes");reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(editor.Model.Patch().Count!=0)throw new Exception("Native reset retained edits.");
       if(scale==1.5){editor.Form.Window.Width=480;PumpPopupLayout();tabs.SelectedIndex=2;PumpPopupLayout();foreach(var control in editor.Inputs.Values.Where(c=>c.IsVisible))if(control.ActualWidth<180)throw new Exception("Narrow metadata form failed to reflow.");}
      }finally{editor.Form.Window.Close();}
     }
    }
   }finally{settings.ThemeMode=originalTheme;settings.TextScalePercent=originalScale;ApplyAppearance();}
  }
 }
}
