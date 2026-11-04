using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using LumasharpChecker.view;
using LumasharpChecker.viewmodel;
class Program {
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
 [STAThread] static void Main(string[] args){
  AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{string name=new AssemblyName(e.Name).Name;string f=name=="LumasharpChecker"?args[0]:Path.Combine(args[1],name+".dll");return File.Exists(f)?Assembly.LoadFrom(f):null;};try { Run(args); } catch(Exception ex) { Console.WriteLine(ex.GetType().FullName+": "+ex.Message); Environment.Exit(1); }
 }
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 static void Run(string[] args){
  System.Threading.Thread.CurrentThread.CurrentCulture=CultureInfo.InvariantCulture;
  var app=new Application();
  var ok=new TestImageResultRow{FilePath="ok.png",FileName="ok.png",Brightness=120,BrightnessMin=108,BrightnessMax=132,BrightnessSafe=true,IsMatched=true,IncludeSharpness=false};
  var ng=new TestImageResultRow{FilePath="ng.png",FileName="ng.png",Brightness=90,BrightnessMin=108,BrightnessMax=132,BrightnessSafe=false,IsMatched=true,IncludeSharpness=false};
  var retest=new TestImageResultRow{FilePath="retest.png",BrightnessMeasured=false,InspectionPipeline="新版流程",IncludeSharpness=false};
  var skipped=new TestImageResultRow{FilePath="skip.png",BrightnessMin=10,BrightnessMax=200,BrightnessMeasured=false,LightInspectionSkipped=true,IsMatched=true,IncludeSharpness=false};
  var rows=new[]{(1,ok),(2,ng),(3,retest),(4,skipped)};
  var window=new TestImageResultsWindow(rows,BatchPreviewModes.NgOnly,"ALL",true);
  var grid=(DataGrid)window.FindName("ResultsGrid");var filter=(ComboBox)window.FindName("ResultFilter");var thumbs=(CheckBox)window.FindName("ShowThumbnails");
  Check(grid.Items.Count==4&&thumbs.IsChecked==true,"all results and all thumbnails shown by default");
  var shown=(TestImageResultDisplayRow)grid.Items[0];Check(shown.MinimumDisplay=="108.00"&&shown.MaximumDisplay=="132.00"&&shown.BrightnessDisplay=="120.00","stored limits and measured value displayed");
  Check(!string.IsNullOrEmpty(shown.ThumbnailPath),"OK thumbnail no longer blank");
  filter.SelectedValue="OK";Check(grid.Items.Count==2,"OK filter includes passing light-skipped row");
  filter.SelectedValue="NG";Check(grid.Items.Count==2,"NG filter keeps failed and Retest rows as previous semantics");
  filter.SelectedValue="Retest";Check(grid.Items.Count==1,"Retest-only filter");
  filter.SelectedValue="ALL";thumbs.IsChecked=false;Check(grid.Columns[0].Visibility==Visibility.Collapsed&&grid.RowHeight==32&&((TestImageResultDisplayRow)grid.Items[0]).ThumbnailPath=="","thumbnail switch avoids decoding and uses compact rows");
  thumbs.IsChecked=true;Check(grid.Columns[0].Visibility==Visibility.Visible&&grid.RowHeight==78&&((TestImageResultDisplayRow)grid.Items[0]).ThumbnailPath=="ok.png","thumbnails can be restored");
  Check(((TestImageResultDisplayRow)grid.Items[3]).MinimumDisplay=="—"&&((TestImageResultDisplayRow)grid.Items[3]).MaximumDisplay=="—","light skipped has no applicable limits");
  filter.SelectedValue="Retest";var count=(TextBlock)window.FindName("ResultCount");Check(count.Text.Contains("1／4"),"visible count updates with filter");
  window.Close();
  if(args.Length>2){
   var converter=new DeferredThumbnailConverter();string[] paths=File.ReadAllLines(args[2]);var t=Stopwatch.StartNew();
   foreach(var path in paths){var bitmap=(BitmapImage)converter.Convert(path,typeof(BitmapImage),null,CultureInfo.InvariantCulture);if(bitmap==null||bitmap.PixelWidth==0)throw new Exception("Thumbnail not decoded");}
   t.Stop();Console.WriteLine("THUMBNAILS_ALL_"+paths.Length+"="+t.Elapsed.TotalMilliseconds.ToString("F3")+" ms (decode only)");
   t.Restart();foreach(var path in paths)converter.Convert("",typeof(BitmapImage),null,CultureInfo.InvariantCulture);t.Stop();Console.WriteLine("THUMBNAILS_NG_ALL_OK="+t.Elapsed.TotalMilliseconds.ToString("F3")+" ms (no decoding)");
  }
  Console.WriteLine("ALL RESULTS WINDOW CHECKS PASSED");app.Shutdown();
 }
}
