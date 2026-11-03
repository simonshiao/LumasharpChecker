using LumasharpChecker.model;
using System.IO.Compression;
class RecipeManager { public static string GetStationDirectoryName(int n) => $"station{n:00}"; }
class Program
{
 static void Assert(bool ok,string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
 static void Reject(Action run,string label) { try {run();} catch { Console.WriteLine("PASS " + label); return; } throw new Exception(label); }
 static void Main()
 {
  string root=Path.Combine(AppContext.BaseDirectory,"fixtures-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
  string image=Path.Combine(root,"original.bmp"); File.WriteAllBytes(image,new byte[]{1,2,3,4,5});
  string history=LightSampleHistory.Folder(root,1);
  var settings=new LightInspectionSettings{HasRoi=true,Left=1,Top=2,Right=3,Bottom=4,BaseValue=100};
  int applied=0; Action<LightSampleHistory.Sample,string> apply=(s,p)=>{Assert(File.ReadAllBytes(p).SequenceEqual(s.Image),"apply exact original bytes"); applied++;};
  var first=LightSampleHistory.Capture("r1",1,image,settings);
  Assert(LightSampleHistory.Save(history,first,apply),"create sample point");
  Assert(!LightSampleHistory.Save(history,first,apply),"identical image/settings no duplicate");
  string firstArchive=Directory.GetFiles(history,"sample-*.zip").Single();
  var read=LightSampleHistory.Read(firstArchive,"r1",1);
  Assert(read.Image.SequenceEqual(first.Image)&&read.Settings.Left==1&&read.Settings.BaseValue==100,"roundtrip image and ROI/baseline/limits");
  Reject(()=>LightSampleHistory.Read(firstArchive,"r1",2),"cross station rejected");
  Reject(()=>LightSampleHistory.Read(firstArchive,"r2",1),"cross recipe rejected");
  string active=Path.Combine(root,"active.txt"); File.WriteAllText(active,"previous");
  string newFile=Path.Combine(root,"new.txt");
  Reject(()=>LightSampleHistory.FileTransaction(new[]{active,newFile},()=>{File.WriteAllText(active,"new");File.WriteAllText(newFile,"new");throw new IOException("injected");}),"failed write reports failure");
  Assert(File.ReadAllText(active)=="previous"&&!File.Exists(newFile),"all files rolled back including new files");
  settings.BaseValue=101;
  Reject(()=>LightSampleHistory.Save(history,LightSampleHistory.Capture("r1",1,image,settings),(s,p)=>throw new IOException("injected")),"failed standard action does not publish point");
  Assert(Directory.GetFiles(history,"sample-*.zip").Length==1&&Directory.GetFileSystemEntries(history).Length==1,"failed transaction leaves no staging");
  for(int i=0;i<5;i++){settings.BaseValue=102+i;LightSampleHistory.Save(history,LightSampleHistory.Capture("r1",1,image,settings),apply);LightSampleHistory.Prune(history);}
  Assert(Directory.GetFiles(history,"sample-*.zip").Length==3,"bounded retention three");
  Assert(Directory.GetFiles(history,"sample-*.zip").Select(p=>LightSampleHistory.Read(p,"r1",1).Settings.BaseValue).Order().SequenceEqual(new double[]{104,105,106}),"retains newest states");
  File.WriteAllBytes(image,new byte[]{9,8,7});
  Assert(LightSampleHistory.Save(history,LightSampleHistory.Capture("r1",1,image,settings),apply),"image-only change creates point"); LightSampleHistory.Prune(history);
  string corrupted=Path.Combine(root,"corrupt.zip");File.Copy(Directory.GetFiles(history,"sample-*.zip")[0],corrupted);
  using(var zip=ZipFile.Open(corrupted,ZipArchiveMode.Update)){var e=zip.Entries.Single(e=>e.FullName.StartsWith("image"));e.Delete();using var w=zip.CreateEntry("image.bmp").Open();w.WriteByte(0);}
  Reject(()=>LightSampleHistory.Read(corrupted,"r1",1),"tampered image rejected before apply");
  string source=Path.Combine(root,"source"),dest=Path.Combine(root,"destination");Directory.CreateDirectory(source);Directory.CreateDirectory(dest);
  File.WriteAllText(Path.Combine(source,"p.ini"),"old");File.WriteAllText(Path.Combine(dest,"p.ini"),"old");
  Assert(!RecipeSnapshotStorage.Replace(source,dest,()=>{}),"unchanged recipe skips directory replacement");
  File.WriteAllText(Path.Combine(source,"p.ini"),"new");
  Reject(()=>RecipeSnapshotStorage.Replace(source,dest,()=>throw new IOException("injected manifest failure")),"manifest failure reports failure");
  Assert(File.ReadAllText(Path.Combine(dest,"p.ini"))=="old","native snapshot rollback preserves previous data");
  Assert(RecipeSnapshotStorage.Replace(source,dest,()=>{}),"changed recipe replaces directory");
  Assert(File.ReadAllText(Path.Combine(dest,"p.ini"))=="new"&&!Directory.EnumerateDirectories(root).Any(d=>d.Contains("transaction-")),"verified recipe leaves no backup/stage");
  Reject(()=>RecipeSnapshotStorage.Replace(source,Path.Combine(source,"child"),()=>{}),"nested snapshot destination rejected");
  var invalidRoi = new LightInspectionSettings { HasRoi=true,Left=1683,Right=1642,Top=216,Bottom=216,BaseValue=82.52 };
  var sideBounds = new System.Drawing.Rectangle(23,18,646,444);
  Reject(()=>invalidRoi.GetRoi(sideBounds),"oversized legacy margins still rejected by inspection");
  Assert(invalidRoi.EnsureEditableRoi(sideBounds),"editor recovers screenshot's oversized ROI");
  var recovered=invalidRoi.GetRoi(sideBounds);
  Assert(recovered.Width==323&&recovered.Height==222&&sideBounds.Contains(recovered),"recovered ROI centered and inside located object");
  int l=invalidRoi.Left,r=invalidRoi.Right,t=invalidRoi.Top,b=invalidRoi.Bottom;
  Assert(!invalidRoi.EnsureEditableRoi(sideBounds)&&invalidRoi.Left==l&&invalidRoi.Right==r&&invalidRoi.Top==t&&invalidRoi.Bottom==b,"valid saved ROI is preserved");
  foreach(var size in new[]{new System.Drawing.Size(2,2),new System.Drawing.Size(3,5),new System.Drawing.Size(12,600),new System.Drawing.Size(600,12)})
  {
   var bounds=new System.Drawing.Rectangle(37,91,size.Width,size.Height);
   var roiSettings=new LightInspectionSettings();
   Assert(roiSettings.EnsureEditableRoi(bounds),"initialize unset ROI "+size);
   var box=roiSettings.GetRoi(bounds);
   Assert(box.Width>=2&&box.Height>=2&&bounds.Contains(box),"thin/odd/minimum ROI stays within bounds "+size);
  }
  invalidRoi.Left=int.MaxValue;invalidRoi.Right=int.MaxValue;
  Assert(invalidRoi.EnsureEditableRoi(sideBounds),"oversized margins cannot overflow during recovery");
  var unchanged=new LightInspectionSettings {HasRoi=true,Left=0,Right=0,Top=0,Bottom=0};
  Reject(()=>unchanged.EnsureEditableRoi(new System.Drawing.Rectangle(0,0,1,100)),"invalid native bounds reported without fabricating an ROI");
  Console.WriteLine("ALL STORAGE CHECKS PASSED");
 }
}
