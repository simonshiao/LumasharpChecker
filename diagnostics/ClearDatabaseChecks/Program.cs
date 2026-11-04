using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
class Program {
 static object Get(object o,string n)=>o.GetType().GetProperty(n).GetValue(o);
 static void Set(object o,string n,object v)=>o.GetType().GetProperty(n).SetValue(o,v);
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
 [STAThread] static void Main(string[] args){
  string deps=args[1];AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{string f=Path.Combine(deps,new AssemblyName(e.Name).Name+".dll");return File.Exists(f)?Assembly.LoadFrom(f):null;};
  var a=Assembly.LoadFrom(args[0]);Type type=a.GetType("LumasharpChecker.model.RecipeManager");string root=Path.Combine(AppContext.BaseDirectory,"fixtures-"+Guid.NewGuid().ToString("N"));
  object manager=Activator.CreateInstance(type,new object[]{root,Path.Combine(root,"profiles")});object recipe=type.GetMethod("CreateRecipe").Invoke(manager,new object[]{"fixture",8});
  object[] stations=((IEnumerable)Get(recipe,"Stations")).Cast<object>().ToArray();object s7=stations.Single(s=>(int)Get(s,"Number")==7),s8=stations.Single(s=>(int)Get(s,"Number")==8);
  Action seed=()=>{Set(s7,"AllDatabasePath","all");Set(s7,"OkDatabasePath","ok");Set(s7,"NgDatabasePath","ng");type.GetMethod("SaveManifest").Invoke(manager,new[]{recipe});};
  Set(s8,"OkDatabasePath","other");Set(s7,"PatternFileName","standard.png");
  string marker=Path.Combine(root,"original.png");File.WriteAllText(marker,"unchanged");
  var clear=type.GetMethod("ClearStationDatabase");
  seed();clear.Invoke(manager,new object[]{recipe,7,"OK"});Check(Get(s7,"AllDatabasePath")==null&&Get(s7,"OkDatabasePath")==null&&(string)Get(s7,"NgDatabasePath")=="ng","clear OK also removes ALL override, preserves NG");
  seed();clear.Invoke(manager,new object[]{recipe,7,"NG"});Check(Get(s7,"AllDatabasePath")==null&&Get(s7,"NgDatabasePath")==null&&(string)Get(s7,"OkDatabasePath")=="ok","clear NG preserves OK");
  seed();clear.Invoke(manager,new object[]{recipe,7,"STATION"});Check(Get(s7,"AllDatabasePath")==null&&Get(s7,"OkDatabasePath")==null&&Get(s7,"NgDatabasePath")==null,"clear station removes three paths");
  Check((string)Get(s8,"OkDatabasePath")=="other"&&(string)Get(s7,"PatternFileName")=="standard.png"&&File.ReadAllText(marker)=="unchanged","other station, standard part and source file preserved");
  object loaded=((IEnumerable)type.GetMethod("LoadAllRecipes").Invoke(manager,null)).Cast<object>().Single();object loaded7=((IEnumerable)Get(loaded,"Stations")).Cast<object>().Single(s=>(int)Get(s,"Number")==7);Check(Get(loaded7,"OkDatabasePath")==null&&Get(loaded7,"NgDatabasePath")==null&&Get(loaded7,"AllDatabasePath")==null,"clear remains after recipe reload");
  Check(!(bool)clear.Invoke(manager,new object[]{recipe,7,"STATION"}),"clearing empty station is no-op");
  seed();string json=Path.Combine(root,"recipes",(string)Get(recipe,"Id"),"recipe.json");string updated=(string)Get(recipe,"UpdatedAt");bool rejected=false;
  using(var locked=File.Open(json,FileMode.Open,FileAccess.Read,FileShare.None)){try{clear.Invoke(manager,new object[]{recipe,7,"STATION"});}catch(TargetInvocationException){rejected=true;}}
  Check(rejected&&(string)Get(s7,"AllDatabasePath")=="all"&&(string)Get(s7,"OkDatabasePath")=="ok"&&(string)Get(s7,"NgDatabasePath")=="ng"&&(string)Get(recipe,"UpdatedAt")==updated,"failed manifest write restores paths and timestamp");
  bool invalid=false;try{clear.Invoke(manager,new object[]{recipe,9,"STATION"});}catch(TargetInvocationException e){invalid=e.InnerException is ArgumentOutOfRangeException;}Check(invalid,"station outside active recipe rejected");
  Console.WriteLine("ALL CLEAR DATABASE CHECKS PASSED");
 }
}
