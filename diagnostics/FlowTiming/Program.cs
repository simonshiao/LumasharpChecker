using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
class Program {
 static object Get(object o,string n)=>o.GetType().GetProperty(n).GetValue(o);
 static void Set(object o,string n,object v)=>o.GetType().GetProperty(n).SetValue(o,v);
 static double Median(IEnumerable<double> x){var a=x.OrderBy(v=>v).ToArray();return a[a.Length/2];}
 [STAThread] static int Main(string[] args){
  Console.InputEncoding=new System.Text.UTF8Encoding(false); Trace.Listeners.Add(new ConsoleTraceListener()); string app=args[1];AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{string p=Path.Combine(app,new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
  var a=Assembly.LoadFrom(args[0]);var bridge=a.GetType("LumasharpChecker.model.NativeRecipeBridge");
  try {
   if(args.Length==3&&args[2]=="hash-check") {
    string fixture=Path.Combine(Path.GetDirectoryName(args[0]),"hash-fixture-"+Guid.NewGuid().ToString("N"));string source=Path.Combine(fixture,"a"),dest=Path.Combine(fixture,"b");Directory.CreateDirectory(source);Directory.CreateDirectory(dest);
    var files=Enumerable.Range(0,8).Select(i=>"file"+i+".ini").ToList();var identity=new System.Text.StringBuilder();
    using(var sha=System.Security.Cryptography.SHA256.Create()) {
     foreach(string f in files){byte[] b=System.Text.Encoding.UTF8.GetBytes(f+new string('x',10000));File.WriteAllBytes(Path.Combine(source,f),b);File.WriteAllBytes(Path.Combine(dest,f),b);identity.Append(f).Append(':').Append(Convert.ToBase64String(sha.ComputeHash(b))).Append('\n');}
     string expected=Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(identity.ToString())));
     var compare=bridge.GetMethod("CompareExactFiles",BindingFlags.NonPublic|BindingFlags.Static);string actual=(string)compare.Invoke(null,new object[]{source,dest,files});if(actual!=expected)throw new Exception("Revision changed");Console.WriteLine("PASS parallel SHA256 revision equals legacy digest");
     string changed=Path.Combine(dest,files[3]);DateTime stamp=File.GetLastWriteTimeUtc(changed);byte[] bytes=File.ReadAllBytes(changed);bytes[100]^=1;File.WriteAllBytes(changed,bytes);File.SetLastWriteTimeUtc(changed,stamp);
     bool rejected=false;try{compare.Invoke(null,new object[]{source,dest,files});}catch(TargetInvocationException e){rejected=e.InnerException is InvalidDataException;}
     if(!rejected)throw new Exception("Tamper not rejected");Console.WriteLine("PASS same-size/same-time content tampering rejected with original exception type");
    }
    return 0;
   }
   var json=new JavaScriptSerializer();var recipe=json.Deserialize(File.ReadAllText(args[2]),a.GetType("LumasharpChecker.model.RecipeManager+RecipeManifest"));
   var settings=json.Deserialize(File.ReadAllText(args[4]),a.GetType("LumasharpChecker.model.LightInspectionSettings"));
   Console.WriteLine("INPUT "+args[3]+" exists="+File.Exists(args[3])); int station=int.Parse(args[5]);var data=new List<Dictionary<string,object>>();
   for(int i=0;i<60;i++){
    var whole=Stopwatch.StartNew();var task=(Task)bridge.GetMethod("RunPreLightAsync").Invoke(null,new object[]{recipe,station,args[3],app,"face","100"});task.GetAwaiter().GetResult();
    var r=Get(task,"Result");if((int)Get(r,"Exit")!=0)throw new Exception("Native inspection failed: "+Get(r,"Error"));
    var frame=Activator.CreateInstance(a.GetType("LumasharpChecker.model.NativeLocationService+Frame",true),true);
    Set(frame,"ImagePath",Get(r,"OutputImage"));Set(frame,"Bounds",new Rectangle((int)Get(r,"X"),(int)Get(r,"Y"),(int)Get(r,"Width"),(int)Get(r,"Height")));
    var t=Stopwatch.StartNew();double value=(double)a.GetType("LumasharpChecker.model.LightBrightnessMeasurement").GetMethod("Measure",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new[]{frame,settings});t.Stop();whole.Stop();
    data.Add(new Dictionary<string,object>{{"Total",whole.Elapsed.TotalMilliseconds},{"Prep",Get(r,"PreparationMs")},{"Native",Get(r,"NativeRoundTripMs")},{"Worker",Get(r,"WorkerMs")},{"Locate",Get(r,"LocateMs")},{"Light",t.Elapsed.TotalMilliseconds},{"Brightness",value},{"ImageRead",Get(r,"ImageReadMs")},{"FrameExport",Get(r,"FrameExportMs")}});
   }
   var warm=data.Skip(10).ToArray();var summary=new Dictionary<string,object>();foreach(string key in new[]{"Total","Prep","Native","Worker","Locate","Light","Brightness","ImageRead","FrameExport"})summary[key]=Median(warm.Select(d=>Convert.ToDouble(d[key])));
   File.WriteAllText(args[6],json.Serialize(new {summary,samples=data}));Console.WriteLine(json.Serialize(summary));return 0;
  }catch(Exception e){Console.Error.WriteLine(e.GetBaseException());
string probe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LumasharpChecker","LumaProbe");
if(Directory.Exists(probe))foreach(string d in Directory.GetDirectories(probe,"session-"+Process.GetCurrentProcess().Id+"-*"))foreach(string f in Directory.GetFiles(d,"*.json"))Console.WriteLine(File.ReadAllText(f));return 1;}
  finally {bridge.GetMethod("ShutdownWorker").Invoke(null,null);bridge.GetMethod("CleanupSessionArtifacts").Invoke(null,null);}
 }
}
