using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pr7Editor {
public static class GrowthTests {
 static void Check(bool condition,string description){if(!condition)throw new Exception("Growth regression: "+description);}
 static byte[] Stored(Archive a,Asset e){using(var stream=File.OpenRead(a.FileName))return Bytes.Read(stream,e.Offset,e.Size);}
 static void Verify(Archive before,Archive after,byte[] oldBh,string bhPath,ISet<string> edited){
  byte[] bh=File.ReadAllBytes(bhPath);
  Check(BitConverter.ToUInt32(after.Header,4)==new FileInfo(after.FileName).Length,"BIG file size");
  Check(before.Entries.Count==after.Entries.Count,"entry count");
  foreach(var original in before.Entries){var current=after.Entries[original.Index];int index=16+original.Index*20;
   Check(current.Name==original.Name&&current.DirectoryOffset==original.DirectoryOffset,"directory order");
   Check(Bytes.UInt(bh,index)==current.Offset&&Bytes.UInt(bh,index+4)==current.Size,"BH offsets and sizes");
   Check(Bytes.Slice(oldBh,index+12,8).SequenceEqual(Bytes.Slice(bh,index+12,8)),"BH hashes");
   if(!edited.Contains(original.Name)){Check(current.Offset==original.Offset&&current.Size==original.Size,"untouched location");Check(Stored(before,original).SequenceEqual(Stored(after,current)),"untouched stored bytes");Check(Bytes.Slice(oldBh,index,20).SequenceEqual(Bytes.Slice(bh,index,20)),"untouched BH entry");}
  }
 }
 public static IEnumerable<string> Run(string project,string output){
  var results=new List<string>();string work=Path.Combine(output,"growth-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
  string path=Path.Combine(work,"data0.big");SelfTest.MakeArchive(path,new Dictionary<string,byte[]>{{"first",new byte[128]},{"second",new byte[128]},{"neighbor",new byte[]{7,8,9,10}}});
  string pristine=Path.Combine(work,"pristine.big");File.Copy(path,pristine);var before=new Archive(pristine);byte[] bh=File.ReadAllBytes(Path.ChangeExtension(path,".bh"));var hashes=Directory.GetFiles(work).ToDictionary(p=>p,p=>Bytes.Hash(p));
  var random=new Random(246);byte[] first=new byte[4096],second=new byte[8192];random.NextBytes(first);random.NextBytes(second);var archive=new Archive(path);
  string backup=Storage.SaveFiles(work,new Dictionary<Asset,byte[]>{{archive.Find("first"),first},{archive.Find("second"),second}},Path.Combine(work,"backups"));
  var after=new Archive(path);Verify(before,after,bh,Path.ChangeExtension(path,".bh"),new HashSet<string>{"first","second"});
  Check(after.Read(after.Find("first")).SequenceEqual(first)&&after.Read(after.Find("second")).SequenceEqual(second),"incompressible replacements");
  Check(after.Find("first").Offset>=new FileInfo(pristine).Length&&after.Find("first").Offset%64==0,"aligned append");
  Check(after.Find("second").Offset>=(long)after.Find("first").Offset+after.Find("first").Size,"multiple appends do not overlap");
  Check(Bytes.UInt(File.ReadAllBytes(Path.ChangeExtension(path,".bh")),24)==0,"raw BH expanded-size sentinel");
  Storage.Restore(Path.Combine(backup,"respaldo.json"),work);foreach(var item in hashes)Check(Bytes.Hash(item.Key)==item.Value,"synthetic byte-exact restore");
  results.Add("Crecimiento BIG/BH: dos recursos incomprimibles mayores que sus espacios; offsets alineados, hashes/vecino intactos, modo sin compresion y restauracion byte exacta.");

  string game=Session.Resolve(project);var liveArchive=new Archive(Path.Combine(game,"data0.big"));string actual=Originals.SourceArchive(liveArchive.Find("data/db/fifa_ng_db.db"),game,Path.Combine(project,"modding","backups")),caseRoot=Path.Combine(work,"scholes");Directory.CreateDirectory(caseRoot);
  path=Path.Combine(caseRoot,"data0.big");File.Copy(actual,path);File.Copy(Path.ChangeExtension(actual,".bh"),Path.ChangeExtension(path,".bh"));
  pristine=Path.Combine(work,"original-data0.big");File.Copy(actual,pristine);before=new Archive(pristine);bh=File.ReadAllBytes(Path.ChangeExtension(actual,".bh"));
  var expected=new Dictionary<string,string>{{"data0.big",Bytes.Hash(path)},{"data0.bh",Bytes.Hash(Path.ChangeExtension(path,".bh"))}};
  archive=new Archive(path);var entry=archive.Find("data/db/fifa_ng_db.db");byte[] meta=archive.Read(archive.Find("data/db/fifa_ng_db-meta.xml"));var db=new GameDatabase(archive.Read(entry),meta);
  int player=db.Rows("players").FindIndex(r=>GameDatabase.Number(r,"playerid")==246);Check(player>=0,"Scholes present");
  db.SetNumber("players",player,"shoetypecode",116);db.SetNumber("players",player,"shoecolorcode1",0);db.SetNumber("players",player,"jerseystylecode",0);
  byte[] edited=db.Serialize();int compressed=RefPackEncoder.Pack(edited).Length;long space=archive.Entries.Where(e=>e.Offset>entry.Offset).Select(e=>(long)e.Offset).DefaultIfEmpty(new FileInfo(path).Length).Min()-entry.Offset;
  var changes=new Dictionary<Asset,byte[]>{{entry,edited}};string history=Path.Combine(caseRoot,"backups");Originals.CheckSave(caseRoot,history,changes.Keys);Storage.Save(caseRoot,changes,history);
  after=new Archive(path);Verify(before,after,bh,Path.ChangeExtension(path,".bh"),new HashSet<string>{entry.Name});
  var checkDb=new GameDatabase(after.Read(after.Find(entry.Name)),meta);var row=checkDb.Rows("players")[player];
  Check(GameDatabase.Number(row,"shoetypecode")==116&&GameDatabase.Number(row,"shoecolorcode1")==0&&GameDatabase.Number(row,"jerseystylecode")==0,"Scholes settings persist after reopen");
  if(compressed>space)Check(after.Find(entry.Name).Offset>=new FileInfo(pristine).Length,"real DB relocation");
  checkDb.SetNumber("players",player,"jerseystylecode",1);checkDb.SetNumber("players",player,"overallrating",93);
  changes=new Dictionary<Asset,byte[]>{{after.Find(entry.Name),checkDb.Serialize()}};Originals.CheckSave(caseRoot,history,changes.Keys);Storage.Save(caseRoot,changes,history);
  var twice=new Archive(path);checkDb=new GameDatabase(twice.Read(twice.Find(entry.Name)),meta);Check(GameDatabase.Number(checkDb.Rows("players")[player],"jerseystylecode")==1&&GameDatabase.Number(checkDb.Rows("players")[player],"overallrating")==93,"second save persists");
  Verify(before,twice,bh,Path.ChangeExtension(path,".bh"),new HashSet<string>{entry.Name});
  Originals.RestoreSet(caseRoot,history,expected);foreach(var item in expected)Check(Bytes.Hash(Path.Combine(caseRoot,item.Key))==item.Value,"real original restore");
  results.Add("Regresion Scholes en copia del data0 real: botin116/color0 + camiseta dentro, reabrir, segundo guardado de camiseta/media y restauracion original. Comprimido="+compressed+"; espacio anterior="+space+". Ninguna escritura al juego activo.");
  return results;
 }
}
}
