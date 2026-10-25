using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Pr7Editor {
public sealed class SaveTransactionFile {public string Original,Stage,Undo,BeforeHash,AfterHash;}
public sealed class SaveTransaction {public string GameRoot,State;public List<SaveTransactionFile> Files=new List<SaveTransactionFile>();}
public static class OriginalBackupStore {
 static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=Int32.MaxValue};}
 static string Root(string path){return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);}
 static bool Same(string a,string b){return String.Equals(Root(a),Root(b),StringComparison.OrdinalIgnoreCase);}
 static bool HashIs(string file,string hash){return File.Exists(file)&&String.Equals(Bytes.Hash(file),hash,StringComparison.OrdinalIgnoreCase);}
 static bool ArchiveName(string name){return Regex.IsMatch(name,@"^data[0-6]\.(big|bh)$",RegexOptions.IgnoreCase);}
 // The editor can open several installations. Each has one original set;
 // another game's working copy must never inherit the first game's manifest.
 static string Scope(string game,string backups){backups=Root(backups);string json=Path.Combine(backups,"originals","respaldo.json");if(!File.Exists(json))return backups;var m=Json().Deserialize<BackupManifest>(File.ReadAllText(json));if(m==null||String.IsNullOrEmpty(m.GameRoot))throw new IOException("Indice de originales invalido.");if(Same(m.GameRoot,game))return backups;using(var hash=System.Security.Cryptography.SHA256.Create()){string id=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Root(game).ToUpperInvariant()))).Replace("-","").Substring(0,32);return Path.Combine(backups,"games",id);}}
 static FileStream Lock(string backups){Directory.CreateDirectory(backups);return new FileStream(Path.Combine(backups,".writer.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
 static void WriteJson(string path,object value){string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{byte[] data=Encoding.UTF8.GetBytes(Json().Serialize(value));using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(data,0,data.Length);stream.Flush(true);}if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
 static BackupManifest Load(string game,string backups){string folder=Path.Combine(backups,"originals"),path=Path.Combine(folder,"respaldo.json");if(!File.Exists(path))return new BackupManifest{GameRoot=Root(game),State="originals"};var m=Json().Deserialize<BackupManifest>(File.ReadAllText(path));
  if(m==null||m.Files==null||!Same(m.GameRoot,game)||m.State!="originals"||m.Files.Select(f=>f.Original).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=m.Files.Count)throw new IOException("Respaldo original invalido o de otra instalacion.");
  foreach(var f in m.Files){string name=Path.GetFileName(f.Original);if(!ArchiveName(name)||!Same(f.Original,Path.Combine(game,name))||!Same(f.Backup,Path.Combine(folder,name))||!HashIs(f.Backup,f.BeforeHash))throw new IOException("Respaldo original alterado: "+name);}return m;
 }
 static void AddOriginal(BackupManifest m,string game,string backups,string name,string source,string expected){
  if(m.Files.Any(f=>String.Equals(Path.GetFileName(f.Original),name,StringComparison.OrdinalIgnoreCase)))return;
  string folder=Path.Combine(backups,"originals"),target=Path.Combine(folder,name);Directory.CreateDirectory(folder);
  if(File.Exists(target)){if(!HashIs(target,expected))throw new IOException("El original existente no se sobrescribira: "+name);}
  else {string temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.Copy(source,temp);if(!HashIs(temp,expected))throw new IOException("Copia original no verificable: "+name);File.Move(temp,target);}finally{if(File.Exists(temp))File.Delete(temp);}}
  m.Files.Add(new BackupFile{Original=Path.Combine(Root(game),name),Backup=Path.GetFullPath(target),BeforeHash=expected,AfterHash=expected});
  WriteJson(Path.Combine(folder,"respaldo.json"),m);
 }
 static void Ensure(BackupManifest m,string game,string backups,IEnumerable<string> files){foreach(string original in files){string name=Path.GetFileName(original);if(!ArchiveName(name)||!Same(original,Path.Combine(game,name)))throw new IOException("Archivo fuera de la carpeta del juego.");if(m.Files.Any(f=>String.Equals(Path.GetFileName(f.Original),name,StringComparison.OrdinalIgnoreCase)))continue;
   string hash=Bytes.Hash(original),source=original,known;BackupFile old;
   if(Originals.Known.TryGetValue(name,out known)&&!String.Equals(hash,known,StringComparison.OrdinalIgnoreCase)&&(old=Originals.Find(game,backups,name,known))!=null){source=old.Backup;hash=known;}
   // Internal fixture saves may use synthetic archives. Production Save first
   // validates every archive against Originals.Known or verified originals.
   AddOriginal(m,game,backups,name,source,hash);
  }}
 static string Journal(string backups){return Path.Combine(backups,"pending-save.json");}
 static void ValidateTransaction(SaveTransaction tx,string game){
  if(tx==null||tx.Files==null||!Same(tx.GameRoot,game)||tx.Files.Count==0||tx.Files.Select(f=>f.Original).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=tx.Files.Count||tx.State!="applying"&&tx.State!="complete")throw new IOException("Transaccion de guardado invalida.");
  foreach(var f in tx.Files){string name=Path.GetFileName(f.Original);if(!ArchiveName(name)||!Same(f.Original,Path.Combine(game,name))||!Same(Path.GetDirectoryName(f.Stage),game)||!Same(Path.GetDirectoryName(f.Undo),game)||!Regex.IsMatch(Path.GetFileName(f.Stage),"^"+Regex.Escape(name)+@"\.pr7-[0-9a-f]{32}\.tmp$")||!Regex.IsMatch(Path.GetFileName(f.Undo),"^"+Regex.Escape(name)+@"\.undo-[0-9a-f]{32}\.tmp$"))throw new IOException("Rutas de transaccion invalidas.");}
 }
 static void Recover(string game,string backups){string journal=Journal(backups);if(!File.Exists(journal))return;var tx=Json().Deserialize<SaveTransaction>(File.ReadAllText(journal));ValidateTransaction(tx,game);
  // Validate every rollback before touching any active file. Undo holds the
  // previous edited working copy, never the pristine original.
  if(tx.State=="applying"){
   foreach(var f in tx.Files){if(File.Exists(f.Undo)){if(!HashIs(f.Undo,f.BeforeHash)||!HashIs(f.Original,f.AfterHash)&&!HashIs(f.Original,f.BeforeHash))throw new IOException("No se puede recuperar un guardado: archivos cambiados.");}else if(!HashIs(f.Original,f.BeforeHash))throw new IOException("Falta una copia temporal de recuperacion.");}
   foreach(var f in tx.Files.AsEnumerable().Reverse())if(File.Exists(f.Undo))File.Replace(f.Undo,f.Original,null);
  }
  foreach(var f in tx.Files){if(File.Exists(f.Stage))File.Delete(f.Stage);if(File.Exists(f.Undo))File.Delete(f.Undo);}File.Delete(journal);
 }
 public static void RecoverPending(string game,string backups){backups=Scope(game,backups);if(!File.Exists(Journal(backups)))return;Storage.CheckGameClosed();using(Lock(backups))Recover(Root(game),backups);}
 internal static string Save(string game,Dictionary<Asset,byte[]> changes,string backups){if(changes.Count==0)return null;game=Root(game);backups=Scope(game,backups);using(Lock(backups)){
  Recover(game,backups);var manifest=Load(game,backups);var archives=changes.Keys.Select(a=>a.Archive).Distinct().ToArray();
  Ensure(manifest,game,backups,archives.SelectMany(a=>new[]{a.FileName,Path.ChangeExtension(a.FileName,".bh")}));
  var tx=new SaveTransaction{GameRoot=game,State="applying"};bool journalWritten=false;
  try {
   foreach(var archive in archives){var files=new List<SaveTransactionFile>();foreach(string original in new[]{archive.FileName,Path.ChangeExtension(archive.FileName,".bh")}){string id=Guid.NewGuid().ToString("N");var f=new SaveTransactionFile{Original=Path.GetFullPath(original),Stage=Path.GetFullPath(original)+".pr7-"+id+".tmp",Undo=Path.GetFullPath(original)+".undo-"+id+".tmp",BeforeHash=Bytes.Hash(original)};files.Add(f);tx.Files.Add(f);}archive.Stage(files[0].Stage,files[1].Stage,changes.Where(p=>p.Key.Archive==archive).ToDictionary(p=>p.Key.Name,p=>p.Value));}
   foreach(var f in tx.Files){f.AfterHash=Bytes.Hash(f.Stage);if(!HashIs(f.Original,f.BeforeHash))throw new IOException("El archivo cambio mientras se preparaba el guardado.");}
   WriteJson(Journal(backups),tx);journalWritten=true;
   foreach(var f in tx.Files)File.Replace(f.Stage,f.Original,f.Undo);
   tx.State="complete";WriteJson(Journal(backups),tx);
  }catch{if(journalWritten)Recover(game,backups);throw;}
  finally{if(!journalWritten)foreach(var f in tx.Files)if(File.Exists(f.Stage))File.Delete(f.Stage);}
  Recover(game,backups);return Path.Combine(backups,"originals");
 }}
 public static string Compact(string game,string backups){Storage.CheckGameClosed();game=Root(game);backups=Scope(game,backups);if(Same(game,backups)||!Directory.Exists(game))throw new IOException("Carpetas invalidas.");using(Lock(backups)){
  Recover(game,backups);var manifest=Load(game,backups);var dirs=Directory.GetDirectories(backups).Where(d=>Regex.IsMatch(Path.GetFileName(d),@"^\d{8}-\d{6}-\d{3}$")).ToArray();
  var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long oldBytes=0;var metadata=new List<string>();
  // Only dated, flat, known backup directories are eligible for deletion.
  foreach(string dir in dirs){if(!Same(Path.GetDirectoryName(dir),backups)||(File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0||Directory.GetDirectories(dir).Length!=0)throw new IOException("Carpeta de respaldo no reconocida: "+dir);
   string json=Path.Combine(dir,"respaldo.json");if(!File.Exists(json))throw new IOException("Falta el indice de un respaldo.");metadata.Add(File.ReadAllText(json));
   foreach(string file in Directory.GetFiles(dir)){if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("Enlace en un respaldo.");string name=Path.GetFileName(file);if(name!="respaldo.json"&&!Originals.Known.ContainsKey(name))throw new IOException("Archivo inesperado en respaldo: "+file);if(name!="respaldo.json")names.Add(name);oldBytes+=new FileInfo(file).Length;}
  }
  var active=Originals.Known.Keys.Where(n=>File.Exists(Path.Combine(game,n))).ToDictionary(n=>n,n=>Bytes.Hash(Path.Combine(game,n)),StringComparer.OrdinalIgnoreCase);
  foreach(var item in active)if(!String.Equals(item.Value,Originals.Known[item.Key],StringComparison.OrdinalIgnoreCase))names.Add(item.Key);
  foreach(string name in names){if(manifest.Files.Any(f=>String.Equals(Path.GetFileName(f.Original),name,StringComparison.OrdinalIgnoreCase)))continue;string expected=Originals.Known[name],source=null;
   if(HashIs(Path.Combine(game,name),expected))source=Path.Combine(game,name);
   else foreach(string dir in dirs){string candidate=Path.Combine(dir,name);if(HashIs(candidate,expected)){source=candidate;break;}}
   if(source==null)throw new IOException("No se encontro el original verificable de "+name+"; no se eliminara ningun respaldo.");
   AddOriginal(manifest,game,backups,name,source,expected);
  }
  manifest=Load(game,backups);foreach(string name in names){var f=manifest.Files.Single(p=>String.Equals(Path.GetFileName(p.Original),name,StringComparison.OrdinalIgnoreCase));if(!String.Equals(f.BeforeHash,Originals.Known[name],StringComparison.OrdinalIgnoreCase))throw new IOException("El respaldo no es original.");}
  foreach(var item in active)if(!HashIs(Path.Combine(game,item.Key),item.Value))throw new IOException("El juego cambio; no se eliminaran respaldos.");
  // Retain tiny historical metadata, not the old full-archive snapshots.
  if(dirs.Length>0)WriteJson(Path.Combine(backups,"retired-history.json"),metadata);
  foreach(string dir in dirs){string resolved=Root(dir);if(!Same(Path.GetDirectoryName(resolved),backups)||Same(resolved,Path.Combine(backups,"originals")))throw new IOException("Destino de limpieza invalido.");Directory.Delete(resolved,true);}
  long kept=manifest.Files.Sum(f=>new FileInfo(f.Backup).Length);return "Originales conservados: "+manifest.Files.Count+" archivos; generaciones retiradas: "+dirs.Length+"; bytes de historial retirados: "+oldBytes+"; bytes en originales: "+kept+". Archivos activos verificados sin cambios.";
 }}
}
}
