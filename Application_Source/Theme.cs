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
   // Accent-coloured text (links, captions, callout labels): the accent itself is too dim for small text on dark surfaces.
   var link=new SolidColorBrush((Color)ColorConverter.ConvertFromString(contrast?(dark?"#8AB4FF":"#1D4ED8"):(dark?"#B7B4FF":"#5951D6")));link.Freeze();window.Resources["Link"]=link;
   window.Resources["AccentText"]=new SolidColorBrush(contrast||!dark?Colors.White:Color.FromRgb(12,18,32));
   string[] controlKeys={"ControlBorder","DisabledSurface","DisabledText","DisabledBorder","SelectionIndicator"};
   string[] controlColours=contrast?(dark?new[]{"#FFFFFF","#151515","#FFFFFF","#FFFFFF","#FFFFFF"}:new[]{"#000000","#F3F3F3","#000000","#000000","#000000"}):(dark?new[]{"#94A5C0","#202C40","#A8B5CA","#71829D","#B7B4FF"}:new[]{"#64748B","#E3E8F0","#536077","#64748B","#5951D6"});
   for(int i=0;i<controlKeys.Length;i++){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(controlColours[i]));brush.Freeze();window.Resources[controlKeys[i]]=brush;}
   // Starred rows: a faint warm tint that stays behind selection, plus an amber star.
   string[] starKeys={"Starred","StarAccent"};string[] starColours=contrast?(dark?new[]{"#3A3000","#FFD54A"}:new[]{"#FFF0B3","#000000"}):(dark?new[]{"#2D2A26","#FBBF24"}:new[]{"#FFF8E5","#B27800"});
   for(int i=0;i<starKeys.Length;i++){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(starColours[i]));brush.Freeze();window.Resources[starKeys[i]]=brush;}
   // Quality scores: green / amber / red text for the table. The number and the tooltip always carry the meaning, never colour alone.
   string[] qualityKeys={"QualityHigh","QualityMid","QualityLow"};string[] qualityColours=contrast?(dark?new[]{"#7CFF9B","#FFD54A","#FF8A8A"}:new[]{"#005A1E","#6B4500","#8F0000"}):(dark?new[]{"#4ADE80","#FBBF24","#FA8585"}:new[]{"#14703A","#A04A08","#B91C1C"});
   for(int i=0;i<qualityKeys.Length;i++){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(qualityColours[i]));brush.Freeze();window.Resources[qualityKeys[i]]=brush;}
   // Preview target circles and their labels are white in every theme: they sit on dark sky, with a dark outline for bright nebulae.
   window.Resources["TargetMark"]=Brushes.White;
   var support=new SolidColorBrush((Color)ColorConverter.ConvertFromString(contrast?(dark?"#183E69":"#DDEBFF"):(dark?"#29314B":"#E7E9F8")));support.Freeze();window.Resources["SupportSurface"]=support;
   // Tooltips and popup windows have separate visual trees. Share the palette and
   // implicit control styles at application scope so they resolve the same colours.
   if(Application.Current!=null&&!Application.Current.Resources.MergedDictionaries.Contains(window.Resources))Application.Current.Resources.MergedDictionaries.Add(window.Resources);
   object[] systemKeys={SystemColors.WindowBrushKey,SystemColors.WindowTextBrushKey,SystemColors.ControlBrushKey,SystemColors.ControlTextBrushKey,SystemColors.InfoBrushKey,SystemColors.InfoTextBrushKey,SystemColors.HighlightBrushKey,SystemColors.HighlightTextBrushKey,SystemColors.GrayTextBrushKey};
   string[] palette={"Surface","Text","SurfaceAlt","Text","Surface","Text","Selection","Text","DisabledText"};
   for(int i=0;i<systemKeys.Length;i++)window.Resources[systemKeys[i]]=window.Resources[palette[i]];
  }
  public static void Bind(FrameworkElement element,DependencyProperty property,string key){element.SetResourceReference(property,key);}
 }
}
