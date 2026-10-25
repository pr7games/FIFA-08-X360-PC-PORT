using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace Pr7Editor {
public static partial class Originals {
 internal static BackupFile Find(string gameRoot,string backupRoot,string file,string expected) {
  if(!Directory.Exists(backupRoot))return null;string original=Path.GetFullPath(Path.Combine(gameRoot,file)),boundary=Path.GetFullPath(backupRoot).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  foreach(string json in Directory.GetFiles(backupRoot,"respaldo.json",SearchOption.AllDirectories).OrderBy(p=>p))try{
   var m=new JavaScriptSerializer().Deserialize<BackupManifest>(File.ReadAllText(json));if(m==null||m.Files==null||!SameRoot(m.GameRoot,gameRoot))continue;
   foreach(var f in m.Files)if(String.Equals(Path.GetFullPath(f.Original),original,StringComparison.OrdinalIgnoreCase)&&String.Equals(f.BeforeHash,expected,StringComparison.OrdinalIgnoreCase)&&Path.GetFullPath(f.Backup).StartsWith(boundary,StringComparison.OrdinalIgnoreCase)&&File.Exists(f.Backup)&&String.Equals(Bytes.Hash(f.Backup),expected,StringComparison.OrdinalIgnoreCase))return new BackupFile{Original=original,Backup=Path.GetFullPath(f.Backup),BeforeHash=expected};
  }catch(IOException){}catch(ArgumentException){}catch(InvalidOperationException){}return null;
 }
 static bool SameRoot(string a,string b){return String.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase);}
 public static void CheckSave(string gameRoot,string backupRoot,IEnumerable<Asset> assets){foreach(var archive in assets.Select(a=>a.Archive).Distinct())foreach(string file in new[]{Path.GetFileName(archive.FileName),Path.GetFileName(Path.ChangeExtension(archive.FileName,".bh"))}){string expected;if(!Known.TryGetValue(file,out expected))throw new IOException("Version de archivo no identificada: "+file);if(String.Equals(Bytes.Hash(Path.Combine(gameRoot,file)),expected,StringComparison.OrdinalIgnoreCase))continue;if(Find(gameRoot,backupRoot,file,expected)==null)throw new IOException("No hay un original verificable de "+file+". Restaura tu copia funcional original antes de volver a editar.");}}
 public static int RestoreAll(string gameRoot,string backupRoot){return RestoreSet(gameRoot,backupRoot,Known);}
 public static int RestoreSet(string gameRoot,string backupRoot,Dictionary<string,string> expected){Storage.CheckGameClosed();var plan=new BackupManifest{GameRoot=Path.GetFullPath(gameRoot),State="originals"};
  foreach(var item in expected){string current=Path.Combine(gameRoot,item.Key);if(!File.Exists(current))throw new IOException("Falta "+item.Key);if(String.Equals(Bytes.Hash(current),item.Value,StringComparison.OrdinalIgnoreCase))continue;var source=Find(gameRoot,backupRoot,item.Key,item.Value);if(source==null)throw new IOException("No se encontro un respaldo ORIGINAL verificado de "+item.Key+". No se ha restaurado ningun archivo.");plan.Files.Add(source);}
  if(plan.Files.Count==0)return 0;Storage.RestorePlan(plan,gameRoot,backupRoot);foreach(var item in expected)if(!String.Equals(Bytes.Hash(Path.Combine(gameRoot,item.Key)),item.Value,StringComparison.OrdinalIgnoreCase))throw new IOException("No se pudo verificar la restauracion completa.");return plan.Files.Count;
 }
}
}
