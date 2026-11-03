using System.Drawing;
using LumasharpChecker.model;
namespace LumasharpChecker.model { internal static class NativeLocationService { internal sealed class Frame { public Rectangle Bounds {get;set;} public string ImagePath {get;set;}=""; } } }
class Program {
 static void Assert(bool value,string text) { if(!value)throw new Exception(text); Console.WriteLine("PASS "+text); }
 static void Main() {
  string image=Path.Combine(AppContext.BaseDirectory,"roi-input.bmp");
  using(var bmp=new Bitmap(16,16)) { for(int y=0;y<16;y++)for(int x=0;x<16;x++)bmp.SetPixel(x,y,x>=4&&x<12&&y>=4&&y<12?Color.FromArgb(120,50,20):Color.White);bmp.Save(image); }
  var frame=new NativeLocationService.Frame {Bounds=new Rectangle(0,0,16,16),ImagePath=image};
  var settings=new LightInspectionSettings{HasRoi=true,Left=4,Top=4,Right=4,Bottom=4,BaseValue=0};
  double baseline=LightBrightnessMeasurement.Measure(frame,settings);
  Assert(Math.Abs(baseline-120)<0.001,"uses saved ROI and HSV V, excludes white background");
  settings.BaseValue=baseline;var limits=settings.EffectiveLimits();
  Assert(Math.Abs(limits.Min-108)<0.001&&Math.Abs(limits.Max-132)<0.001,"zero prior baseline replaced before calculating percentage limits");
  var initial=new LightInspectionSettings();Assert(initial.EnsureEditableRoi(frame.Bounds),"initializes missing ROI");
  Assert(Math.Abs(LightBrightnessMeasurement.Measure(frame,initial)-120)<0.001,"initial ROI measures actual target");
  bool rejected=false;try{LightBrightnessMeasurement.Measure(new NativeLocationService.Frame {Bounds=new Rectangle(20,20,16,16),ImagePath=image},settings);}catch(InvalidOperationException){rejected=true;}
  Assert(rejected,"out-of-image ROI rejected before save");
  var rng=new Random(42);
  foreach(var size in new[]{new Size(17,19),new Size(80,51),new Size(2,2)}) {
   string bmpPath=Path.Combine(AppContext.BaseDirectory,"random-"+size.Width+".bmp"),pngPath=Path.ChangeExtension(bmpPath,".png");
   using(var bmp=new Bitmap(size.Width,size.Height,System.Drawing.Imaging.PixelFormat.Format24bppRgb)) {
    for(int y=0;y<bmp.Height;y++)for(int x=0;x<bmp.Width;x++)bmp.SetPixel(x,y,Color.FromArgb(rng.Next(256),rng.Next(256),rng.Next(256)));
    bmp.Save(bmpPath,System.Drawing.Imaging.ImageFormat.Bmp);bmp.Save(pngPath,System.Drawing.Imaging.ImageFormat.Png);
   }
   var samples=new[]{new LightInspectionSettings{HasRoi=true},new LightInspectionSettings{HasRoi=true,Left=size.Width>2?1:0,Top=size.Height>2?1:0,Right=size.Width>2?2:0,Bottom=size.Height>2?2:0}};
   foreach(var sample in samples) {
    var bounds=new Rectangle(0,0,size.Width,size.Height);
    double fast=LightBrightnessMeasurement.Measure(new NativeLocationService.Frame{Bounds=bounds,ImagePath=bmpPath},sample);
    double reference=LightBrightnessMeasurement.Measure(new NativeLocationService.Frame{Bounds=bounds,ImagePath=pngPath},sample);
    Assert(Math.Abs(fast-reference)<1e-10,"24-bit BMP equals HSV reference, padded/edge ROI "+size);
   }
   byte[] bytes=File.ReadAllBytes(bmpPath);int offset=BitConverter.ToInt32(bytes,10);int stride=(size.Width*3+3)&~3;
   var reversed=(byte[])bytes.Clone();Array.Copy(BitConverter.GetBytes(-size.Height),0,reversed,22,4);
   for(int y=0;y<size.Height;y++)Array.Copy(bytes,offset+y*stride,reversed,offset+(size.Height-1-y)*stride,stride);
   string topDown=Path.Combine(AppContext.BaseDirectory,"topdown-"+size.Width+".bmp");File.WriteAllBytes(topDown,reversed);
   Assert(Math.Abs(LightBrightnessMeasurement.Measure(new NativeLocationService.Frame{Bounds=new Rectangle(0,0,size.Width,size.Height),ImagePath=topDown},samples[1])-LightBrightnessMeasurement.Measure(new NativeLocationService.Frame{Bounds=new Rectangle(0,0,size.Width,size.Height),ImagePath=pngPath},samples[1]))<1e-10,"top-down BMP equals reference "+size);
  }
  Console.WriteLine("ALL BRIGHTNESS CHECKS PASSED");
 }
}
