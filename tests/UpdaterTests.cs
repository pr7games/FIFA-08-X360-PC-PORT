using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using FIFA08Launcher;

internal static class UpdaterTests {
 static void Assert(bool result,string message){if(!result)throw new Exception(message);}
 static void Write(string root,string file,string content){string path=Path.Combine(root,file);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,content);}
 static string Snapshot(string root){return string.Join("\n",Directory.GetFiles(root,"*",SearchOption.AllDirectories).Where(p=>!p.EndsWith("update.lock")).OrderBy(p=>p).Select(p=>p.Substring(root.Length)+":"+File.ReadAllText(p)).ToArray());}
 static int Main(string[] args){
  string temp=Path.Combine(Path.GetTempPath(),"FIFA08-Updater-Test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
  try{
   Assert(PortUpdater.ParseVersion("v1.0.0")>PortUpdater.ParseVersion("0.0.5"),"Version ordering");
   Assert(PortUpdater.ParseVersion("v1.0.10")>PortUpdater.ParseVersion("1.0.9"),"Semantic version ordering");
   var release=new JavaScriptSerializer().Deserialize<UpdateRelease>("{\"tag_name\":\"v1.0.1\",\"assets\":[{\"name\":\"FIFA08-PR7-Setup-1.0.1.exe\",\"size\":12,\"digest\":\"sha256:"+new string('0',64)+"\",\"browser_download_url\":\""+PortUpdater.Repository+"/releases/download/v1.0.1/FIFA08-PR7-Setup-1.0.1.exe\"}]}");
   Assert(PortUpdater.SelectAsset(release).size==12,"GitHub JSON selection");
   release.assets[0].browser_download_url="https://example.com/untrusted.exe";
   bool rejected=false;try{PortUpdater.SelectAsset(release);}catch(InvalidDataException){rejected=true;}Assert(rejected,"Reject foreign package URL");
   string sample=Path.Combine(temp,"sample");File.WriteAllText(sample,"trusted");string digest;using(var h=SHA256.Create())digest="sha256:"+BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(sample))).Replace("-","").ToLowerInvariant();PortUpdater.Verify(sample,digest);
   File.AppendAllText(sample,"tampered");rejected=false;try{PortUpdater.Verify(sample,digest);}catch(InvalidDataException){rejected=true;}Assert(rejected,"Reject corrupt package");
   string payload=Path.Combine(temp,"payload"),root=Path.Combine(temp,"install");
   foreach(string file in new[]{"FIFA 08.exe","FIFA 08 Launcher.exe","FIFA08 editor by pr7.exe","native/build/fifa08.exe","native/build/Backends/Vulkan/manifest.json","native/build/Backends/D3D12/manifest.json","native/build/new.dll","probar-fifa08.ps1","launcher-settings.json","launcher-controls.json","game/data0.big","mods/edited.dds","data/user/save"})Write(payload,file,"NEW");
   foreach(string file in new[]{"FIFA 08.exe","FIFA 08 Launcher.exe","FIFA08 editor by pr7.exe","bin/probar-fifa08.ps1","bin/launcher-settings.json","bin/launcher-controls.json","game/data0.big","mods/edited.dds","data/user/save"})Write(root,file,"ORIGINAL");
   string before=Snapshot(root);bool interrupted=false;
   try{PortUpdater.Apply(payload,root,Path.Combine(temp,"rollback-fail"),n=>{if(n==5)throw new IOException("Simulated interruption");});}catch(IOException){interrupted=true;}
   Assert(interrupted&&before==Snapshot(root),"Rollback restores overwritten files and removes new files");
   PortUpdater.Apply(payload,root,Path.Combine(temp,"rollback-ok"));
   foreach(string file in new[]{"bin/launcher-settings.json","bin/launcher-controls.json","game/data0.big","mods/edited.dds","data/user/save"})Assert(File.ReadAllText(Path.Combine(root,file))=="ORIGINAL","Preserved "+file);
   Assert(File.ReadAllText(Path.Combine(root,"bin/native/build/new.dll"))=="NEW","New component installed");
   string realInstaller=args.FirstOrDefault(a=>a!="--github");
   if(args.Contains("--github")){
    System.Net.ServicePointManager.SecurityProtocol|=System.Net.SecurityProtocolType.Tls12;
    using(var client=new UpdateClient()){
     var liveRelease=new JavaScriptSerializer().Deserialize<UpdateRelease>(client.DownloadString(PortUpdater.Latest));var asset=PortUpdater.SelectAsset(liveRelease);
     realInstaller=Path.Combine(temp,asset.name);client.DownloadFile(asset.browser_download_url,realInstaller);PortUpdater.Verify(realInstaller,asset.digest);
     Assert(new FileInfo(realInstaller).Length==asset.size,"Live asset length");Console.WriteLine("Verified GitHub "+liveRelease.tag_name+" ("+asset.size+" bytes).");
    }
   }
   if(realInstaller!=null){
    // Optional real installer: exercise its CLI extractor and the exact installation mapping.
    string extracted=Path.Combine(temp,"real-payload");Directory.CreateDirectory(extracted);
    using(var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(realInstaller,"--extract-payload \""+extracted+"\""){UseShellExecute=false,CreateNoWindow=true})){Assert(process.WaitForExit(120000)&&process.ExitCode==0,"Installer extraction");}
    PortUpdater.Apply(extracted,root,Path.Combine(temp,"rollback-real"));
    Assert(File.ReadAllText(Path.Combine(root,"game/data0.big"))=="ORIGINAL","Real installer preserves edited game");
    Assert(File.ReadAllText(Path.Combine(root,"bin/launcher-settings.json"))=="ORIGINAL","Real installer preserves settings");
    Assert(new FileInfo(Path.Combine(root,"bin/native/build/fifa08.exe")).Length>1000,"Real runtime installed");
   }
   Console.WriteLine("PASS: version, API JSON, trusted URL, digest, rollback, preservation, package installation.");return 0;
  }finally{Directory.Delete(temp,true);}
 }
}
