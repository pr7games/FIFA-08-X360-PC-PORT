using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Pr7Editor {
public static class Bytes {
 public static int U32(byte[] b,int p) { Check(b,p,4);uint v=((uint)b[p]<<24)|((uint)b[p+1]<<16)|((uint)b[p+2]<<8)|b[p+3];return checked((int)v); }
 public static uint UInt(byte[] b,int p) { Check(b,p,4);return ((uint)b[p]<<24)|((uint)b[p+1]<<16)|((uint)b[p+2]<<8)|b[p+3]; }
 public static int U16(byte[] b,int p) { Check(b,p,2);return (b[p]<<8)|b[p+1]; }
 public static void BE(byte[] b,int p,int v) { BE(b,p,unchecked((uint)v)); }
 public static void BE(byte[] b,int p,uint v) { Check(b,p,4);b[p]=(byte)(v>>24);b[p+1]=(byte)(v>>16);b[p+2]=(byte)(v>>8);b[p+3]=(byte)v; }
 public static void Check(byte[] b,int p,int n) { if(p<0||n<0||p>b.Length-n)throw new InvalidDataException("Archivo truncado o rango invalido."); }
 public static int Align(int n,int a) { return checked((n+a-1)/a*a); }
 public static byte[] Slice(byte[] b,int p,int n) { Check(b,p,n);byte[] r=new byte[n];Buffer.BlockCopy(b,p,r,0,n);return r; }
 public static byte[] Read(FileStream f,long p,int n) { if(p<0||n<0||p>f.Length-n)throw new InvalidDataException("Entrada fuera del archivo.");byte[] b=new byte[n];f.Position=p;int k=0;while(k<n){int r=f.Read(b,k,n-k);if(r==0)throw new EndOfStreamException();k+=r;}return b; }
 public static string Hash(string path) { using(var f=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(f)).Replace("-",""); }
 public static byte[] Inflate(byte[] b) {
  if(b.Length<2||b[0]!=0x10||b[1]!=0xfb)return b;
  Check(b,0,5);int size=(b[2]<<16)|(b[3]<<8)|b[4];byte[] r=new byte[size];int p=5,o=0;
  while(true){Check(b,p,1);int a=b[p++],lit=0,dist=0,n=0;
   if(a<0x80){Check(b,p,1);int c=b[p++];lit=a&3;dist=((a&0x60)<<3)+c+1;n=((a>>2)&7)+3;}
   else if(a<0xc0){Check(b,p,2);int c=b[p++],d=b[p++];lit=c>>6;dist=((c&63)<<8)+d+1;n=(a&63)+4;}
   else if(a<0xe0){Check(b,p,3);int c=b[p++],d=b[p++],e=b[p++];lit=a&3;dist=((a&16)<<12)+(c<<8)+d+1;n=((a&12)<<6)+e+5;}
   else if(a<0xfc)lit=((a&31)<<2)+4;else lit=a&3;
   Check(b,p,lit);Check(r,o,lit);Buffer.BlockCopy(b,p,r,o,lit);p+=lit;o+=lit;
   if(n>0){if(dist>o)throw new InvalidDataException("RefPack: distancia invalida.");Check(r,o,n);for(int i=0;i<n;i++){r[o]=r[o-dist];o++;}}
   if(a>=0xfc)break;
  }
  if(o!=size)throw new InvalidDataException("RefPack: tamano incorrecto.");return r;
 }
 public static byte[] LiteralPack(byte[] b) {
  if(b.Length>0xffffff)throw new InvalidDataException("Recurso demasiado grande para RefPack 24-bit.");
  using(var m=new MemoryStream()){m.WriteByte(0x10);m.WriteByte(0xfb);m.WriteByte((byte)(b.Length>>16));m.WriteByte((byte)(b.Length>>8));m.WriteByte((byte)b.Length);int p=0;
   while(b.Length-p>=4){int n=Math.Min(112,(b.Length-p)&~3);m.WriteByte((byte)(0xe0+(n-4)/4));m.Write(b,p,n);p+=n;}
   m.WriteByte((byte)(0xfc+b.Length-p));m.Write(b,p,b.Length-p);return m.ToArray();}
 }
}
public sealed class Asset {
 public string Name; public int Offset,Size,Index,DirectoryOffset; public Archive Archive;
 public override string ToString(){return Name;}
}
public sealed class Archive {
 public string FileName; public List<Asset> Entries=new List<Asset>();public byte[] Header;readonly long OriginalLength;readonly DateTime OriginalWrite;
 public Archive(string path) {
  FileName=Path.GetFullPath(path);OriginalLength=new FileInfo(FileName).Length;OriginalWrite=File.GetLastWriteTimeUtc(FileName);using(var f=File.OpenRead(path)){var h=Bytes.Read(f,0,16);string magic=Encoding.ASCII.GetString(h,0,4);
   if(magic!="BIG4"&&magic!="BIGF")throw new InvalidDataException("Se esperaba un archivo BIG4.");
   int count=Bytes.U32(h,8),end=Bytes.U32(h,12);if(count>100000||end>32*1024*1024||end<16)throw new InvalidDataException("Directorio BIG invalido.");
   Header=Bytes.Read(f,0,end);int p=16;for(int i=0;i<count;i++){Bytes.Check(Header,p,9);int z=Array.IndexOf(Header,(byte)0,p+8);if(z<0)throw new InvalidDataException("Nombre BIG sin terminador.");
    var e=new Asset{Archive=this,Index=i,DirectoryOffset=p,Offset=Bytes.U32(Header,p),Size=Bytes.U32(Header,p+4),Name=Encoding.UTF8.GetString(Header,p+8,z-p-8)};
    if(e.Offset<f.Position && e.Offset<end)throw new InvalidDataException("Entrada superpuesta al directorio.");if((long)e.Offset+e.Size>f.Length)throw new InvalidDataException("Entrada BIG truncada.");Entries.Add(e);p=z+1;}
  }
 }
 public byte[] Read(Asset e){using(var f=File.OpenRead(FileName))return Bytes.Inflate(Bytes.Read(f,e.Offset,e.Size));}
 public Asset Find(string name){return Entries.FirstOrDefault(e=>String.Equals(e.Name,name,StringComparison.OrdinalIgnoreCase));}
 public void Stage(string bigOutput,string bhOutput,Dictionary<string,byte[]> changes) {
  if(new FileInfo(FileName).Length!=OriginalLength||File.GetLastWriteTimeUtc(FileName)!=OriginalWrite)throw new IOException("El BIG cambio desde que se abrio el editor. Vuelve a cargarlo antes de guardar.");
  string bhPath=Path.ChangeExtension(FileName,".bh");byte[] bh=File.ReadAllBytes(bhPath),h=(byte[])Header.Clone();
  if(bh.Length<16+Entries.Count*20||Encoding.ASCII.GetString(bh,0,4)!="ViV4"||Bytes.U32(bh,8)!=Entries.Count)throw new InvalidDataException("Indice BH incompatible.");
  foreach(var e in Entries){int p=16+e.Index*20;if(Bytes.U32(bh,p)!=e.Offset||Bytes.U32(bh,p+4)!=e.Size)throw new InvalidDataException("BIG/BH no coinciden: "+e.Name);}
  foreach(string name in changes.Keys)if(Find(name)==null)throw new InvalidDataException("Recurso inexistente: "+name);
  // Keep untouched resources at their original addresses. A larger replacement
  // is appended at a 64-byte boundary and BOTH directories point to its new slot.
  // Compression size is data-dependent, even for a one-field database edit.
  var packedChanges=new Dictionary<Asset,byte[]>();var locations=new Dictionary<Asset,int>();long outputLength=OriginalLength;
  foreach(var e in Entries){byte[] replacement;if(!changes.TryGetValue(e.Name,out replacement))continue;
   byte[] packed=replacement.Length<=0xffffff?RefPackEncoder.Pack(replacement):replacement;
   // ViV4 uses expanded size zero for uncompressed entries. Avoid treating an
   // uncompressed 10FB prefix as a compressed resource when reading it back.
   if(packed.Length>=replacement.Length && !(replacement.Length>=2&&replacement[0]==0x10&&replacement[1]==0xfb))packed=replacement;
   if(!Bytes.Inflate(packed).SequenceEqual(replacement))throw new InvalidDataException("Compresion RefPack no verificable.");packedChanges.Add(e,packed);
   long next=Entries.Where(x=>x.Offset>e.Offset).Select(x=>(long)x.Offset).DefaultIfEmpty(OriginalLength).Min();int offset=e.Offset;
   bool shared=Entries.Any(x=>x!=e&&x.Size>0&&x.Offset<(long)e.Offset+packed.Length&&(long)x.Offset+x.Size>e.Offset);
   if(packed.Length>next-e.Offset||shared){long start=checked((outputLength+63)/64*64),end=checked(start+packed.Length);if(end>Int32.MaxValue)throw new InvalidDataException("El BIG editado supera el limite de 2 GiB del editor.");offset=(int)start;outputLength=end;}
   locations.Add(e,offset);
   Bytes.BE(h,e.DirectoryOffset,offset);Bytes.BE(h,e.DirectoryOffset+4,packed.Length);int index=16+e.Index*20;Bytes.BE(bh,index,offset);Bytes.BE(bh,index+4,packed.Length);Bytes.BE(bh,index+8,Object.ReferenceEquals(packed,replacement)?0:replacement.Length);
  }
  Buffer.BlockCopy(BitConverter.GetBytes(checked((int)outputLength)),0,h,4,4); // BIG4 total size is little-endian.
  File.Copy(FileName,bigOutput,false);
  using(var f=new FileStream(bigOutput,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){
   foreach(var item in packedChanges){f.Position=locations[item.Key];f.Write(item.Value,0,item.Value.Length);}
   f.Position=0;f.Write(h,0,h.Length);f.Flush(true);
  }
  File.WriteAllBytes(bhOutput,bh);var test=new Archive(bigOutput);
  foreach(var entry in test.Entries){int index=16+entry.Index*20;if(Bytes.U32(bh,index)!=entry.Offset||Bytes.U32(bh,index+4)!=entry.Size)throw new InvalidDataException("Fallo al verificar el indice BH reconstruido.");}
  foreach(var pair in changes)if(!test.Read(test.Find(pair.Key)).SequenceEqual(pair.Value))throw new InvalidDataException("Fallo al verificar el BIG reconstruido.");
 }
}
public sealed class BackupFile { public string Original,Backup,BeforeHash,AfterHash; }
public sealed class BackupManifest { public string GameRoot,State;public List<BackupFile> Files=new List<BackupFile>(); }
public static class Storage {
 public static string Save(string gameRoot,Dictionary<Asset,byte[]> changes,string backupRoot) {
  if(changes.Count==0)return null;CheckGameClosed();Originals.CheckSave(gameRoot,backupRoot,changes.Keys);
  return SaveFiles(gameRoot,changes,backupRoot);
 }
 // The UI always uses Save above. Tests exercise the same file transaction on
 // isolated fixture directories without interrupting a running real game.
 internal static string SaveFiles(string gameRoot,Dictionary<Asset,byte[]> changes,string backupRoot) {
  return OriginalBackupStore.Save(gameRoot,changes,backupRoot);
 }
 public static void Restore(string json,string gameRoot) {
  var m=new JavaScriptSerializer().Deserialize<BackupManifest>(File.ReadAllText(json));RestorePlan(m,gameRoot,Path.GetDirectoryName(Path.GetFullPath(json)));
 }
 public static void RestorePlan(BackupManifest m,string gameRoot,string backupRoot) {
  CheckGameClosed();string root=Path.GetFullPath(gameRoot).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;string backupBoundary=Path.GetFullPath(backupRoot).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  if(!String.Equals(Path.GetFullPath(m.GameRoot).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,root,StringComparison.OrdinalIgnoreCase))throw new IOException("Este respaldo pertenece a otra carpeta del juego.");
  if(m.Files.Count==0||m.Files.Select(f=>f.Original).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=m.Files.Count)throw new IOException("Lista de respaldo invalida.");
  foreach(var f in m.Files){if(!Path.GetFullPath(f.Original).StartsWith(root,StringComparison.OrdinalIgnoreCase)||!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(f.Original),@"^data[0-6]\.(big|bh)$",System.Text.RegularExpressions.RegexOptions.IgnoreCase)||!Path.GetFullPath(f.Backup).StartsWith(backupBoundary,StringComparison.OrdinalIgnoreCase))throw new IOException("Rutas de respaldo invalidas.");if(!String.Equals(Bytes.Hash(f.Backup),f.BeforeHash,StringComparison.OrdinalIgnoreCase))throw new IOException("Respaldo alterado.");}
  var staged=new Dictionary<BackupFile,string>();var undo=new Dictionary<BackupFile,string>();try{
   foreach(var f in m.Files){string temp=f.Original+".restore-"+Guid.NewGuid().ToString("N");staged.Add(f,temp);File.Copy(f.Backup,temp);if(!String.Equals(Bytes.Hash(temp),f.BeforeHash,StringComparison.OrdinalIgnoreCase))throw new IOException("Copia de restauracion invalida.");}
   foreach(var f in m.Files){string previous=f.Original+".undo-"+Guid.NewGuid().ToString("N");File.Replace(staged[f],f.Original,previous);undo.Add(f,previous);}
  }catch{foreach(var item in undo)File.Replace(item.Value,item.Key.Original,null);throw;}
  finally{foreach(var path in staged.Values.Concat(undo.Values))if(File.Exists(path))File.Delete(path);}
 }
 public static void CheckGameClosed(){if(System.Diagnostics.Process.GetProcessesByName("fifa08").Length>0)throw new IOException("Cierra FIFA 08 antes de guardar o restaurar archivos.");}
}
}
