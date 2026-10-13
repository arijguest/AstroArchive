using System;
using System.Drawing;
using System.Windows.Forms;

namespace AstroArchive.Installation {
 // Design values are DIP. WinForms bounds/minima include borders and taskbar.
 public static class InstallerLayout {
  sealed class Profile {public string Language,ThemeMode;public bool HighContrast;}
  static Profile ProfileSettings(){try{string path=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AstroArchive","settings.json");InstallCore.NoLinks(path);return System.IO.File.Exists(path)&&new System.IO.FileInfo(path).Length<2*1024*1024?new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Profile>(System.IO.File.ReadAllText(path)):new Profile();}catch{return new Profile();}}
  public static void InitializeLanguage(){AstroArchive.Localization.Use(ProfileSettings().Language);}
  public static void Style(Form form){var profile=ProfileSettings();bool dark=profile.ThemeMode=="Dark";
   if(profile.ThemeMode==null||profile.ThemeMode=="System")try{dark=Convert.ToInt32(Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1))==0;}catch{}
   bool contrast=profile.HighContrast||SystemInformation.HighContrast;Color canvas=contrast?SystemColors.Window:dark?Color.FromArgb(12,18,32):Color.FromArgb(242,245,250),surface=contrast?SystemColors.Window:dark?Color.FromArgb(23,32,49):Color.White,text=contrast?SystemColors.WindowText:dark?Color.FromArgb(229,235,246):Color.FromArgb(36,50,71);
   Action<Control> visit=null;visit=control=>{control.Text=AstroArchive.Localization.T(control.Text);if(control.BackColor==Color.FromArgb(77,85,199)){control.BackColor=contrast?SystemColors.Highlight:dark?Color.FromArgb(147,153,255):Color.FromArgb(77,85,199);control.ForeColor=contrast?SystemColors.HighlightText:dark?Color.FromArgb(12,18,32):Color.White;}else if(control.BackColor!=Color.FromArgb(17,28,55)){control.BackColor=control is TextBox||control is Button?surface:canvas;control.ForeColor=text;}foreach(Control child in control.Controls)visit(child);};visit(form);
  }

  public static void Configure(Form form,int width,int height,int minWidth,int minHeight){
   form.AutoScaleDimensions=new SizeF(96,96);form.AutoScaleMode=AutoScaleMode.Dpi;form.ClientSize=new Size(width,height);
   Action clamp=()=>{var area=Screen.FromControl(form).WorkingArea;float dpi;using(var graphics=form.CreateGraphics())dpi=graphics.DpiX/96f;
    int margin=Math.Max(8,(int)(12*dpi));int availableWidth=Math.Max(160,area.Width-margin*2),availableHeight=Math.Max(140,area.Height-margin*2);
    form.MinimumSize=new Size(Math.Min(availableWidth,(int)(minWidth*dpi)),Math.Min(availableHeight,(int)(minHeight*dpi)));
    form.Size=new Size(Math.Min(availableWidth,form.Width),Math.Min(availableHeight,form.Height));
    form.Location=new Point(Math.Max(area.Left+margin,Math.Min(area.Right-margin-form.Width,form.Left)),Math.Max(area.Top+margin,Math.Min(area.Bottom-margin-form.Height,form.Top)));
   };
   form.Shown+=(s,e)=>clamp();form.LocationChanged+=(s,e)=>{if(form.Visible)clamp();};form.DpiChanged+=(s,e)=>clamp();
  }
 }
}
