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
   bool dark=IsDark(mode);
   string[] keys={"Canvas","Surface","SurfaceAlt","Border","Text","Muted","Accent","Selection"};
   string[] light={"#F3F5FA","#FFFFFF","#F5F7FC","#DCE2EF","#202B43","#65748F","#5951D6","#E8E8FC"};
   string[] night={"#0C1220","#141D2E","#1B263B","#2A3851","#EDF2FC","#94A5C0","#7772EE","#30385D"};
   for(int i=0;i<keys.Length;i++){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString((dark?night:light)[i]));brush.Freeze();window.Resources[keys[i]]=brush;}
  }
  public static void Bind(FrameworkElement element,DependencyProperty property,string key){element.SetResourceReference(property,key);}
 }
}
