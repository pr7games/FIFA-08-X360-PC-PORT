using System;
using System.Linq;
using System.Windows.Forms;

namespace Pr7Editor {
public sealed partial class EditorForm {
 public void ValidateCatalogUi(){Kits.ValidateCatalogUi();Faces.ValidateCatalogUi();}
}
public sealed partial class TexturePanel {
 void SelectRole(string role) {
  int index=Textures.FindIndex(t=>t.Role==role);
  if(index<0)throw new InvalidOperationException(L.T("Selecciona una textura compatible."));
  Surfaces.SelectedIndex=index;
 }
 void UpdateInfo() {
  var t=Surfaces.SelectedItem as Texture;
  if(Current==null){Info.Text=L.T("Sin texturas seleccionadas.");return;}
  string help=TextureCatalog.Prefix(Current.Name).Length>0&&TextureCatalog.Prefix(Current.Name)!="Ojos"?"Este recurso se comparte entre jugadores. La importacion modifica el recurso compartido.":t!=null&&t.Role=="Shorts y medias"?"El atlas de shorts y medias se edita como una sola textura.":"PNG regenera los mipmaps; DDS conserva los bloques importados.";
  Info.Text=Current.Name+"\r\n"+(t==null?L.T("Sin superficies 2D compatibles; no se modificara."):t.ToString())+"\r\n"+L.T(help);
 }
 public void RefreshLanguage() {Files.Refresh();Surfaces.Refresh();Category.Refresh();UpdateInfo();}
 internal void ValidateCatalogUi() {
  if(Kind=="Camisetas") {
   FocusId(11);SelectRole("Shorts y medias");
   var t=Surfaces.SelectedItem as Texture;
   if(t==null||t.Section!=7||t.Width!=1024||t.Height!=512)throw new Exception("Catalog: shorts/socks atlas missing.");
   using(var preview=t.Preview())if(preview.Width!=1024)throw new Exception("Catalog: shorts preview invalid.");
   byte[] before=(byte[])BytesCurrent.Clone();t.ImportDds(t.Dds());if(!before.SequenceEqual(BytesCurrent))throw new Exception("Catalog: shorts DDS roundtrip changed bytes.");
  } else if(Kind=="Caras") {
   int count=All.Count(a=>TextureCatalog.IsSkin(a.Asset.Name));
   if(count!=10)throw new Exception("Catalog: expected ten body skin resources, got "+count);
   if(!All.Any(a=>a.Asset.Name.Contains("/heads/eyes_")))throw new Exception("Catalog: eye textures missing.");
   Category.SelectedIndex=2;
   if(Files.Items.Count!=10)throw new Exception("Catalog: skin filter did not display all ten resources.");
   foreach(var item in All.Where(a=>TextureCatalog.IsSkin(a.Asset.Name))) {
    byte[] data=(byte[])Owner.State.Read(item.Asset).Clone(),before=(byte[])data.Clone();
    var surfaces=Texture.Parse(data);if(surfaces.Count==0)throw new Exception("Catalog: skin has no editable texture.");
    foreach(var surface in surfaces)surface.ImportDds(surface.Dds());
    if(!data.SequenceEqual(before))throw new Exception("Catalog: body skin DDS roundtrip changed bytes.");
   }
  }
 }
}
}
