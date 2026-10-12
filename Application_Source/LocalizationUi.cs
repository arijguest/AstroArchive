using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Linq;
using System.Windows.Documents;

namespace AstroArchive {
 // Shows translated text for strings displayed through WPF content (buttons, menus,
 // combo items, headers, tooltips) while the content itself stays English, so code
 // that compares labels keeps working. Access keys (_Import) stay access keys.
 public sealed class LocalizedLabel:Decorator {
  public LocalizedLabel(){DataContextChanged+=(s,e)=>Build();Loaded+=(s,e)=>Build();}
  void Build(){
   string text=DataContext as string;if(text==null){Child=null;return;}
   var presenter=TemplatedParent as ContentPresenter;string context=LocalizedInterface.GetContext(this),shown=context!=null&&Localization.Has(context+"|"+text)?Localization.T(context+"|"+text):Localization.T(text);
   if(presenter!=null&&presenter.RecognizesAccessKey){var access=Child as AccessText??new AccessText();access.Text=context==null&&shown==text?Localization.AccessText(text):shown;if(Child!=access)Child=access;}
   else{var plain=Child as TextBlock??new TextBlock();plain.Text=shown;if(Child!=plain)Child=plain;}
  }
 }
 public static class LocalizedInterface {
  static bool installed;
  static readonly ConditionalWeakTable<TextBlock,object> tracked=new ConditionalWeakTable<TextBlock,object>();
  static readonly HashSet<TextBlock> updating=new HashSet<TextBlock>();
  static readonly ConditionalWeakTable<Window,object> windows=new ConditionalWeakTable<Window,object>();
  static readonly DependencyPropertyDescriptor textDescriptor=DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty,typeof(TextBlock));
  static readonly DependencyPropertyDescriptor titleDescriptor=DependencyPropertyDescriptor.FromProperty(Window.TitleProperty,typeof(Window));
  // Disambiguates labels such as Light and Dark, which are frame types elsewhere: a
  // "Theme" context looks up "Theme|Light" first. It is inherited by dropdown items.
  public static readonly DependencyProperty ContextProperty=DependencyProperty.RegisterAttached("Context",typeof(string),typeof(LocalizedInterface),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.Inherits));
  public static void SetContext(DependencyObject element,string value){element.SetValue(ContextProperty,value);}
  public static string GetContext(DependencyObject element){return (string)element.GetValue(ContextProperty);}
  public static void Install(){
   if(installed||Localization.English||Application.Current==null)return;installed=true;
   var template=new DataTemplate(typeof(string)){VisualTree=new FrameworkElementFactory(typeof(LocalizedLabel))};template.Seal();
   Application.Current.Resources[new DataTemplateKey(typeof(string))]=template;
   // WPF raises Loaded only where a handler is attached, so a style supplies one to every
   // TextBlock. It carries no visual setters, so appearance is unchanged.
   Application.Current.Resources[typeof(TextBlock)]=Hooked(new Style(typeof(TextBlock)));
  }
  static Style Hooked(Style style){
   if(style.IsSealed||style.Setters.OfType<EventSetter>().Any(s=>s.Event==FrameworkElement.LoadedEvent))return style;
   style.Setters.Add(new EventSetter(FrameworkElement.LoadedEvent,new RoutedEventHandler(Loaded)));style.Setters.Add(new EventSetter(FrameworkElement.UnloadedEvent,new RoutedEventHandler(Unloaded)));return style;
  }
  // Named and scoped TextBlock styles replace the implicit one; give them the same hook.
  public static void Prepare(ResourceDictionary resources){
   if(!installed||resources==null)return;
   foreach(var value in resources.Values.OfType<Style>()){if(value.TargetType!=null&&typeof(TextBlock).IsAssignableFrom(value.TargetType))Hooked(value);if(value.Resources!=null)Prepare(value.Resources);}
   foreach(var merged in resources.MergedDictionaries)Prepare(merged);
  }
  public static void Prepare(DependencyObject root){
   var element=root as FrameworkElement;if(element!=null){Prepare(element.Resources);if(element.Style!=null&&typeof(TextBlock).IsAssignableFrom(element.Style.TargetType??typeof(object)))Hooked(element.Style);}
   foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())Prepare(child);
  }
  // Only plain local text is translated: bound values are data (names, paths, numbers).
  static bool LocalText(TextBlock text){
   if(text.Parent is LocalizedLabel||text.Inlines.Count>1||(text.Inlines.Count==1&&!(text.Inlines.FirstInline is Run)))return false;
   var source=DependencyPropertyHelper.GetValueSource(text,TextBlock.TextProperty);return source.BaseValueSource==BaseValueSource.Local&&!source.IsExpression;
  }
  // Remembers what was shown, so reloading an element never translates its translation again.
  sealed class Shown{public string Text;}
  static readonly ConditionalWeakTable<TextBlock,Shown> shownText=new ConditionalWeakTable<TextBlock,Shown>();
  static void Translate(TextBlock text){
   if(updating.Contains(text)||!LocalText(text))return;string value=text.Text;Shown last;if(shownText.TryGetValue(text,out last)&&last.Text==value)return;
   string shown=Localization.T(value);if(shown==value)return;
   updating.Add(text);try{text.Text=shown;shownText.GetValue(text,t=>new Shown()).Text=shown;}finally{updating.Remove(text);}
  }
  static void TextChanged(object sender,EventArgs e){var text=sender as TextBlock;if(text!=null)Translate(text);}
  static void TitleChanged(object sender,EventArgs e){var window=sender as Window;if(window==null)return;string shown=Localization.T(window.Title);if(shown!=window.Title)window.Title=shown;}
  static void Loaded(object sender,RoutedEventArgs e){
   if(e!=null&&!ReferenceEquals(e.OriginalSource,sender))return;
   var element=sender as FrameworkElement;if(element==null)return;
   var name=element.ReadLocalValue(AutomationProperties.NameProperty) as string;if(!string.IsNullOrEmpty(name)){string shown=Localization.T(name);if(shown!=name)AutomationProperties.SetName(element,shown);}
   var text=element as TextBlock;
   if(text!=null){Translate(text);object marker;if(!tracked.TryGetValue(text,out marker)){tracked.Add(text,true);textDescriptor.AddValueChanged(text,TextChanged);}return;}
   var window=element as Window;
   if(window!=null){TitleChanged(window,EventArgs.Empty);object marker;if(!windows.TryGetValue(window,out marker)){windows.Add(window,true);titleDescriptor.AddValueChanged(window,TitleChanged);window.Closed+=(s,a)=>titleDescriptor.RemoveValueChanged(window,TitleChanged);}}
  }
  static void Unloaded(object sender,RoutedEventArgs e){if(e!=null&&!ReferenceEquals(e.OriginalSource,sender))return;var text=sender as TextBlock;object marker;if(text!=null&&tracked.TryGetValue(text,out marker)){tracked.Remove(text);textDescriptor.RemoveValueChanged(text,TextChanged);}}
 }
 // Message boxes are native windows; translate their text before showing them.
 public static class LocalizedMessage {
  public static MessageBoxResult Show(string text){return MessageBox.Show(Localization.T(text));}
  public static MessageBoxResult Show(string text,string caption){return MessageBox.Show(Localization.T(text),Localization.T(caption));}
  public static MessageBoxResult Show(string text,string caption,MessageBoxButton buttons,MessageBoxImage image){return MessageBox.Show(Localization.T(text),Localization.T(caption),buttons,image);}
  public static MessageBoxResult Show(Window owner,string text){return MessageBox.Show(owner,Localization.T(text));}
  public static MessageBoxResult Show(Window owner,string text,string caption){return MessageBox.Show(owner,Localization.T(text),Localization.T(caption));}
  public static MessageBoxResult Show(Window owner,string text,string caption,MessageBoxButton buttons){return MessageBox.Show(owner,Localization.T(text),Localization.T(caption),buttons);}
  public static MessageBoxResult Show(Window owner,string text,string caption,MessageBoxButton buttons,MessageBoxImage image){return MessageBox.Show(owner,Localization.T(text),Localization.T(caption),buttons,image);}
  public static MessageBoxResult Show(Window owner,string text,string caption,MessageBoxButton buttons,MessageBoxImage image,MessageBoxResult result){return MessageBox.Show(owner,Localization.T(text),Localization.T(caption),buttons,image,result);}
 }
}
namespace AstroArchive {
 public partial class MainUi {
  internal static bool RestartRequested;
  static string SettingsPath{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AstroArchive","settings.json");}}
  // Read before the main window exists so the splash already speaks the chosen language.
  internal static string SavedLanguage(){try{if(!File.Exists(SettingsPath))return null;var saved=Util.Deserialize<Settings>(File.ReadAllText(SettingsPath));return saved==null?null:saved.Language;}catch(Exception e){if(e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is InvalidOperationException)return null;throw;}}
  internal static void Restart(MainUi ui){
   ui.Dispose();
   try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location){UseShellExecute=false});}catch(System.ComponentModel.Win32Exception){}
  }
  ComboBox AddLanguagePreference(FormWindow d){
   var choices=new List<LanguageChoice>{new LanguageChoice{Code=Localization.SystemDefault,Name=Localization.T("Windows language")+" ("+Localization.Languages.First(l=>l.Code==Localization.Resolve(null,null)).Name+")"}};choices.AddRange(Localization.Languages);
   string saved=string.IsNullOrEmpty(settings.Language)?Localization.SystemDefault:settings.Language;
   var caption=new TextBlock{Text="Language",Margin=new Thickness(0,8,0,5)};caption.SetResourceReference(TextBlock.FontSizeProperty,"UiFontBody");d.Add(caption);
   var box=new ComboBox{ItemsSource=choices,SelectedItem=choices.FirstOrDefault(c=>c.Code==saved)??choices[0]};AutomationProperties.SetName(box,"Language");d.Add(box);
   var note=new TextBlock{Text="Applies after AstroArchive restarts. The offline guide stays in English.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,4)};note.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");Theme.Bind(note,TextBlock.ForegroundProperty,"Muted");d.Add(note);
   return box;
  }
  static string SelectedLanguage(ComboBox box){var choice=box==null?null:box.SelectedItem as LanguageChoice;return choice==null||choice.Code==Localization.SystemDefault?null:choice.Code;}
  void OfferLanguageRestart(){
   if(closing)return;bool busy=cancel!=null||remoteSessions.Count>0||RepositoryOperationBlocked;
   if(busy){LocalizedMessage.Show(Window,"The new language applies the next time you open AstroArchive.","Language",MessageBoxButton.OK,MessageBoxImage.Information);return;}
   if(LocalizedMessage.Show(Window,"Restart AstroArchive now to use the new language?","Language",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
   RestartRequested=true;Window.Close();
  }
 }
}
