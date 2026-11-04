using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
class Program {
 static object Get(object o,string n)=>o.GetType().GetProperty(n).GetValue(o);
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
 [STAThread] static void Main(string[] args){
  AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{string f=Path.Combine(args[1],new AssemblyName(e.Name).Name+".dll");return File.Exists(f)?Assembly.LoadFrom(f):null;};
  var a=Assembly.LoadFrom(args[0]);Type type=a.GetType("LumasharpChecker.model.RecipeManager");string root=Path.Combine(AppContext.BaseDirectory,"fixtures-"+Guid.NewGuid().ToString("N"));
  object manager=Activator.CreateInstance(type,new object[]{root,Path.Combine(root,"profiles")});var create=type.GetMethod("CreateRecipe");object first=create.Invoke(manager,new object[]{"first",8}),second=create.Invoke(manager,new object[]{"second",8});var set=type.GetMethod("SetRecentImageSource");
  Check((string)Get(first,"RecentImageSource")=="OK","new recipe defaults to OK");
  var stations=((IEnumerable)Get(first,"Stations")).Cast<object>().ToArray();stations[0].GetType().GetProperty("RecentImageCount").SetValue(stations[0],3);stations[1].GetType().GetProperty("RecentImageCount").SetValue(stations[1],9);
  set.Invoke(manager,new object[]{first,"NG"});object[] loaded=((IEnumerable)type.GetMethod("LoadAllRecipes").Invoke(manager,null)).Cast<object>().ToArray();
  Check((string)Get(loaded.Single(x=>(string)Get(x,"Name")=="first"),"RecentImageSource")=="NG"&&(string)Get(loaded.Single(x=>(string)Get(x,"Name")=="second"),"RecentImageSource")=="OK","source persists per recipe without leaking to other recipe");
  Check((int)Get(stations[0],"RecentImageCount")==3&&(int)Get(stations[1],"RecentImageCount")==9,"per-station image counts preserved");
  set.Invoke(manager,new object[]{first,"ALL"});loaded=((IEnumerable)type.GetMethod("LoadAllRecipes").Invoke(manager,null)).Cast<object>().ToArray();Check((string)Get(loaded.Single(x=>(string)Get(x,"Name")=="first"),"RecentImageSource")=="ALL","ALL also persists");
  set.Invoke(manager,new object[]{first,"NG"});string file=Path.Combine(root,"recipes",(string)Get(first,"Id"),"recipe.json");string text=File.ReadAllText(file);text=new Regex(@"(?m)^\s*""recentImageSource"": ""NG"",?\r?\n").Replace(text,"",1);File.WriteAllText(file,text);
  loaded=((IEnumerable)type.GetMethod("LoadAllRecipes").Invoke(manager,null)).Cast<object>().ToArray();Check((string)Get(loaded.Single(x=>(string)Get(x,"Name")=="first"),"RecentImageSource")=="OK","legacy recipe missing global source defaults to OK despite old station ALL");
  string updated=(string)Get(first,"UpdatedAt");bool rejected=false;using(var locked=File.Open(file,FileMode.Open,FileAccess.Read,FileShare.None)){try{set.Invoke(manager,new object[]{first,"OK"});}catch(TargetInvocationException){rejected=true;}}
  Check(rejected&&(string)Get(first,"RecentImageSource")=="NG"&&(string)Get(first,"UpdatedAt")==updated,"failed save restores source and timestamp");
  bool invalid=false;try{set.Invoke(manager,new object[]{second,"BAD"});}catch(TargetInvocationException e){invalid=e.InnerException is ArgumentException;}Check(invalid&&(string)Get(second,"RecentImageSource")=="OK","invalid source rejected without mutation");
  Console.WriteLine("ALL RECIPE SOURCE CHECKS PASSED");
 }
}
