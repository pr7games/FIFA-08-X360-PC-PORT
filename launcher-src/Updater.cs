using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FIFA08Launcher {
 internal sealed class UpdateAsset { public string name {get;set;} public string browser_download_url {get;set;} public string digest {get;set;} public long size {get;set;} }
 internal sealed class UpdateRelease { public string tag_name {get;set;} public bool draft {get;set;} public bool prerelease {get;set;} public UpdateAsset[] assets {get;set;} }
 internal sealed class UpdateJob { public string Root; public string Installer; public string Digest; public string Version; public int Parent; public bool English; }
 internal sealed class UpdateClient : WebClient {
  public UpdateClient() { Headers[HttpRequestHeader.UserAgent]="FIFA08-PR7/"+PortUpdater.CurrentVersion; Headers[HttpRequestHeader.Accept]="application/vnd.github+json"; }
  protected override WebRequest GetWebRequest(Uri address) { var request=base.GetWebRequest(address);request.Timeout=30000;var http=request as HttpWebRequest;if(http!=null)http.ReadWriteTimeout=30000;return request; }
 }
 internal static class PortUpdater {
#if UPDATE_TEST_BUILD
  public const string CurrentVersion="0.0.5";
#else
  public const string CurrentVersion="1.0.0";
#endif
  public const string Repository="https://github.com/pr7games/FIFA-08-X360-PC-PORT";
  public const string Latest="https://api.github.com/repos/pr7games/FIFA-08-X360-PC-PORT/releases/latest";
  public static bool Interactive=true;
  public static Version ParseVersion(string value) { Version version;if(!Version.TryParse((value??"").TrimStart('v','V'),out version))throw new InvalidDataException("Invalid release version.");return version; }
  public static UpdateAsset SelectAsset(UpdateRelease release) {
   if(release==null||release.draft||release.prerelease)throw new InvalidDataException("Invalid release.");
   string version=ParseVersion(release.tag_name).ToString();
   var asset=(release.assets??new UpdateAsset[0]).SingleOrDefault(a=>a.name=="FIFA08-PR7-Setup-"+version+".exe");
   string prefix=Repository+"/releases/download/"+release.tag_name+"/";
   if(asset==null||asset.size<=0||asset.size>1024L*1024*1024||asset.browser_download_url!=prefix+asset.name||!ValidDigest(asset.digest))throw new InvalidDataException("Release package or SHA-256 is missing.");return asset;
  }
  static bool ValidDigest(string digest) { return digest!=null&&digest.StartsWith("sha256:")&&digest.Length==71&&digest.Substring(7).All(c=>Uri.IsHexDigit(c)); }
  public static void Verify(string path,string digest) {
   if(!ValidDigest(digest))throw new InvalidDataException("Missing SHA-256.");
   using(var stream=File.OpenRead(path))using(var hash=SHA256.Create())if(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=digest.Substring(7).ToLowerInvariant())throw new InvalidDataException("SHA-256 verification failed. No files were changed.");
  }
  public static bool Busy() {
   foreach(string name in new[]{"fifa08","FIFA08 editor by pr7"})foreach(var p in Process.GetProcessesByName(name))using(p){try{if(!p.HasExited)return true;}catch(InvalidOperationException){}}
   return false;
  }
  static string SafePath(string root,string relative) {
   if(Directory.Exists(root)&&(File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new IOException("Update root cannot use junctions.");
   string full=Path.GetFullPath(Path.Combine(root,relative));string prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Unsafe update path.");
   for(string dir=Path.GetDirectoryName(full);dir!=null&&dir.StartsWith(prefix,StringComparison.OrdinalIgnoreCase);dir=Path.GetDirectoryName(dir))if(Directory.Exists(dir)&&(File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)throw new IOException("Update paths cannot use junctions.");
   if(File.Exists(full)&&(File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new IOException("Update paths cannot use symbolic links.");return full;
  }
  static bool ProgramFile(string relative) {
   string r=relative.Replace('\\','/');
   if(new[]{"FIFA 08.exe","FIFA 08 Launcher.exe","FIFA08 editor by pr7.exe","probar-fifa08.ps1","configurar-idiomas.ps1","display-config.ps1","FIFA09-NOTICE.txt","NOVEDADES-FIFA08.txt","port-manifest.json","launcher-src/GameLanguages.cs"}.Contains(r,StringComparer.OrdinalIgnoreCase))return true;
   return new[]{"native/build/","launcher-assets/","modding/","sounds/"}.Any(p=>r.StartsWith(p,StringComparison.OrdinalIgnoreCase));
  }
  public static void Apply(string payload,string root,string backup,Action<int> afterCopy=null) {
   if(!File.Exists(Path.Combine(root,"bin","probar-fifa08.ps1")))throw new IOException("This is not a clean-layout FIFA 08 installation.");
   string[] required={"FIFA 08.exe","FIFA 08 Launcher.exe","FIFA08 editor by pr7.exe","native/build/fifa08.exe","native/build/Backends/Vulkan/manifest.json","native/build/Backends/D3D12/manifest.json"};
   foreach(string path in required)if(!File.Exists(SafePath(payload,path)))throw new InvalidDataException("Incomplete package: "+path);
   var changes=new List<Tuple<string,string,bool>>();
   using(var installLock=new FileStream(Path.Combine(root,"bin","update.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
    try {
     foreach(string source in Directory.GetFiles(payload,"*",SearchOption.AllDirectories)) {
      string relative=source.Substring(payload.TrimEnd('\\').Length+1);if(!ProgramFile(relative))continue;
      bool top=new[]{"FIFA 08.exe","FIFA 08 Launcher.exe","FIFA08 editor by pr7.exe"}.Contains(relative,StringComparer.OrdinalIgnoreCase);
      string target=SafePath(root,top?relative:Path.Combine("bin",relative));string saved=SafePath(backup,top?relative:Path.Combine("bin",relative));
      bool existed=File.Exists(target);if(existed){Directory.CreateDirectory(Path.GetDirectoryName(saved));File.Copy(target,saved,false);}
      changes.Add(Tuple.Create(target,saved,existed));Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(source,target,true);
      if(afterCopy!=null)afterCopy(changes.Count);
     }
    } catch(Exception original) {
     var failures=new List<string>();foreach(var change in changes.AsEnumerable().Reverse())try{if(change.Item3)File.Copy(change.Item2,change.Item1,true);else if(File.Exists(change.Item1))File.Delete(change.Item1);}catch(Exception e){failures.Add(e.Message);}
     if(failures.Count>0)throw new IOException("Rollback needs recovery from "+backup+": "+string.Join("; ",failures.ToArray()),original);throw;
    }
   }
  }
  public static int RunHelper(string jobFile) {
   UpdateJob job=null;bool success=false;
   try {
    job=new JavaScriptSerializer().Deserialize<UpdateJob>(File.ReadAllText(jobFile));
    try{using(var parent=Process.GetProcessById(job.Parent))if(!parent.WaitForExit(30000))throw new IOException("Close the launcher before updating.");}catch(ArgumentException){}
    if(Busy())throw new IOException("Close FIFA 08 and its editor before updating.");
    Verify(job.Installer,job.Digest);
    string stage=Path.GetDirectoryName(jobFile),payload=Path.Combine(stage,"payload");Directory.CreateDirectory(payload);
    using(var extractor=Process.Start(new ProcessStartInfo(job.Installer,"--extract-payload \""+payload+"\""){UseShellExecute=false,CreateNoWindow=true})) {
     if(!extractor.WaitForExit(120000)){extractor.Kill();throw new IOException("Package extraction timed out.");}if(extractor.ExitCode!=0)throw new IOException("Package extraction failed.");
    }
    if(Busy())throw new IOException("Close FIFA 08 and its editor before updating.");
    Apply(payload,job.Root,Path.Combine(stage,"rollback"));success=true;
    WriteLog(job.Root,"Updated to "+job.Version);
   } catch(Exception e) {
    if(job!=null)WriteLog(job.Root,"FAILED: "+e);
    MessageBox.Show((job!=null&&job.English?"Update failed. Review the details and update.log.\n":"La actualizacion fallo. Revisa los detalles y update.log.\n")+e.Message,"FIFA 08 by PR7",MessageBoxButtons.OK,MessageBoxIcon.Error);
   }
   if(job!=null)try{Process.Start(new ProcessStartInfo(Path.Combine(job.Root,"FIFA 08 Launcher.exe")){WorkingDirectory=job.Root,UseShellExecute=true});}catch(Exception e){MessageBox.Show(e.Message,"FIFA 08 by PR7");}
   // The helper itself is running in this directory. Keep only a small receipt;
   // failed transactions retain rollback files for manual recovery.
   if(success)try{string stage=Path.GetDirectoryName(jobFile);Directory.Delete(Path.Combine(stage,"payload"),true);Directory.Delete(Path.Combine(stage,"rollback"),true);File.Delete(job.Installer);File.Delete(jobFile);}catch(IOException){}catch(UnauthorizedAccessException){}
   return success?0:1;
  }
  static void WriteLog(string root,string text) { try{string dir=Path.Combine(root,"data","logs");Directory.CreateDirectory(dir);File.AppendAllText(Path.Combine(dir,"update.log"),DateTime.Now.ToString("s")+" "+text+Environment.NewLine);}catch{} }
 }

 public partial class LauncherForm {
  UpdateRelease latestRelease;UpdateAsset latestAsset;UpdateClient downloadClient;
  bool checkingUpdate,downloadingUpdate,updateHandoff;string updateError;int downloadPercent;
  UpdateProgress updateProgress;
  void InitializeUpdater() {
   githubProject.Enabled=true;githubProject.Click+=(s,e)=>Process.Start(PortUpdater.Repository);
   updateProgress=new UpdateProgress{Height=6,Margin=new Padding(0,0,0,8),Visible=false};aboutStack.Controls.Add(updateProgress);
   updateVersion.Click+=async(s,e)=>{if(latestAsset!=null)await DownloadUpdate();else await CheckUpdate();};
   Shown+=async(s,e)=>{if(PortUpdater.Interactive)await CheckUpdate();};
   FormClosing+=(s,e)=>{if(downloadClient!=null)downloadClient.CancelAsync();};
  }
  async Task CheckUpdate() {
   if(checkingUpdate||downloadingUpdate)return;checkingUpdate=true;updateError=null;RefreshAbout();
   try {
    ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
    using(var client=new UpdateClient()) {
     var release=new JavaScriptSerializer().Deserialize<UpdateRelease>(await client.DownloadStringTaskAsync(PortUpdater.Latest));
     var asset=PortUpdater.SelectAsset(release);
     if(PortUpdater.ParseVersion(release.tag_name)>PortUpdater.ParseVersion(PortUpdater.CurrentVersion)){latestRelease=release;latestAsset=asset;SetUpdateAvailable(release.tag_name);}else SetUpdateAvailable(null);
    }
   }catch(Exception e){updateError=e.Message;}finally{checkingUpdate=false;if(!IsDisposed)RefreshAbout();}
  }
  string InstallRoot(){return File.Exists(Path.Combine(rootDir,"probar-fifa08.ps1"))&&Path.GetFileName(rootDir).Equals("bin",StringComparison.OrdinalIgnoreCase)?Path.GetDirectoryName(rootDir):rootDir;}
  async Task DownloadUpdate() {
   if(downloadingUpdate||latestAsset==null)return;
   if(PortUpdater.Busy()){MessageBox.Show(this,T2("Cierra el juego y el editor antes de actualizar.","Close the game and editor before updating."),Text);return;}
   string stage=Path.Combine(Path.GetTempPath(),"FIFA08-Update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
   string installer=Path.Combine(stage,latestAsset.name);downloadingUpdate=true;downloadPercent=0;updateError=null;btnPlay.Enabled=false;RefreshAbout();
   try {
    using(downloadClient=new UpdateClient()) {
     downloadClient.DownloadProgressChanged+=(s,e)=>{if(!IsDisposed){downloadPercent=e.ProgressPercentage;RefreshAbout();}};
     await downloadClient.DownloadFileTaskAsync(latestAsset.browser_download_url,installer);
    }
    downloadClient=null;
    if(new FileInfo(installer).Length!=latestAsset.size)throw new InvalidDataException("Incomplete download.");
    await Task.Run(()=>PortUpdater.Verify(installer,latestAsset.digest));
    if(IsDisposed)return;
    if(PortUpdater.Busy())throw new IOException(T2("Cierra el juego y el editor y vuelve a intentarlo.","Close the game and editor and try again."));
    SaveSettings();
    var job=new UpdateJob{Root=InstallRoot(),Installer=installer,Digest=latestAsset.digest,Version=latestRelease.tag_name,Parent=Process.GetCurrentProcess().Id,English=PortUi.English};
    string jobFile=Path.Combine(stage,"update.json"),helper=Path.Combine(stage,"UpdateHelper.exe");
    File.WriteAllText(jobFile,new JavaScriptSerializer().Serialize(job));File.Copy(typeof(LauncherForm).Assembly.Location,helper);
    // Request elevation only when the installed folder is not writable.
    bool elevation=false;string probe=Path.Combine(job.Root,"bin",".update-probe-"+Guid.NewGuid().ToString("N"));
    try{File.WriteAllText(probe,"");File.Delete(probe);}catch(UnauthorizedAccessException){elevation=true;}
    Process.Start(new ProcessStartInfo(helper,"--apply-update \""+jobFile+"\""){UseShellExecute=true,Verb=elevation?"runas":""});
    updateHandoff=true;Close();
   }catch(Exception e){updateError=e.Message;if(!IsDisposed)MessageBox.Show(this,T2("No se pudo actualizar. Tus archivos se conservan.\n","Update failed. Your files are unchanged.\n")+e.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Information);}
   finally{downloadClient=null;downloadingUpdate=false;if(!updateHandoff)try{File.Delete(installer);}catch{}if(!IsDisposed&&!updateHandoff){btnPlay.Enabled=true;RefreshAbout();}}
  }
  void RefreshUpdater() {
   githubProject.Text=T2("GitHub del proyecto","Project GitHub");updateVersion.Text=T2(latestAsset==null?"Buscar actualizaciones":"Actualizar version",latestAsset==null?"Check for updates":"Update version");
   updateVersion.Enabled=!checkingUpdate&&!downloadingUpdate;updateNotice.ForeColor=Color.Silver;
   if(checkingUpdate)updateNotice.Text=T2("Buscando actualizaciones...","Checking for updates...");
   else if(downloadingUpdate)updateNotice.Text=downloadPercent>=100?T2("Verificando paquete... El launcher se reiniciara.","Verifying package... The launcher will restart."):T2("Descargando paquete completo: ","Downloading full package: ")+downloadPercent+"%";
   else if(updateError!=null){updateNotice.Text=T2("No se pudo actualizar. Pulsa el boton para reintentar.","Update failed. Press the button to retry.");updateNotice.ForeColor=Color.FromArgb(240,110,115);updateNotice.AccessibleDescription=updateError;}
   else if(updateAvailable){updateNotice.Text=T2("Hay una nueva version disponible: ","A new version is available: ")+availableVersion;updateNotice.ForeColor=Color.FromArgb(240,110,115);}
   else updateNotice.Text=T2("Tienes la version mas reciente.","You have the latest version.");
   if(updateProgress!=null){updateProgress.Visible=downloadingUpdate;updateProgress.Value=downloadPercent;}
  }
 }
 internal sealed class UpdateProgress : Control {
  int value;public int Value{get{return value;}set{this.value=Math.Max(0,Math.Min(100,value));Invalidate();}}
  public UpdateProgress(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Color.FromArgb(40,40,46));using(var brush=new SolidBrush(PortUi.Accent))e.Graphics.FillRectangle(brush,0,0,Width*value/100,Height);}
 }
}
