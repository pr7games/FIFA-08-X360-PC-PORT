using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
public static class Fifa08Languages {
 public sealed class Language {public string Code,Tag,Name;public int UserLanguage;public override string ToString(){return Name;}}
 public sealed class Catalog {public List<Language> Text=new List<Language>(),Commentary=new List<Language>();public string DefaultText,DefaultCommentary;}
 sealed class Entry {public int Index;public uint Offset,Size;}
 static uint Read(byte[] b,int p){if(p<0||p>b.Length-4)throw new InvalidDataException("Indice de idiomas truncado");return ((uint)b[p]<<24)|((uint)b[p+1]<<16)|((uint)b[p+2]<<8)|b[p+3];}
 static void Write(byte[] b,int p,uint v){for(int i=0;i<4;i++)b[p+i]=(byte)(v>>(24-8*i));}
 static Dictionary<string,Entry> Entries(byte[] b) {
  if(b.Length<16)throw new InvalidDataException("Archivo de idiomas incompleto");string magic=Encoding.ASCII.GetString(b,0,4);if(magic!="BIGF"&&magic!="BIG4")throw new InvalidDataException("Formato de idiomas desconocido");
  uint count=Read(b,8),end=Read(b,12);if(count>100000||end<16||end>b.Length)throw new InvalidDataException("Indice de idiomas invalido");
  var entries=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);int p=16;
  for(uint i=0;i<count;i++){if(p+8>=end)throw new InvalidDataException("Indice de idiomas truncado");int z=p+8;while(z<end&&b[z]!=0)z++;if(z==end)throw new InvalidDataException("Nombre de idioma truncado");var e=new Entry{Index=p,Offset=Read(b,p),Size=Read(b,p+4)};if(e.Offset<end||(ulong)e.Offset+e.Size>(ulong)b.Length)throw new InvalidDataException("Recurso de idioma fuera del archivo");entries.Add(Encoding.UTF8.GetString(b,p+8,z-p-8),e);p=z+1;}return entries;
 }
 static string TagFromName(string name){var m=Regex.Match(name,@"^data/loc/([a-z]{3}_[a-z]{2})\.db$",RegexOptions.IgnoreCase);return m.Success?m.Groups[1].Value.ToLowerInvariant():null;}
 static bool Aliased(Dictionary<string,Entry> es){return es.Where(e=>TagFromName(e.Key)!=null&&e.Value.Size>0).GroupBy(e=>e.Value.Offset+":"+e.Value.Size).Any(g=>g.Count()>1);}
 static byte[] Original(string root,string fallback) {
  string folder=Path.Combine(root,"data","loc"),backup=Path.Combine(folder,"locale-original.big");byte[] b=File.ReadAllBytes(File.Exists(backup)?backup:Path.Combine(folder,"locale.big"));var es=Entries(b);
  if(Aliased(es)){if(File.Exists(backup)||String.IsNullOrEmpty(fallback)||!File.Exists(fallback))throw new InvalidDataException("Falta una copia original de las bases de idioma; no se sobrescribira el respaldo");byte[] candidate=File.ReadAllBytes(fallback);var original=Entries(candidate);if(Aliased(original)||!es.Keys.OrderBy(x=>x).SequenceEqual(original.Keys.OrderBy(x=>x),StringComparer.OrdinalIgnoreCase))throw new InvalidDataException("El respaldo de idiomas pertenece a otra copia");b=candidate;}return b;
 }
 static string Code(string tag){return tag=="eng_us"?"en":tag=="spa_mx"?"es":tag;}
 public static Language Describe(string tag) {
  tag=tag.ToLowerInvariant();string name;
  switch(tag){case "eng_us":name="English (US)";break;case "eng_gb":name="English (UK)";break;case "spa_mx":name="Espa\u00f1ol (Latinoam\u00e9rica)";break;case "spa_es":name="Espa\u00f1ol (Espa\u00f1a)";break;case "por_br":name="Portugu\u00eas (Brasil)";break;case "por_pt":name="Portugu\u00eas (Portugal)";break;case "ita_it":name="Italiano";break;case "fre_fr":case "fra_fr":name="Fran\u00e7ais";break;case "ger_de":case "deu_de":name="Deutsch";break;case "dut_nl":case "nld_nl":name="Nederlands";break;case "pol_pl":name="Polski";break;case "rus_ru":name="\u0420\u0443\u0441\u0441\u043a\u0438\u0439";break;case "jpn_jp":name="\u65e5\u672c\u8a9e";break;case "kor_kr":name="\ud55c\uad6d\uc5b4";break;default:name=tag;break;}
  string prefix=tag.Split('_')[0];int id=prefix=="jpn"?2:prefix=="ger"||prefix=="deu"?3:prefix=="fre"||prefix=="fra"?4:prefix=="spa"?5:prefix=="ita"?6:prefix=="kor"?7:prefix=="por"?9:prefix=="pol"?11:prefix=="rus"?12:1;return new Language{Tag=tag,Code=Code(tag),Name=name,UserLanguage=id};
 }
 public static Language Resolve(IEnumerable<Language> languages,string code) {
  if(String.IsNullOrEmpty(code))return null;code=code.ToLowerInvariant();var all=languages.ToList();var exact=all.FirstOrDefault(l=>l.Code==code||l.Tag==code);if(exact!=null)return exact;
  string prefix=code=="en"?"eng":code=="es"?"spa":code=="pt"?"por":code=="it"?"ita":code=="fr"?"fre":code=="de"?"ger":null;return prefix==null?null:all.FirstOrDefault(l=>l.Tag!=null&&l.Tag.StartsWith(prefix+"_",StringComparison.Ordinal));
 }
 static Catalog Detect(string root,byte[] original) {
  var es=Entries(original);var catalog=new Catalog();foreach(var e in es){string tag=TagFromName(e.Key);Entry meta;if(tag!=null&&e.Value.Size>0&&es.TryGetValue("data/loc/"+tag+"-meta.xml",out meta)&&meta.Size>0)catalog.Text.Add(Describe(tag));}
  string speech=Path.Combine(root,"audiodata","speechdata");if(Directory.Exists(speech))foreach(string folder in Directory.GetDirectories(speech)){string tag=Path.GetFileName(folder).ToLowerInvariant();if(Regex.IsMatch(tag,@"^[a-z]{3}_[a-z]{2}$")&&new[]{"speech_dat.big","speech_hdr.big","speech_sth.big"}.All(n=>File.Exists(Path.Combine(folder,n))&&new FileInfo(Path.Combine(folder,n)).Length>0))catalog.Commentary.Add(Describe(tag));}
  if(catalog.Text.Count==0)throw new InvalidDataException("Esta copia no contiene bases de texto reconocibles");catalog.Text=catalog.Text.OrderBy(l=>l.Tag).ToList();catalog.Commentary=catalog.Commentary.OrderBy(l=>l.Tag).ToList();
  string tagDefault=null;Entry ini;if(es.TryGetValue("locale.ini",out ini)){string config=Encoding.ASCII.GetString(original,(int)ini.Offset,(int)ini.Size);var m=Regex.Match(config,@"(?im)^\s*DEFAULT_LANGUAGE\s*=\s*([a-z]{3})\s*,\s*([a-z]{2})\b");if(m.Success)tagDefault=m.Groups[1].Value.ToLowerInvariant()+"_"+m.Groups[2].Value.ToLowerInvariant();}
  catalog.DefaultText=(Resolve(catalog.Text,tagDefault)??catalog.Text[0]).Code;catalog.DefaultCommentary=catalog.Commentary.Count==0?"original":(Resolve(catalog.Commentary,tagDefault)??catalog.Commentary[0]).Code;return catalog;
 }
 public static Catalog Detect(string root,string fallback){return Detect(root,Original(root,fallback));}
 // A fresh extraction must not inherit the original-locale cache of another disc.
 public static void AdoptExtractedLocale(string root){
  string folder=Path.Combine(root,"data","loc"),archive=Path.Combine(folder,"locale.big"),backup=Path.Combine(folder,"locale-original.big");
  byte[] fresh=File.ReadAllBytes(archive);if(Aliased(Entries(fresh))||!File.Exists(backup))return;
  if(!fresh.SequenceEqual(File.ReadAllBytes(backup)))File.Move(backup,Path.Combine(folder,"locale-original.previous-"+Guid.NewGuid().ToString("N")+".big"));
 }
 public static void Configure(string root,string text,string commentary,string fallback) {
  byte[] original=Original(root,fallback);var es=Entries(original);var catalog=Detect(root,original);var selected=Resolve(catalog.Text,text);if(selected==null)throw new ArgumentException("Esta copia no contiene los textos: "+text);
  Language speech=null;if(commentary!="original"||catalog.Commentary.Count>0){speech=Resolve(catalog.Commentary,commentary);if(speech==null)throw new ArgumentException("Esta copia no contiene los comentarios: "+commentary);}
  byte[] result=(byte[])original.Clone();Entry db=es["data/loc/"+selected.Tag+".db"],meta=es["data/loc/"+selected.Tag+"-meta.xml"];
  foreach(var lang in catalog.Text){Entry target=es["data/loc/"+lang.Tag+".db"];Write(result,target.Index,db.Offset);Write(result,target.Index+4,db.Size);target=es["data/loc/"+lang.Tag+"-meta.xml"];Write(result,target.Index,meta.Offset);Write(result,target.Index+4,meta.Size);}
  Entry ini;if(!es.TryGetValue("locale.ini",out ini))throw new InvalidDataException("Falta locale.ini");string config=Encoding.ASCII.GetString(original,(int)ini.Offset,(int)ini.Size);var match=Regex.Match(config,@"(?im)^\s*DEFAULT_LANGUAGE\s*=\s*(?<language>[a-z]{3})\s*,\s*(?<region>[a-z]{2})\b");if(!match.Success)throw new InvalidDataException("Configuracion de idiomas inesperada");
  if(speech!=null){Encoding.ASCII.GetBytes(speech.Tag.Substring(0,3)).CopyTo(result,(int)ini.Offset+match.Groups["language"].Index);Encoding.ASCII.GetBytes(speech.Tag.Substring(4,2)).CopyTo(result,(int)ini.Offset+match.Groups["region"].Index);}
  string folder=Path.Combine(root,"data","loc"),archive=Path.Combine(folder,"locale.big"),backup=Path.Combine(folder,"locale-original.big");if(!File.Exists(backup))using(var stream=new FileStream(backup,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(original,0,original.Length);stream.Flush(true);}
  string temp=archive+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(result,0,result.Length);stream.Flush(true);}File.Replace(temp,archive,null);}finally{if(File.Exists(temp))File.Delete(temp);}
 }
 public static int UserLanguage(string code){return Describe(code=="en"?"eng_us":code=="es"?"spa_mx":code).UserLanguage;}
}
