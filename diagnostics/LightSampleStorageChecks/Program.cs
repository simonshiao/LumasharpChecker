using LumasharpChecker.model;
using System.IO.Compression;
class RecipeManager { public static string GetStationDirectoryName(int n) => $"station{n:00}"; }
class Program
{
 static void Assert(bool ok,string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
 static void Reject(Action run,string label) { try {run();} catch { Console.WriteLine("PASS " + label); return; } throw new Exception(label); }
 static void InspectProductionSnapshot(string originalRecipeRoot,int station)
 {
  // 只從正式配方讀取，將全部實驗都限制在唯一 TEMP 快照，不觸碰原始 Recipe。
  string id=Path.GetFileName(Path.GetFullPath(originalRecipeRoot).TrimEnd(Path.DirectorySeparatorChar));
  string sourceStation=Path.Combine(originalRecipeRoot,RecipeManager.GetStationDirectoryName(station));
  string fixture=Path.Combine(Path.GetTempPath(),"Luma-Recovery-Snapshot-"+Guid.NewGuid().ToString("N"));
  string cloneRoot=Path.Combine(fixture,"App","recipes",id);
  string cloneStation=Path.Combine(cloneRoot,RecipeManager.GetStationDirectoryName(station));
  Directory.CreateDirectory(cloneStation);
  foreach(string file in Directory.GetFiles(sourceStation,"*"))
  {
   string name=Path.GetFileName(file);
   if(name.StartsWith("_light-standard",StringComparison.OrdinalIgnoreCase) ||
      name.Equals("light-inspection.json",StringComparison.OrdinalIgnoreCase))
    File.Copy(file,Path.Combine(cloneStation,name));
  }
  foreach(bool full in new[]{false,true})
  {
   string srcHistory=LightSampleHistory.Folder(originalRecipeRoot,station,full);
   if(!Directory.Exists(srcHistory))continue;
   string destHistory=LightSampleHistory.Folder(cloneRoot,station,full);
   Directory.CreateDirectory(destHistory);
   foreach(string file in Directory.GetFiles(srcHistory,"sample-*.zip"))
    File.Copy(file,Path.Combine(destHistory,Path.GetFileName(file)));
  }
  var sourceResidues=StandardSampleRecovery.FindResidues(originalRecipeRoot,station);
  Console.WriteLine("PRODUCTION_RESIDUE_COUNT="+sourceResidues.Count);
  foreach(var file in sourceResidues) Console.WriteLine("READ_ONLY_PRODUCTION_RESIDUE="+Path.GetFileName(file));
  string[] activeImages=Directory.GetFiles(cloneStation)
   .Where(path=>Path.GetFileName(path).StartsWith("_light-standard-fullimage.",StringComparison.OrdinalIgnoreCase)
         && new[]{".png",".bmp",".jpg",".jpeg",".tif",".tiff"}.Contains(Path.GetExtension(path).ToLowerInvariant()))
   .ToArray();
  var activeSettings=System.Text.Json.JsonSerializer.Deserialize<LightInspectionSettings>(
   File.ReadAllText(Path.Combine(cloneStation,"light-inspection.json")))!;
  activeSettings.Validate();
  string settingsHash=Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
   System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(activeSettings)));
  string imageHash=activeImages.Length==1?Convert.ToBase64String(
   System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(activeImages[0]))):"NONE";
  string archiveFolder=LightSampleHistory.Folder(cloneRoot,station,true);
  string[] archives=Directory.Exists(archiveFolder)?Directory.GetFiles(archiveFolder,"sample-*.zip"):Array.Empty<string>();
  Console.WriteLine("VERIFIED_ACTIVE_IMAGES="+activeImages.Length);
  Console.WriteLine("ARCHIVE_COUNT="+archives.Length);
  foreach(string archive in archives)
  {
   try
   {
    var sample=LightSampleHistory.Read(archive,id,station,true);
    Console.WriteLine("ARCHIVE="+Path.GetFileName(archive)+
     " IMAGE_MATCH="+(sample.ImageHash==imageHash)+" SETTINGS_MATCH="+(sample.SettingsHash==settingsHash));
   }
   catch(Exception ex)
   {
    using var zip=ZipFile.OpenRead(archive);
    using var input=zip.GetEntry("sample.json")!.Open();
    var sample=System.Text.Json.JsonSerializer.Deserialize<LightSampleHistory.Sample>(input)!;
    string savedSettingsHash=Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
     System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(sample.Settings)));
    var payload=zip.Entries.FirstOrDefault(e=>e.FullName.StartsWith("image."));
    string payloadHash="NO_IMAGE";
    if(payload!=null)
    {
     using var img=payload.Open(); using var ms=new MemoryStream();img.CopyTo(ms);
     payloadHash=Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(ms.ToArray()));
    }
    Console.WriteLine("ARCHIVE_INVALID="+Path.GetFileName(archive)+
     " REASON="+ex.GetBaseException().Message+
     " META_IMAGE_VALID="+(sample.ImageHash==payloadHash)+
     " META_SETTINGS_VALID="+(sample.SettingsHash==savedSettingsHash)+
     " ACTIVE_IMAGE_MATCH="+(sample.ImageHash==imageHash)+
     " ACTIVE_SETTINGS_MATCH="+(sample.SettingsHash==settingsHash)+
     " MODE="+sample.FullImageMode);
   }
  }
  var outcome=StandardSampleRecovery.RepairVerifiedOrphans(cloneRoot,id,station);
  Console.WriteLine("CLONE_RECOVERED="+outcome.RecoveredFiles);
  foreach(string issue in outcome.Issues)Console.WriteLine("CLONE_ISSUE="+issue);
  Console.WriteLine("CLONE_RESIDUES_LEFT="+StandardSampleRecovery.FindResidues(cloneRoot,station).Count);
  Console.WriteLine("CLONE_PATH="+fixture);
 }
 static void Main(string[] args)
 {
  if(args.Length==3 && args[0]=="--inspect-snapshot")
  {
   InspectProductionSnapshot(args[1],int.Parse(args[2]));
   return;
  }
  string root=Path.Combine(AppContext.BaseDirectory,"fixtures-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
  string image=Path.Combine(root,"original.bmp"); File.WriteAllBytes(image,new byte[]{1,2,3,4,5});
  string history=LightSampleHistory.Folder(root,1,false);
  var settings=new LightInspectionSettings{HasRoi=true,Left=1,Top=2,Right=3,Bottom=4,BaseValue=100};
  int applied=0; Action<LightSampleHistory.Sample,string> apply=(s,p)=>{Assert(File.ReadAllBytes(p).SequenceEqual(s.Image),"apply exact original bytes"); applied++;};
  var first=LightSampleHistory.Capture("r1",1,image,settings,false);
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
  Reject(()=>LightSampleHistory.Save(history,LightSampleHistory.Capture("r1",1,image,settings,false),(s,p)=>throw new IOException("injected")),"failed standard action does not publish point");
  Assert(Directory.GetFiles(history,"sample-*.zip").Length==1&&Directory.GetFileSystemEntries(history).Length==1,"failed transaction leaves no staging");
  for(int i=0;i<5;i++){settings.BaseValue=102+i;LightSampleHistory.Save(history,LightSampleHistory.Capture("r1",1,image,settings,false),apply);LightSampleHistory.Prune(history);}
  Assert(Directory.GetFiles(history,"sample-*.zip").Length==3,"bounded retention three");
  Assert(Directory.GetFiles(history,"sample-*.zip").Select(p=>LightSampleHistory.Read(p,"r1",1).Settings.BaseValue).Order().SequenceEqual(new double[]{104,105,106}),"retains newest states");
  File.WriteAllBytes(image,new byte[]{9,8,7});
  Assert(LightSampleHistory.Save(history,LightSampleHistory.Capture("r1",1,image,settings,false),apply),"image-only change creates point"); LightSampleHistory.Prune(history);
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
  // 模擬 Apex One 或檔案占用拒絕寫入（不關閉防毒、不動正式 Recipe）。
  string activeLocked=Path.Combine(root,"locked.txt");File.WriteAllText(activeLocked,"original");
  Reject(()=>LightSampleHistory.FileTransaction(new[]{activeLocked},()=>{
    File.WriteAllText(activeLocked,"changed");
    throw new UnauthorizedAccessException("simulated security refusal");
  }),"access-denied transaction reports failure");
  Assert(File.ReadAllText(activeLocked)=="original","access-denied rollback keeps old data");

  string appRoot=Path.Combine(root,"App");
  string recipeRoot=Path.Combine(appRoot,"recipes","isolated-r1");
  string stationDir=Path.Combine(recipeRoot,"station01");
  Directory.CreateDirectory(stationDir);
  string official=Path.Combine(stationDir,"_light-standard.bmp");
  var validSettings=new LightInspectionSettings {HasRoi=true,Left=1,Top=2,Right=3,Bottom=4,BaseValue=100};
  var precisionSettings=new LightInspectionSettings {HasRoi=true,Left=1,Top=2,Right=3,Bottom=4,BaseValue=100.004};
  File.WriteAllBytes(official,new byte[]{7,8,9,10});
  File.WriteAllText(Path.Combine(stationDir,"light-inspection.json"),
    System.Text.Json.JsonSerializer.Serialize(validSettings));
  var validSample=LightSampleHistory.Capture("isolated-r1",1,official,precisionSettings,false);
  string validHistory=LightSampleHistory.Folder(recipeRoot,1,false);
  LightSampleHistory.Save(validHistory,validSample,(_,_)=>{});
  var beforeRecovery=StandardSampleRecovery.RepairVerifiedOrphans(recipeRoot,"isolated-r1",1);
  Assert(beforeRecovery.RecoveredFiles==0 && beforeRecovery.Issues.Count==0,"clean recipe has no residual transaction");
  string goodTemp=official+"."+Guid.NewGuid().ToString("N")+".tmp";
  File.Copy(official,goodTemp);
  string goodApply=Path.Combine(validHistory,".apply-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(goodApply);
  File.Copy(official,Path.Combine(goodApply,"_light-standard.bmp"));
  var recoveredResult=StandardSampleRecovery.RepairVerifiedOrphans(recipeRoot,"isolated-r1",1);
  Assert(recoveredResult.RecoveredFiles==2 && recoveredResult.Issues.Count==0,
    "verified staging recovered with only baseline rounded to 2 decimals");
  Assert(!File.Exists(goodTemp)&&!Directory.Exists(goodApply)&&File.Exists(official),
    "recovery does not touch official standard");
  string backupRoot=Path.Combine(appRoot,"standard-sample-recovery","isolated-r1");
  Assert(Directory.GetFiles(backupRoot,"*",SearchOption.AllDirectories).Length>=2,
    "recovery keeps verified evidence backup");
  string changedSettingsTemp=official+"."+Guid.NewGuid().ToString("N")+".tmp";
  File.Copy(official,changedSettingsTemp);
  validSettings.PercentUpper=11;
  File.WriteAllText(Path.Combine(stationDir,"light-inspection.json"),
    System.Text.Json.JsonSerializer.Serialize(validSettings));
  var changedSettingsResult=StandardSampleRecovery.RepairVerifiedOrphans(recipeRoot,"isolated-r1",1);
  Assert(changedSettingsResult.Issues.Count==1 && File.Exists(changedSettingsTemp),
    "modified acceptance range cannot be mistaken for harmless baseline rounding");
  File.Delete(changedSettingsTemp);
  validSettings.PercentUpper=10;
  File.WriteAllText(Path.Combine(stationDir,"light-inspection.json"),
    System.Text.Json.JsonSerializer.Serialize(validSettings));
  string badTemp=official+"."+Guid.NewGuid().ToString("N")+".tmp";
  File.WriteAllBytes(badTemp,new byte[]{9,9,9});
  var unsafeResult=StandardSampleRecovery.RepairVerifiedOrphans(recipeRoot,"isolated-r1",1);
  Assert(unsafeResult.Issues.Count==1&&File.Exists(badTemp),
    "unknown content is never silently deleted or overwritten");
  File.Delete(badTemp);
  Console.WriteLine("ALL STORAGE CHECKS PASSED");
 }
}
