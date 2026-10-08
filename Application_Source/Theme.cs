using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace AstroArchive {
 public static class Theme {
  public static bool IsDark(string mode) {
   if(mode=="Dark")return true;if(mode=="Light")return false;
   try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))return key!=null&&Convert.ToInt32(key.GetValue("AppsUseLightTheme",1))==0;}catch{return false;}
  }
  public static void Apply(Window window,string mode) {
   bool dark=IsDark(mode);bool contrast=SystemParameters.HighContrast||object.Equals(window.TryFindResource("PreferHighContrast"),true);
   string[] keys={"Canvas","Surface","SurfaceAlt","Border","Text","Muted","Accent","Selection"};
   string[] light={"#F3F5FA","#FFFFFF","#F5F7FC","#DCE2EF","#202B43","#5F6D86","#5951D6","#E8E8FC"};
   string[] night={"#0C1220","#141D2E","#1B263B","#2A3851","#EDF2FC","#94A5C0","#7772EE","#30385D"};
   if(contrast){light=new[]{"#FFFFFF","#FFFFFF","#F3F3F3","#000000","#000000","#000000","#1D4ED8","#DDEBFF"};night=new[]{"#000000","#000000","#151515","#FFFFFF","#FFFFFF","#FFFFFF","#1D4ED8","#183E69"};}
   window.Resources["Focus"]=new SolidColorBrush(dark?Color.FromRgb(251,191,36):Color.FromRgb(29,78,216));
   for(int i=0;i<keys.Length;i++){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString((dark?night:light)[i]));brush.Freeze();window.Resources[keys[i]]=brush;}
   window.Resources["AccentText"]=new SolidColorBrush(contrast||!dark?Colors.White:Color.FromRgb(12,18,32));
   var support=new SolidColorBrush((Color)ColorConverter.ConvertFromString(contrast?(dark?"#183E69":"#DDEBFF"):(dark?"#29314B":"#E7E9F8")));support.Freeze();window.Resources["SupportSurface"]=support;
   // Tooltips and popup windows have separate visual trees. Share the palette and
   // implicit control styles at application scope so they resolve the same colours.
   if(Application.Current!=null&&!Application.Current.Resources.MergedDictionaries.Contains(window.Resources))Application.Current.Resources.MergedDictionaries.Add(window.Resources);
   object[] systemKeys={SystemColors.WindowBrushKey,SystemColors.WindowTextBrushKey,SystemColors.ControlBrushKey,SystemColors.ControlTextBrushKey,SystemColors.InfoBrushKey,SystemColors.InfoTextBrushKey,SystemColors.HighlightBrushKey,SystemColors.HighlightTextBrushKey};
   string[] palette={"Surface","Text","SurfaceAlt","Text","Surface","Text","Selection","Text"};
   for(int i=0;i<systemKeys.Length;i++)window.Resources[systemKeys[i]]=window.Resources[palette[i]];
  }
  public static void Bind(FrameworkElement element,DependencyProperty property,string key){element.SetResourceReference(property,key);}
 }
}
