using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Pr7Editor {
public sealed class AppearanceRules {
 public static readonly HashSet<string> AttributeFields=new HashSet<string>(("overallrating potential acceleration sprintspeed agility balance jumping reactions stamina strength aggression positioning vision ballcontrol crossing curve dribbling dribspeed finishing freekickaccuracy headingaccuracy longpassing longshots marking penalties shortpassing shotpower slidingtackle standingtackle tacticalawareness volleys gkdiving gkhandling gkkicking gkoneonone gkpositioning gkreflexes weakfootabilitytypecode preferredfoot height weight").Split(' '));
 public static readonly Dictionary<int,string> Accessories=new Dictionary<int,string>{{0,"Sin accesorio"},{8,"Munequera izquierda"},{9,"Munequera derecha"},{14,"Cinta tobillo izquierdo"},{15,"Cinta tobillo derecho"},{22,"Cinta dedos izquierdos"},{23,"Cinta dedos derechos"},{24,"Cinta larga muneca izquierda"},{25,"Cinta larga muneca derecha"},{26,"Brazalete izquierdo"},{27,"Brazalete derecho"}};
 public readonly Dictionary<int,SortedSet<int>> ShoeColors=new Dictionary<int,SortedSet<int>>();
 readonly Dictionary<int,SortedSet<int>> AccessoryColors=new Dictionary<int,SortedSet<int>>();
 public AppearanceRules(IEnumerable<Asset> assets,IEnumerable<Dictionary<string,object>> originalPlayers) {
  var models=new HashSet<int>();foreach(var a in assets){var model=Regex.Match(a.Name,@"/shoe/shoe_(\d+)\.rx2$",RegexOptions.IgnoreCase);if(model.Success)models.Add(Int32.Parse(model.Groups[1].Value));var m=Regex.Match(a.Name,@"/shoe/shoe_(\d+)_(\d+)_textures\.rx2$",RegexOptions.IgnoreCase);if(m.Success){int id=Int32.Parse(m.Groups[1].Value),color=Int32.Parse(m.Groups[2].Value);SortedSet<int> colors;if(!ShoeColors.TryGetValue(id,out colors))ShoeColors[id]=colors=new SortedSet<int>();colors.Add(color);}}
  foreach(int id in ShoeColors.Keys.ToArray())if(!models.Contains(id))ShoeColors.Remove(id);
  // These combinations exist in the shipped player database. Texture existence
  // alone is insufficient: some tape types share another accessory's textures.
  foreach(var p in originalPlayers)for(int i=1;i<=4;i++){int id=(int)GameDatabase.Number(p,"accessorycode"+i),color=(int)GameDatabase.Number(p,"accessorycolourcode"+i);if(!Accessories.ContainsKey(id))continue;SortedSet<int> cs;if(!AccessoryColors.TryGetValue(id,out cs))AccessoryColors[id]=cs=new SortedSet<int>();cs.Add(color);}
 }
 public int[] Colors(string code,int id){SortedSet<int> c;if(code=="shoetypecode")return ShoeColors.TryGetValue(id,out c)?c.ToArray():new int[0];return AccessoryColors.TryGetValue(id,out c)?c.ToArray():new[]{0};}
 public void Validate(Dictionary<string,object> original,Dictionary<string,long> values){
  Func<string,long> get=k=>values.ContainsKey(k)?values[k]:GameDatabase.Number(original,k);
  int shoe=(int)get("shoetypecode"),color=(int)get("shoecolorcode1");SortedSet<int> cs;
  if(!ShoeColors.TryGetValue(shoe,out cs))throw new ArgumentException("No existe el modelo de botines "+shoe+".");
  if(!cs.Contains(color)){if(shoe!=GameDatabase.Number(original,"shoetypecode"))values["shoecolorcode1"]=cs.Min;else throw new ArgumentException("Los botines "+shoe+" no tienen el color "+color+". Selecciona una variante existente.");}
  for(int i=1;i<=4;i++){string code="accessorycode"+i,key="accessorycolourcode"+i;int id=(int)get(code),col=(int)get(key);if(!Accessories.ContainsKey(id))throw new ArgumentException("Accesorio no identificado: "+id);if(id==0)continue;
   if(!AccessoryColors.TryGetValue(id,out cs)||!cs.Contains(col)){if(id!=GameDatabase.Number(original,code)&&cs!=null&&cs.Count>0)values[key]=cs.Min;else throw new ArgumentException("Combinacion de accesorio/color no verificada: "+id+" / "+col);}
  }
 }
}
}
