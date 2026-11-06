using LumasharpChecker.model;
class Program {
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
 static void Image(string root,string path){string f=Path.Combine(root,path,"one.PNG");Directory.CreateDirectory(Path.GetDirectoryName(f)!);File.WriteAllBytes(f,new byte[]{0});}
 static void Reject(Action f,string text){try{f();}catch(ArgumentException){Console.WriteLine("PASS "+text);return;}throw new Exception(text);}
 static void Main(string[] args){
  string root=Path.Combine(AppContext.BaseDirectory,"fixtures",Guid.NewGuid().ToString("N"),"TrainingImage");
  Image(root,@"20160H-CCD01\Origional\1\OK");Image(root,@"20160H-CCD01\Origional\5\NG");Image(root,@"20160H-CCD02\Original\02\OK");Image(root,@"20160H-CCD06\Original\6\NG");Image(root,@"20160H-CCD01\X\OK");
  var found=TrainingImageDiscovery.Discover(root,8);
  Check(found.Product=="20160H"&&found.Candidates.Select(c=>c.Station).SequenceEqual(new[]{1,2,5,6}),"root infers prefix, padded CCD and inner stations including shared camera station 5");
  Check(found.Candidates.Single(c=>c.Station==5).OkPath==null&&found.Candidates.Single(c=>c.Station==5).NgCount==1,"missing category remains null and count uses images only");
  var nested=TrainingImageDiscovery.Discover(Path.Combine(root,@"20160H-CCD01\Origional\5\NG"),8);Check(nested.Candidates.Count==4,"nested category finds same product family");
  Image(root,@"R002-CCD1\Origional\1\OK");Image(root,@"R002-CCD10\Origional\10\NG");Image(root,@"R002-CCD11\Origional\3\OK");
  Reject(()=>TrainingImageDiscovery.Discover(root,8),"ambiguous product prefixes require selection");
  var legacy=TrainingImageDiscovery.Discover(Path.Combine(root,"R002-CCD1"),10);Check(legacy.Candidates.Select(c=>c.Station).SequenceEqual(new[]{1,10}),"legacy CCD1/CCD10 works, CCD11 and other product excluded");
  Check(TrainingImageDiscovery.Discover(Path.Combine(root,"R002-CCD1"),8).Candidates.Count==1,"station limit respected");
  Image(root,@"R002-CCD01-v2\Origional\1\OK");Check(TrainingImageDiscovery.Discover(Path.Combine(root,"R002-CCD1"),8).Candidates.Count(c=>c.Station==1)==2,"multiple versions remain separate choices");
  string root2=Path.Combine(AppContext.BaseDirectory,"fixtures",Guid.NewGuid().ToString("N"),"TrainingImage");Image(root2,@"A.B+-CCD06\Original\6\OK");Check(TrainingImageDiscovery.Discover(root2,8).Product=="A.B+","unique prefix inferred without camera 1 and regex symbols escaped");
  if(args.Length>0) {
   var real=TrainingImageDiscovery.Discover(args[0],8);Check(real.Product=="20160H"&&real.Candidates.Select(c=>c.Station).SequenceEqual(new[]{1,2,3,4,5,6}),"20160H sample folder read-only discovers six stations");
   foreach(var c in real.Candidates)Console.WriteLine($"STATION {c.Station}: {c.FolderName}, OK={c.OkCount}, NG={c.NgCount}");
  }
  Console.WriteLine("ALL TRAINING DISCOVERY CHECKS PASSED");
 }
}
