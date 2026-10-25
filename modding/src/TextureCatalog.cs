using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Pr7Editor {
internal static class TextureCatalog {
 internal static bool IsSkin(string name){return Regex.IsMatch(name,@"/body/skin_\d+_textures\.rx2$",RegexOptions.IgnoreCase);}
 internal static int FaceCategory(string name){return IsSkin(name)?2:name.Contains("/body/")?4:name.Contains("/heads/")?3:1;}
 internal static bool Includes(string kind,string name) {
  string n=name.ToLowerInvariant();if(!n.EndsWith(".rx2"))return false;
  if(kind=="Caras")return n.Contains("/faces/face_")&&n.Contains("textures")||IsSkin(n)||n.EndsWith("/body/body_common_textures.rx2")||n.Contains("/heads/")&&n.EndsWith("_textures.rx2");
  // The *_bnm/_dk helpers are stacked texture arrays, not standalone 2D
  // surfaces. The editable kit atlases already include shorts and socks.
  return kind=="Camisetas"?n.Contains("/kit/")&&(n.Contains("/kit/kit_")||n.EndsWith("_textures.rx2")):n.Contains("/shoe/shoe_")&&n.Contains("textures");
 }
 internal static string Prefix(string name) {
  string n=name.ToLowerInvariant();
  if(IsSkin(n))return "Piel corporal (compartida)";
  if(n.Contains("/body/"))return "Cuerpo (compartido)";
  if(n.Contains("/heads/eyes_"))return "Ojos";
  if(n.Contains("/heads/"))return "Cabeza (compartido)";
  if(n.Contains("/kit/shorts_"))return "Shorts (compartido)";
  return "";
 }
 internal static void Describe(string name,List<Texture> textures) {
  string n=name.ToLowerInvariant();
  foreach(var t in textures) {
   t.Role="";
   if(Regex.IsMatch(n,@"/kit/kit_\d+_\d+_\d+\.rx2$")&&textures.Any(x=>x.Section==3&&x.Width==1024&&x.Height==1024)&&textures.Any(x=>x.Section==7&&x.Width==1024&&x.Height==512))
    t.Role=t.Section==3?"Camiseta":t.Section==7?"Shorts y medias":t.Section==1?"Escudo":"Auxiliar";
   else if(IsSkin(n))t.Role="Piel corporal (compartida)";
   else if(n.Contains("/faces/"))t.Role="Cara";
   else if(n.Contains("/heads/eyes_"))t.Role="Ojos";
   else if(n.Contains("/kit/shorts_"))t.Role="Shorts (compartido)";
   else if(n.Contains("/body/"))t.Role="Cuerpo (compartido)";
   else if(n.Contains("/heads/"))t.Role="Cabeza (compartido)";
  }
 }
}
}
