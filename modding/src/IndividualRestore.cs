using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pr7Editor {
public static partial class Originals {
 internal static string SourceArchive(Asset asset,string gameRoot,string backupRoot) {
  string file=Path.GetFileName(asset.Archive.FileName),expected;
  if(!Known.TryGetValue(file,out expected))throw new IOException("No se reconoce el original de "+file+".");
  string source=Path.Combine(gameRoot,file);
  if(!String.Equals(Bytes.Hash(source),expected,StringComparison.OrdinalIgnoreCase)) {
   var backup=Find(gameRoot,backupRoot,file,expected);
   if(backup==null)throw new IOException("No hay un respaldo original verificado de "+file+". No se ha cambiado ningun recurso.");
   source=backup.Backup;
  }
  return source;
 }
 public static byte[] ReadResource(Asset asset,string gameRoot,string backupRoot) {
  var archive=new Archive(SourceArchive(asset,gameRoot,backupRoot));var original=archive.Find(asset.Name);
  if(original==null)throw new IOException("El recurso no existe en el original verificado: "+asset.Name);
  return archive.Read(original);
 }
}

public sealed partial class Session {
 GameDatabase originalDatabase;
 GameDatabase OriginalDatabase(string backupRoot) {
  if(originalDatabase==null) {
   var db=Originals.ReadResource(DbAsset,Root,backupRoot);
   var meta=Originals.ReadResource(MetaAsset,Root,backupRoot);
   originalDatabase=new GameDatabase(db,meta);
   Appearance=new AppearanceRules(Assets,Db.Rows("players").Concat(originalDatabase.Rows("players")));
  }
  return originalDatabase;
 }
 public void RestoreResource(Asset asset,string backupRoot) {
  if(asset==DbAsset||asset==MetaAsset)throw new InvalidOperationException("Usa Restaurar jugador para los datos de jugadores.");
  byte[] original=Originals.ReadResource(asset,Root,backupRoot);
  // A resource restore is prepared just like an import; Save commits it together
  // with any other edits. An unsaved import over an original needs no disk write.
  if(asset.Archive.Read(asset).SequenceEqual(original))Pending.Remove(asset);
  else Pending[asset]=original;
 }
 public void RestorePlayer(int playerId,string backupRoot) {
  var original=OriginalDatabase(backupRoot);
  int target=Db.Rows("players").FindIndex(r=>GameDatabase.Number(r,"playerid")==playerId);
  var source=original.Rows("players").SingleOrDefault(r=>GameDatabase.Number(r,"playerid")==playerId);
  int nameTarget=Db.Rows("playernames").FindIndex(r=>GameDatabase.Number(r,"playerid")==playerId);
  var nameSource=original.Rows("playernames").SingleOrDefault(r=>GameDatabase.Number(r,"playerid")==playerId);
  if(target<0||source==null||nameTarget<0||nameSource==null)throw new InvalidOperationException("El jugador no existe en el original verificado.");
  foreach(var field in Db.Tables["players"].Fields.Where(f=>f.Type==2||f.Type==3)) {
   long value=GameDatabase.Number(source,field.Name);
   if(GameDatabase.Number(Db.Rows("players")[target],field.Name)!=value) { Db.SetNumber("players",target,field.Name,value);DbDirty=true; }
  }
  foreach(string field in new[]{"firstname","surname","commonname","playerjerseyname"}) {
   string value=Convert.ToString(nameSource[field]);
   if(Convert.ToString(Db.Rows("playernames")[nameTarget][field])!=value) { Db.SetName(nameTarget,field,value);DbDirty=true; }
  }
 }
}

public sealed partial class EditorForm {
 void RestoreSelectedPlayer() {
  if(State==null||Selected==null)throw new InvalidOperationException("Selecciona un jugador.");
  int id=Selected.Id;
  State.RestorePlayer(id,BackupRoot);
  // Replace the visible cells directly: invalid or uncommitted edits on THIS
  // player may be discarded, without touching queued edits on other players.
  ShowPlayerCore();
  Note("Original del jugador "+id+" preparado. Pulsa Guardar cambios. Los demas jugadores y recursos conservan sus cambios.");
 }
 public void PrepareResourceRestore(Asset asset) {
  State.RestoreResource(asset,BackupRoot);
  Note(State.Pending.ContainsKey(asset)?"Original preparado: "+asset.Name+". Pulsa Guardar cambios. Solo se restaura este recurso completo.":"Este recurso ya coincide con el original. Se descarto su importacion pendiente; las demas ediciones se conservan.");
 }
}
}
