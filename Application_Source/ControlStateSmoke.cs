// Render and exercise real WPF controls in the shared themes, including popup trees.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using ShapePath=System.Windows.Shapes.Path;
namespace AstroArchive {
 public partial class MainUi {
  static void ControlContrast(Brush first,Brush second,double minimum,string label){var a=(SolidColorBrush)first;var b=(SolidColorBrush)second;double x=Luminance(a.Color),y=Luminance(b.Color);if((Math.Max(x,y)+0.05)/(Math.Min(x,y)+0.05)<minimum)throw new Exception(label+" has insufficient contrast: "+a+" on "+b);}
  static byte[] ControlPixels(FrameworkElement element){var bitmap=PopupBitmap(element);var bytes=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(bytes,bitmap.PixelWidth*4,0);return bytes;}
  static T ControlPart<T>(Control control,string name) where T:DependencyObject {control.ApplyTemplate();var part=control.Template.FindName(name,control) as T;if(part==null)throw new Exception("Control is missing its visible state: "+name);return part;}
  static void ToggleChoice(CheckBox choice){((IToggleProvider)new CheckBoxAutomationPeer(choice).GetPattern(PatternInterface.Toggle)).Toggle();PumpPopupLayout();}
  public void SmokeControls(string output){Directory.CreateDirectory(output);Window.Show();PumpPopupLayout();SmokeControlStates(output);}
  void SmokeControlStates(string output){
   string previousTheme=settings.ThemeMode;int previousScale=settings.TextScalePercent;bool previousContrast=settings.HighContrast;
   try{foreach(string mode in new[]{"Dark","Light"})foreach(bool contrast in new[]{false,true})foreach(int scale in new[]{100,150}){
    settings.ThemeMode=mode;settings.HighContrast=contrast;settings.TextScalePercent=scale;ApplyAppearance();string label=mode+(contrast?"_Contrast":"")+scale;
    var dialog=new FormWindow(Window,"Control states",700,590);dialog.Tabs("Choices","Actions","Review","Options");
    var off=dialog.Check("Unchecked — keep source files",false);var on=dialog.Check("Checked — delete verified copies",true);var mixed=dialog.Check("Mixed selection",false);mixed.IsThreeState=true;mixed.IsChecked=null;
    var disabledOff=dialog.Check("Unavailable, unchecked",false);disabledOff.IsEnabled=false;var disabledOn=dialog.Check("Unavailable, checked",true);disabledOn.IsEnabled=false;var disabledMixed=dialog.Check("Unavailable, mixed",false);disabledMixed.IsThreeState=true;disabledMixed.IsChecked=null;disabledMixed.IsEnabled=false;
    dialog.Tab(1);int clicked=0;var button=dialog.Button("Working action",()=>clicked++);var disabledButton=dialog.Button("Unavailable action",()=>clicked++);disabledButton.IsEnabled=false;
    dialog.Select("Available selection",new[]{"Keep files","Delete files"},"Keep files");var disabledCombo=dialog.Select("Unavailable selection",new[]{"Recorded choice"},"Recorded choice");disabledCombo.IsEnabled=false;
    var editable=new ComboBox{IsEditable=true,IsTextSearchEnabled=false,Text="M31",ItemsSource=new[]{"M31","M51"}};dialog.Add(editable);
    var list=new ListBox{ItemsSource=new[]{"Selected item","Other item"},SelectedIndex=0,Height=90};dialog.Add(list);
    dialog.Tab(2);var review=EditedImportReviewTable(new EditedImportPlan{Images={new EditedImportCandidate{RelativePath="new.fit",Include=true},new EditedImportCandidate{RelativePath="duplicate.fit",DuplicateReason="Already in Edited"}}});dialog.Add(review);
    dialog.Tab(3);var options=dialog.Advanced("Expandable options",()=>dialog.Text("Visible when expanded"));var unavailable=dialog.Advanced("Unavailable options, expanded",()=>dialog.Text("Recorded choice remains visible"));unavailable.IsExpanded=true;unavailable.IsEnabled=false;dialog.CloseOnly();
    try{dialog.Window.Show();PumpPopupLayout();
     foreach(var choice in new[]{off,on,mixed,disabledOff,disabledOn,disabledMixed}){
      var box=ControlPart<Border>(choice,"ChoiceBox");var tick=ControlPart<ShapePath>(choice,"Checkmark");var dash=ControlPart<System.Windows.Shapes.Rectangle>(choice,"MixedMark");
      if((tick.Visibility==Visibility.Visible)!=(choice.IsChecked==true)||(dash.Visibility==Visibility.Visible)!=(!choice.IsChecked.HasValue)||Math.Abs(box.Width-18.0*scale/100)>0.1)throw new Exception(label+" checkbox state or size is hidden");
      if(choice.IsChecked==false)ControlContrast(box.BorderBrush,box.Background,3,label+" empty checkbox outline");else ControlContrast(choice.IsChecked==true?tick.Stroke:dash.Fill,box.Background,3,label+" selection glyph");
     }
     var before=ControlPixels(off);ToggleChoice(off);if(off.IsChecked!=true||before.SequenceEqual(ControlPixels(off)))throw new Exception(label+" clicking a checkbox did not visibly check it");ToggleChoice(off);if(off.IsChecked!=false)throw new Exception("Checkbox cannot be unchecked");
     bool rejected=false;try{ToggleChoice(disabledOn);}catch(ElementNotEnabledException){rejected=true;}if(!rejected||disabledOn.IsChecked!=true)throw new Exception("Disabled checkbox changed its saved selection");
     off.Focus();PumpPopupLayout();if(ControlPart<Border>(off,"ChoiceFocus").Visibility!=Visibility.Visible)throw new Exception(label+" checkbox keyboard focus is hidden");on.Focus();PumpPopupLayout();
     CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Control_Choices_"+label+".png"));
     dialog.SelectTab(1);PumpPopupLayout();((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();PumpPopupLayout();if(clicked!=1)throw new Exception("Working button did not invoke its action once");
     var setPressed=typeof(ButtonBase).GetMethod("SetIsPressed",BindingFlags.Instance|BindingFlags.NonPublic);if(setPressed==null)throw new Exception("Button press fixture is unavailable");setPressed.Invoke(button,new object[]{true});PumpPopupLayout();if(ControlPart<Border>(button,"ButtonFocus").Visibility!=Visibility.Visible||!(ControlPart<Grid>(button,"ButtonRoot").RenderTransform is TranslateTransform))throw new Exception(label+" pressed button feedback is hidden");setPressed.Invoke(button,new object[]{false});
     var disabledSurface=ControlPart<Border>(disabledButton,"Surface");ControlContrast(ControlPart<ContentPresenter>(disabledButton,"ButtonLabel").GetValue(TextElement.ForegroundProperty) as Brush,disabledSurface.Background,4.5,label+" disabled action label");
     if(disabledButton.Opacity!=1||disabledCombo.Opacity!=1||!ToolTipService.GetShowOnDisabled(disabledButton))throw new Exception("Disabled actions hide labels or explanations");
     var input=ControlPart<TextBox>(editable,"PART_EditableTextBox");if(input.Visibility!=Visibility.Visible)throw new Exception("Editable target selection has no working input");input.Text="C27";PumpPopupLayout();if(editable.Text!="C27")throw new Exception("Typing into a target selection does not update its value");
     var row=list.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;if(row==null||!row.IsSelected)throw new Exception("List selection disappeared");var rowBorder=(Border)row.Template.FindName("Item",row);ControlContrast(rowBorder.Background,Window.Resources["Text"] as Brush,4.5,label+" selected row text");ControlContrast(rowBorder.BorderBrush,rowBorder.Background,3,label+" selected row indicator");
     CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Control_Actions_"+label+".png"));
     var menu=ThemedMenu();var menuOff=new MenuItem{Header="Off option",IsCheckable=true};var menuOn=new MenuItem{Header="On option",IsCheckable=true,IsChecked=true};var menuDisabled=new MenuItem{Header="Unavailable, on",IsCheckable=true,IsChecked=true,IsEnabled=false};menu.Items.Add(menuOff);menu.Items.Add(menuOn);menu.Items.Add(menuDisabled);menu.PlacementTarget=button;menu.IsOpen=true;PumpPopupLayout();
     try{foreach(var item in new[]{menuOff,menuOn,menuDisabled}){if(ControlPart<Border>(item,"MenuCheckBox").Visibility!=Visibility.Visible||(ControlPart<ShapePath>(item,"Checkmark").Visibility==Visibility.Visible)!=item.IsChecked)throw new Exception("Menu option state is hidden");}SavePopup(menu,Path.Combine(output,"AstroArchive_Control_Menu_"+label+".png"));}finally{menu.IsOpen=false;}
     dialog.SelectTab(2);PumpPopupLayout();if(PopupChildren<CheckBox>(review).Any(choice=>choice.Template==null||choice.Template.FindName("ChoiceBox",choice)==null))throw new Exception("Review table bypasses the shared checkbox style");
     CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Control_Review_"+label+".png"));
     dialog.SelectTab(3);PumpPopupLayout();var disclosure=ControlPart<ToggleButton>(options,"Disclosure");((IToggleProvider)new ToggleButtonAutomationPeer(disclosure).GetPattern(PatternInterface.Toggle)).Toggle();PumpPopupLayout();if(!options.IsExpanded||ControlPart<ContentPresenter>(options,"ExpandedContent").Visibility!=Visibility.Visible)throw new Exception("Expandable options do not respond to a toggle");
     var arrow=ControlPart<ShapePath>(disclosure,"DisclosureArrow");if(!(arrow.RenderTransform is RotateTransform)||((RotateTransform)arrow.RenderTransform).Angle!=90)throw new Exception("Expanded options lack a visible open indicator");
     var inactive=ControlPart<ToggleButton>(unavailable,"Disclosure");if(inactive.IsEnabled||!unavailable.IsExpanded)throw new Exception("Unavailable options lost their recorded state");ControlContrast(ControlPart<ShapePath>(inactive,"DisclosureArrow").Stroke,ControlPart<Border>(inactive,"DisclosureSurface").Background,3,label+" disabled disclosure arrow");
     foreach(TabItem tab in PopupChildren<TabControl>(dialog.Window).Single().Items){var indicator=ControlPart<Border>(tab,"TabSelection");if((indicator.Visibility==Visibility.Visible)!=tab.IsSelected)throw new Exception("Active tab underline does not identify the selected page");if(tab.IsSelected)ControlContrast(indicator.Background,ControlPart<Border>(tab,"Tab").Background,3,label+" active tab indicator");}
     CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Control_Options_"+label+".png"));
    }finally{dialog.Window.Close();}
   }
   SmokeOriginalsChoiceStatus(output);
   File.WriteAllText(Path.Combine(output,"control-states-smoke.txt"),"PASS: actual checkbox clicks change pixels and saved states; empty, checked, mixed and disabled selections remain legible; working actions invoke once; pressed and keyboard focus feedback; disabled action contrast and tooltips; editable target input; persistent list/menu selection; review table checkboxes; deletion On/Off feedback and disabled explanation; Dark/Light/high contrast at 100/150% text.");
   }finally{settings.ThemeMode=previousTheme;settings.TextScalePercent=previousScale;settings.HighContrast=previousContrast;ApplyAppearance();}
  }
  void SmokeOriginalsChoiceStatus(string output){
   SmokeSourceRemovalConfirmation();
   var original=(CheckBox)Window.FindName("DeleteOriginalsCheck");bool enabled=original.IsEnabled;bool? selected=original.IsChecked;
   try{original.IsChecked=false;original.IsEnabled=false;var dialog=new FormWindow(Window,"Import option feedback",700,480);var fields=AddImportPreferences(dialog);fields.Current.IsExpanded=true;
    try{dialog.Window.Show();PumpPopupLayout();var status=PopupChildren<TextBlock>(dialog.Window).Single(t=>t.Text.StartsWith("Off — source originals"));if(fields.Originals.IsEnabled||status.Text.IndexOf("scan a source folder",StringComparison.OrdinalIgnoreCase)<0||!ToolTipService.GetShowOnDisabled(fields.Originals))throw new Exception("Unavailable deletion option gives no explanation");
     fields.Originals.IsEnabled=true;ToggleChoice(fields.Originals);if(!status.Text.StartsWith("On —")||status.Text.Contains("Scan a source"))throw new Exception("Deletion checkbox lacks immediate On feedback");ToggleChoice(fields.Originals);if(!status.Text.StartsWith("Off —"))throw new Exception("Deletion checkbox lacks immediate Off feedback");
     original.IsChecked=true;if(!L("ImportPolicyLabel").Text.StartsWith("Delete after import ON"))throw new Exception("Deletion policy summary did not follow the selected option");original.IsChecked=false;if(!L("ImportPolicyLabel").Text.StartsWith("Originals kept"))throw new Exception("Deletion policy summary did not follow the cleared option");CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Delete_Option_Feedback.png"));
    }finally{dialog.Window.Close();}
   }finally{original.IsEnabled=enabled;original.IsChecked=selected;}
  }
  void SmokeSourceRemovalConfirmation(){
   foreach(bool dump in new[]{false,true}){
    var dialog=SourceDeletionConfirmation("C:\\source-removal-fixture",dump);bool canceled=false;Exception failure=null;
    Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>{
     try{PumpPopupLayout();var texts=PopupChildren<TextBlock>(dialog.Window).Select(t=>t.Text).ToArray();if(!texts.Contains("Delete after import is ON")||!texts.Any(t=>t.Contains("Calibration originals always stay"))||!texts.Any(t=>t.Contains("source-removal-fixture")))throw new Exception("Source removal confirmation omits deletion state, source or calibration protection.");
      var proceed=PopupChildren<Button>(dialog.Window).Single(b=>Convert.ToString(b.Content)=="Import and delete eligible originals");if(proceed.IsDefault)throw new Exception("Enter implicitly approves source deletion.");
      PopupChildren<Button>(dialog.Window).Single(b=>Convert.ToString(b.Content)=="Cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));canceled=true;
     }catch(Exception e){failure=e;dialog.Window.Close();}
    }));
    if(dialog.Show()||!canceled)throw new Exception("Cancel approved a source-removing import.");if(failure!=null)throw failure;
   }
  }
 }
}
