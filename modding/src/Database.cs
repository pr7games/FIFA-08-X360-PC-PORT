using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Pr7Editor {
public sealed class DbField { public string Name,Short;public int Type,Bit,Depth,Low,High; }
public sealed class DbTable {
 public string Name,Short;public int Offset,RecordSize,Count,RecordOffset,Span;public List<DbField> Fields=new List<DbField>();public List<Dictionary<string,object>> Rows;
 public bool Dirty,NamesDirty;
}
public sealed class GameDatabase {
 public byte[] Data;public Dictionary<string,DbTable> Tables=new Dictionary<string,DbTable>();public List<DbTable> Ordered=new List<DbTable>();
 static readonly uint[] CrcTable=MakeCrc();
 static uint[] MakeCrc(){uint[] r=new uint[256];for(int i=0;i<256;i++){uint c=(uint)i<<24;for(int j=0;j<8;j++)c=(c<<1)^((c&0x80000000)!=0?0x04c11db7u:0);r[i]=c;}return r;}
 public static uint Crc(byte[] b,int start,int length){Bytes.Check(b,start,length);uint c=0xffffffff;for(int i=start;i<start+length;i++)c=(c<<8)^CrcTable[((c>>24)^b[i])&255];return c;}
 public static long ReadBits(byte[] b,int row,DbField f){long v=0;for(int j=0;j<f.Depth;j++){int bit=f.Bit+j;v=(v<<1)+((b[row+bit/8]>>(7-bit%8))&1);}return v+f.Low;}
 public static void WriteBits(byte[] b,int row,DbField f,long value){if(value<f.Low||value>f.High)throw new ArgumentException(f.Name+": rango "+f.Low+".."+f.High);long v=value-f.Low;if(f.Depth<63&&v>=(1L<<f.Depth))throw new ArgumentException("Valor fuera del campo.");for(int j=0;j<f.Depth;j++){int bit=f.Bit+j,mask=1<<(7-bit%8);b[row+bit/8]=(byte)((b[row+bit/8]&~mask)|((((v>>(f.Depth-1-j))&1)!=0)?mask:0));}}
 public GameDatabase(byte[] data,byte[] meta) {
  Data=(byte[])data.Clone();if(Data.Length<28||Data[0]!=68||Data[1]!=66||Data[2]!=0||Data[3]!=8||BitConverter.ToInt32(Data,4)!=1)throw new InvalidDataException("Se requiere FIFA DB version 8 Xbox big-endian.");
  var xml=new XmlDocument();xml.XmlResolver=null;using(var m=new MemoryStream(meta))using(var r=XmlReader.Create(m,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))xml.Load(r);
  var defs=new Dictionary<string,XmlElement>();foreach(XmlElement e in xml.SelectNodes("/database/table"))defs.Add(e.GetAttribute("shortname"),e);
  int n=Bytes.U32(Data,16);if(n>1000)throw new InvalidDataException("Demasiadas tablas.");int first=checked(24+n*8+4);
  if(Bytes.UInt(Data,20)!=Crc(Data,0,20)||Bytes.UInt(Data,first-4)!=Crc(Data,24,n*8))throw new InvalidDataException("CRC de la base invalido.");
  for(int i=0;i<n;i++) {
   string s=Encoding.ASCII.GetString(Data,24+i*8,4);if(!defs.ContainsKey(s))throw new InvalidDataException("Esquema no coincide: "+s);XmlElement def=defs[s];int p=checked(first+Bytes.U32(Data,28+i*8));
   var t=new DbTable{Name=def.GetAttribute("name"),Short=s,Offset=p,RecordSize=Bytes.U32(Data,p+4),Count=Bytes.U16(Data,p+18)};int nf=Data[p+24];t.RecordOffset=p+36+nf*16;
   var fields=new Dictionary<string,XmlElement>();foreach(XmlElement f in def.SelectNodes("fields/field"))fields.Add(f.GetAttribute("shortname"),f);
   for(int j=0;j<nf;j++){int q=p+36+j*16;string sn=Encoding.ASCII.GetString(Data,q+8,4);XmlElement x;if(!fields.TryGetValue(sn,out x))throw new InvalidDataException("Campo sin metadatos.");
    var f=new DbField{Name=x.GetAttribute("name"),Short=sn,Type=Bytes.U32(Data,q),Bit=Bytes.U32(Data,q+4),Depth=Bytes.U32(Data,q+12),Low=Int32.Parse(x.GetAttribute("rangelow")),High=Int32.Parse(x.GetAttribute("rangehigh"))};
    if(f.Bit+(f.Type==13||f.Type==14?32:f.Depth)>t.RecordSize*8)throw new InvalidDataException("Campo fuera del registro.");t.Fields.Add(f);
   }
   int end=checked(t.RecordOffset+t.Count*t.RecordSize+Bytes.Align(Bytes.U32(Data,p+12),8));Bytes.Check(Data,end,4);t.Span=end+4-p;
   if(Bytes.UInt(Data,p+32)!=Crc(Data,p,32)||Bytes.UInt(Data,end)!=Crc(Data,p+36,end-p-36))throw new InvalidDataException("CRC invalido en "+t.Name);
   Tables.Add(t.Name,t);Ordered.Add(t);
  }
 }
 public List<Dictionary<string,object>> Rows(string name){var t=Tables[name];if(t.Rows!=null)return t.Rows;t.Rows=new List<Dictionary<string,object>>();var cf=t.Fields.Where(f=>f.Type==13||f.Type==14).ToArray();int min=Int32.MaxValue;
  for(int i=0;i<t.Count;i++){int p=t.RecordOffset+i*t.RecordSize;var row=new Dictionary<string,object>();foreach(var f in t.Fields){object v=null;
   if(f.Type==3||f.Type==2)v=ReadBits(Data,p,f);else if(f.Type==0)v=Encoding.UTF8.GetString(Data,p+f.Bit/8,f.Depth/8).TrimEnd('\0');
   else if(f.Type==4){byte[] b=Bytes.Slice(Data,p+f.Bit/8,4);Array.Reverse(b);v=BitConverter.ToSingle(b,0);}else if(f.Type==13||f.Type==14){int a=unchecked((int)Bytes.UInt(Data,p+f.Bit/8));v=a;if(a>=0)min=Math.Min(min,a);}
   row[f.Name]=v;
  }t.Rows.Add(row);}
  if(cf.Length>0){int start=t.RecordOffset+t.Count*t.RecordSize,nodes=min==Int32.MaxValue?0:min/4;foreach(var row in t.Rows)foreach(var f in cf){int off=(int)row[f.Name];if(off<0){row[f.Name]="";continue;}int p=start+off;int len=f.Type==13?Data[p++]:Bytes.U16(Data,p);if(f.Type==14)p+=2;
    var text=new List<byte>();if(nodes==0){text.AddRange(Bytes.Slice(Data,p,len));}else{int bit=0,node=0;while(text.Count<len){if(bit>len*64+64)throw new InvalidDataException("Huffman invalido.");Bytes.Check(Data,p+bit/8,1);int choice=(Data[p+bit/8]>>(7-bit%8))&1;bit++;int q=start+node*4+choice*2;Bytes.Check(Data,q,2);int child=Data[q];if(child>0){node=child;if(node>=nodes)throw new InvalidDataException("Nodo Huffman invalido.");}else{text.Add(Data[q+1]);node=0;}}}
    row[f.Name]=Encoding.UTF8.GetString(text.ToArray());
   }}return t.Rows;
 }
 public void SetNumber(string table,int row,string field,long value){var t=Tables[table];var f=t.Fields.Single(x=>x.Name==field);if(f.Type!=3&&f.Type!=2)throw new ArgumentException("Este campo no es entero.");Rows(table);WriteBits(Data,t.RecordOffset+row*t.RecordSize,f,value);t.Rows[row][field]=value;t.Dirty=true;}
 public void SetName(int row,string field,string text){var t=Tables["playernames"];var f=t.Fields.Single(x=>x.Name==field);if(Encoding.UTF8.GetByteCount(text)>Math.Min(255,f.Depth/8))throw new ArgumentException("Nombre demasiado largo.");Rows("playernames")[row][field]=text;t.Dirty=t.NamesDirty=true;}
 sealed class Node { public Node Left,Right;public int Symbol=-1,Frequency,Index; }
 static void Codes(Node n,List<int> bits,Dictionary<byte,int[]> codes){if(n.Symbol>=0){codes[(byte)n.Symbol]=bits.ToArray();return;}bits.Add(0);Codes(n.Left,bits,codes);bits[bits.Count-1]=1;Codes(n.Right,bits,codes);bits.RemoveAt(bits.Count-1);}
 byte[] BuildNames(DbTable t) {
  var rows=Rows(t.Name);var fs=t.Fields.Where(f=>f.Type==13||f.Type==14).ToArray();var texts=rows.SelectMany(r=>fs.Select(f=>(string)r[f.Name])).Distinct().ToArray();var frequency=new int[256];foreach(string s in texts)foreach(byte c in Encoding.UTF8.GetBytes(s))frequency[c]++;
  var nodes=new List<Node>();for(int i=0;i<256;i++)if(frequency[i]>0)nodes.Add(new Node{Symbol=i,Frequency=frequency[i]});if(nodes.Count==0)nodes.Add(new Node{Symbol=0,Frequency=1});if(nodes.Count==1)nodes.Add(new Node{Symbol=nodes[0].Symbol==0?1:0,Frequency=1});
  while(nodes.Count>1){nodes=nodes.OrderBy(n=>n.Frequency).ThenBy(n=>n.Symbol).ToList();var a=nodes[0];var b=nodes[1];nodes.RemoveRange(0,2);nodes.Add(new Node{Left=a,Right=b,Frequency=a.Frequency+b.Frequency});}
  Node tree=nodes[0];var internalNodes=new List<Node>{tree};for(int i=0;i<internalNodes.Count;i++){var n=internalNodes[i];n.Index=i;if(n.Left.Symbol<0)internalNodes.Add(n.Left);if(n.Right.Symbol<0)internalNodes.Add(n.Right);}if(internalNodes.Count>255)throw new InvalidDataException("Arbol Huffman demasiado grande.");
  var codes=new Dictionary<byte,int[]>();Codes(tree,new List<int>(),codes);var offsets=new Dictionary<string,int>();byte[] pool;
  using(var m=new MemoryStream()) {foreach(var n in internalNodes)foreach(var child in new[]{n.Left,n.Right}){m.WriteByte((byte)(child.Symbol<0?child.Index:0));m.WriteByte((byte)(child.Symbol<0?0:child.Symbol));}
   // This table uses type 13 throughout. Mixing 13/14 would need per-field prefixes.
   if(fs.Any(f=>f.Type!=13))throw new NotSupportedException("Variante de nombres no compatible.");
   foreach(string s in texts){offsets[s]=checked((int)m.Position);byte[] utf=Encoding.UTF8.GetBytes(s);if(utf.Length>255)throw new ArgumentException("Nombre demasiado largo.");m.WriteByte((byte)utf.Length);int current=0,count=0;foreach(byte c in utf)foreach(int bit in codes[c]){current=(current<<1)|bit;if(++count==8){m.WriteByte((byte)current);current=count=0;}}if(count>0)m.WriteByte((byte)(current<<(8-count)));}
   pool=m.ToArray();}
  int prefix=t.RecordOffset-t.Offset,records=checked(t.Count*t.RecordSize),crc=prefix+records+Bytes.Align(pool.Length,8);var output=new byte[crc+4];Buffer.BlockCopy(Data,t.Offset,output,0,prefix+records);Buffer.BlockCopy(pool,0,output,prefix+records,pool.Length);
  for(int i=0;i<rows.Count;i++)foreach(var f in fs)Bytes.BE(output,prefix+i*t.RecordSize+f.Bit/8,offsets[(string)rows[i][f.Name]]);
  Bytes.BE(output,12,pool.Length);Bytes.BE(output,32,Crc(output,0,32));Bytes.BE(output,crc,Crc(output,36,crc-36));return output;
 }
 public byte[] Serialize() {
  if(!Ordered.Any(t=>t.NamesDirty)){byte[] exact=(byte[])Data.Clone();foreach(var t in Ordered.Where(t=>t.Dirty))Bytes.BE(exact,t.Offset+t.Span-4,Crc(exact,t.Offset+36,t.Span-40));return exact;}
  int first=24+Ordered.Count*8+4;using(var m=new MemoryStream()){m.Write(Data,0,first);var directory=Bytes.Slice(Data,0,first);
   for(int i=0;i<Ordered.Count;i++){DbTable t=Ordered[i];while((m.Position-first)%4!=0)m.WriteByte(0);Bytes.BE(directory,28+i*8,checked((int)m.Position-first));byte[] block;
    if(t.NamesDirty)block=BuildNames(t);else{block=Bytes.Slice(Data,t.Offset,t.Span);if(t.Dirty)Bytes.BE(block,block.Length-4,Crc(block,36,block.Length-40));}
    m.Write(block,0,block.Length);
   }
   byte[] result=m.ToArray();Bytes.BE(directory,8,result.Length);Bytes.BE(directory,20,Crc(directory,0,20));Bytes.BE(directory,first-4,Crc(directory,24,Ordered.Count*8));Buffer.BlockCopy(directory,0,result,0,first);return result;
  }
 }
 public static long Number(Dictionary<string,object> row,string key){object o;return row.TryGetValue(key,out o)&&o!=null?Convert.ToInt64(o):0;}
}
}
