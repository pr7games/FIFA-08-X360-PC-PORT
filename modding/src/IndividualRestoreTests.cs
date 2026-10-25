using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace Pr7Editor {
internal static class IndividualRestoreTests {
 static void Check(bool value,string message){if(!value)throw new Exception("Individual restore: "+message);}
 static int Player(GameDatabase db,int id){return db.Rows("players").FindIndex(p=>GameDatabase.Number(p,"playerid")==id);}
 static int Name(GameDatabase db,int id){return db.Rows("playernames").FindIndex(p=>GameDatabase.Number(p,"playerid")==id);}
 static Asset Kit(Session session,int number){return session.Assets.Single(a=>a.Name=="data/sceneassets/kit/kit_11_"+number+"_0.rx2");}
 static void Save(Session session,string history){var changes=session.Collect();Originals.CheckSave(session.Root,history,changes.Keys);Storage.SaveFiles(session.Root,changes,history);}
 internal static IEnumerable<string> Run(string project,string output){
  var results=new List<string>();string root=Path.Combine(output,"individual-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  var live=new Session(project);string source=Originals.SourceArchive(live.DbAsset,live.Root,Path.Combine(project,"modding","backups"));
  File.Copy(source,Path.Combine(root,"data0.big"));File.Copy(Path.ChangeExtension(source,".bh"),Path.Combine(root,"data0.bh"));
  byte[] originalKit=File.ReadAllBytes(Path.Combine(project,"analysis","modding-research","samples","kit_11_0_0.rx2"));
  SelfTest.MakeArchive(Path.Combine(root,"data3.big"),new Dictionary<string,byte[]>{{"data/sceneassets/kit/kit_11_0_0.rx2",originalKit},{"data/sceneassets/kit/kit_11_1_0.rx2",originalKit}});
  string pristine=Path.Combine(output,"original-kit-"+Guid.NewGuid().ToString("N")+".big");File.Copy(Path.Combine(root,"data3.big"),pristine);
  var expected=Directory.GetFiles(root).ToDictionary(Path.GetFileName,Bytes.Hash);
  string oldBig=Originals.Known["data3.big"],oldBh=Originals.Known["data3.bh"],history=Path.Combine(root,"backups");
  // Tiny synthetic texture archives let us exercise the production Session and
  // verified-history paths without copying or modifying the user's 1 GB BIG.
  try {
   Originals.Known["data3.big"]=expected["data3.big"];Originals.Known["data3.bh"]=expected["data3.bh"];
   var session=new Session(root);int scholes=Player(session.Db,246),ronaldo=Player(session.Db,20801);
   long ronaldoOriginal=GameDatabase.Number(session.Db.Rows("players")[ronaldo],"overallrating");
   long scholesOriginal=GameDatabase.Number(session.Db.Rows("players")[scholes],"overallrating");
   string nameOriginal=Convert.ToString(session.Db.Rows("playernames")[Name(session.Db,246)]["firstname"]);
   string ronaldoNameOriginal=Convert.ToString(session.Db.Rows("playernames")[Name(session.Db,20801)]["firstname"]);
   var kitA=(byte[])originalKit.Clone();kitA[kitA.Length-1]^=1;var kitB=(byte[])originalKit.Clone();kitB[kitB.Length-2]^=2;
   session.Db.SetNumber("players",scholes,"overallrating",93);session.Db.SetName(Name(session.Db,246),"firstname","Nombre editado");session.Db.SetNumber("players",ronaldo,"overallrating",94);session.Db.SetName(Name(session.Db,20801),"firstname","Otro jugador editado");session.DbDirty=true;
   session.Pending[Kit(session,0)]=kitA;session.Pending[Kit(session,1)]=kitB;Save(session,history);
   session=new Session(root);session.Db.SetNumber("players",Player(session.Db,20801),"overallrating",95);session.Db.SetName(Name(session.Db,20801),"firstname","Otro nombre pendiente");session.DbDirty=true;
   var kitB2=(byte[])kitB.Clone();kitB2[kitB2.Length-3]^=4;session.Pending[Kit(session,1)]=kitB2;
   session.RestoreResource(Kit(session,0),history);session.RestorePlayer(246,history);
   Check(session.Pending.Count==2&&session.Read(Kit(session,0)).SequenceEqual(originalKit)&&session.Read(Kit(session,1)).SequenceEqual(kitB2),"restore one kit preserves pending edit on another kit");
   Check(GameDatabase.Number(session.Db.Rows("players")[Player(session.Db,20801)],"overallrating")==95,"other player pending attribute preserved");
   Save(session,history);session=new Session(root);
   Check(session.Read(Kit(session,0)).SequenceEqual(originalKit)&&session.Read(Kit(session,1)).SequenceEqual(kitB2),"one kit restored after reopen; other kit still edited");
   Check(GameDatabase.Number(session.Db.Rows("players")[Player(session.Db,246)],"overallrating")==scholesOriginal&&Convert.ToString(session.Db.Rows("playernames")[Name(session.Db,246)]["firstname"])==nameOriginal,"selected player original attribute and name restored");
   Check(GameDatabase.Number(session.Db.Rows("players")[Player(session.Db,20801)],"overallrating")==95,"other player persisted");
   Check(Convert.ToString(session.Db.Rows("playernames")[Name(session.Db,20801)]["firstname"])=="Otro nombre pendiente","other player name persisted while restoring one player");
   session.Pending[Kit(session,0)]=kitA;session.RestoreResource(Kit(session,0),history);Check(session.Pending.Count==0,"unsaved import on original undone without a disk write");
   results.Add("Restauracion individual: camiseta A y jugador Scholes recuperan originales tras guardar/reabrir; camiseta B y Ronaldo conservan ediciones guardadas y pendientes. Nombres/CRC validos; importacion pendiente se deshace sin escribir.");

   var target=Kit(session,1);string verified=Originals.SourceArchive(target,root,history);string json=Path.Combine(Path.GetDirectoryName(verified),"respaldo.json");
   string originalManifest=File.ReadAllText(json);var serializer=new JavaScriptSerializer();var manifest=serializer.Deserialize<BackupManifest>(originalManifest);
   var currentHashes=Directory.GetFiles(root).ToDictionary(p=>p,Bytes.Hash);
   try {
    manifest.GameRoot=Path.Combine(root,"other-installation");File.WriteAllText(json,serializer.Serialize(manifest),Encoding.UTF8);
    bool refused=false;try{session.RestoreResource(target,history);}catch(IOException){refused=true;}Check(refused&&session.Pending.Count==0,"foreign installation backup rejected");
   } finally {File.WriteAllText(json,originalManifest,Encoding.UTF8);}
   try {
    using(var f=new FileStream(verified,FileMode.Open,FileAccess.ReadWrite)){f.Position=f.Length-1;int value=f.ReadByte();f.Position--;f.WriteByte((byte)(value^1));}
    bool refused=false;try{session.RestoreResource(target,history);}catch(IOException){refused=true;}Check(refused&&session.Pending.Count==0,"tampered original backup rejected");
   } finally {File.Copy(pristine,verified,true);}
   foreach(var hash in currentHashes)Check(Bytes.Hash(hash.Key)==hash.Value,"failed restore leaves active test files unchanged");
   results.Add("Restauracion protegida: respaldos alterados o de otra instalacion se rechazan sin preparar ni escribir cambios; nunca toma una version editada como original.");
   session.RestoreResource(target,history);session.RestorePlayer(20801,history);Save(session,history);session=new Session(root);
   Check(session.Read(Kit(session,1)).SequenceEqual(originalKit),"second individual kit restore works");
   Check(GameDatabase.Number(session.Db.Rows("players")[Player(session.Db,20801)],"overallrating")==ronaldoOriginal,"other player restore uses the verified original");
   Check(Convert.ToString(session.Db.Rows("playernames")[Name(session.Db,20801)]["firstname"])==ronaldoNameOriginal,"second player name restores independently");
  } finally {Originals.Known["data3.big"]=oldBig;Originals.Known["data3.bh"]=oldBh;}
  string preference=Path.Combine(root,"preferences","theme.txt");Check(ThemePreference.Read(preference),"dark by default");ThemePreference.Write(preference,false);Check(!ThemePreference.Read(preference),"light preference saved");ThemePreference.Write(preference,true);Check(ThemePreference.Read(preference),"dark preference saved");
  results.Add("Tema: oscuro predeterminado y preferencia claro/oscuro conservada; pruebas usan configuracion temporal y no cambian la preferencia del usuario.");
  return results;
 }
}
}
