using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Pr7Editor {
internal static class L {
 internal static bool English;
 internal static readonly Dictionary<string,string> Words=new Dictionary<string,string>{
 {"Carpeta del juego","Game folder"},{"Abrir carpeta","Open folder"},{"Guardar cambios","Save changes"},{"Restaurar originales","Restore all originals"},
 {"Jugadores","Players"},{"Caras","Faces"},{"Caras y piel","Faces & skin"},{"Camisetas","Kits"},{"Botines","Boots"},
 {"Todas las ligas","All leagues"},{"Todos los equipos","All teams"},{"Liga: ","League: "},{"Equipo: ","Team: "},
 {"Buscar jugador o ID","Search player or ID"},{"Datos y atributos","Player attributes"},{"Apariencia y vestimenta","Appearance & clothing"},
 {"Campo","Field"},{"Valor","Value"},{"Preparar jugador","Apply player edits"},{"Ver botas","View boots"},{"Ver cara","View face"},{"Restaurar jugador","Restore player"},
 {"Los cambios se preparan al cambiar de jugador. Guardar cambios escribe con respaldo.","Switching players prepares your edits. Save changes writes them with a backup."},
 {"Buscar nombre, equipo, ID o archivo","Search name, team, ID or file"},{"Exportar PNG","Export PNG"},{"Exportar DDS","Export DDS"},{"Importar PNG","Import PNG"},{"Importar DDS","Import DDS"},
 {"Restaurar este recurso","Restore this resource"},{"Sin texturas seleccionadas.","No texture selected."},{"Sin superficies 2D compatibles; no se modificara.","No compatible 2D surfaces; this resource will not be edited."},
 {"PNG regenera los mipmaps; DDS conserva los bloques importados.","PNG rebuilds mipmaps; DDS preserves imported blocks."},
 {"Selecciona una textura compatible.","Select a compatible texture."},{"Selecciona un recurso.","Select a resource."},{"Selecciona un jugador.","Select a player."},
 {"Nombre","First name"},{"Apellido","Last name"},{"Nombre habitual","Common name"},{"Nombre en camiseta","Jersey name"},{"Media general","Overall rating"},
 {"Botines: ID","Boots: ID"},{"Botines: color","Boots: color"},{"Camiseta: estilo (0/1)","Jersey: style (0/1)"},{"Camiseta: mangas (0/1)","Jersey: sleeves (0/1)"},
 {"Aceleracion","Acceleration"},{"Velocidad","Sprint speed"},{"Definicion","Finishing"},{"Pase corto","Short passing"},{"Pase largo","Long passing"},
 {"Regate","Dribbling"},{"Control","Ball control"},{"Potencia de tiro","Shot power"},{"Resistencia","Stamina"},
 {"Modelo de botines","Boot model"},{"Variante de botines","Boot variant"},{"Camiseta","Jersey"},{"Por dentro","Tucked in"},{"Por fuera","Untucked"},
 {"Mangas","Sleeves"},{"Cortas","Short"},{"Largas","Long"},{"Guantes de invierno","Winter gloves"},{"No","No"},{"Si","Yes"},
 {"Sin accesorio","No accessory"},{"Munequera izquierda","Left wristband"},{"Munequera derecha","Right wristband"},{"Cinta tobillo izquierdo","Left ankle tape"},{"Cinta tobillo derecho","Right ankle tape"},
 {"Cinta dedos izquierdos","Left finger tape"},{"Cinta dedos derechos","Right finger tape"},{"Cinta larga muneca izquierda","Long left wrist tape"},{"Cinta larga muneca derecha","Long right wrist tape"},
 {"Brazalete izquierdo","Left armband"},{"Brazalete derecho","Right armband"},
 {"Textura","Texture"},{"Shorts y medias","Shorts & socks"},{"Escudo","Badge"},{"Auxiliar","Auxiliary"},{"Cara","Face"},
 {"Piel corporal (compartida)","Body skin (shared)"},{"Cuerpo (compartido)","Body (shared)"},{"Cabeza (compartido)","Head (shared)"},{"Shorts (compartido)","Shorts (shared)"},{"Ojos","Eyes"},
 {"Todas las caras y pieles","All faces & skin"},{"Caras individuales","Individual faces"},{"Piel corporal","Body skin"},{"Ojos y cabeza","Eyes & head"},
 {"Mapas corporales compartidos","Shared body maps"},
 {": rango ",": range "},{": nombre demasiado largo.",": name is too long."},
 {"DDS: conserva dimensiones y la cadena completa de ","DDS: preserve dimensions and all "},
 {"La imagen debe medir ","The image must be "},{"DDS: solo se admite una superficie 2D.","DDS: only one 2D surface is supported."},
 {"El DDS debe conservar el formato ","DDS must preserve format "},{" (sin cabecera DX10)."," (without a DX10 header)."},
 {"DDS: superficies adicionales o datos sobrantes.","DDS: extra surfaces or trailing data."},
 {"Este recurso se comparte entre jugadores. La importacion modifica el recurso compartido.","This resource is shared by players. Importing changes the shared resource."},
 {"El atlas de shorts y medias se edita como una sola textura.","Shorts and socks share one texture atlas."},
 {"Cambiar al tema claro","Switch to light theme"},{"Cambiar al tema oscuro","Switch to dark theme"},{"Cambiar idioma ES / EN","Switch language ES / EN"},
 {"Selecciona un valor valido del catalogo.","Select a valid catalog value."},{"Selecciona un valor numerico valido.","Select a valid numeric value."},
 {"No hay variantes disponibles.","No variants available."},{"Abre la carpeta del juego para comenzar.","Open the game folder to begin."},
 {"No hay cambios preparados. Usa Preparar jugador o importa una textura.","No pending changes. Apply player edits or import a texture."},
 {"Jugador preparado con combinaciones verificadas. Pulsa Guardar cambios para aplicarlo.","Player edits prepared with verified combinations. Click Save changes to apply."},
 {"Guardando y verificando respaldos... puede tardar al copiar los BIG.","Saving and verifying backups... copying BIG archives may take a while."},
 {"Este recurso ya coincide con el original. Se descarto su importacion pendiente; las demas ediciones se conservan.","This resource already matches the original. Its pending import was discarded; other edits are preserved."},
 {"Hay cambios sin guardar. ¿Cerrar y descartarlos?","There are unsaved edits. Close and discard them?"},
 {"¿Descartar los cambios pendientes y abrir otra carpeta?","Discard pending edits and open another folder?"},
 {"Selecciona la carpeta del port o GameData","Select the port or GameData folder"},
 {"Original (no identificado)","Original (unidentified)"},
 {"Cierra FIFA 08 antes de guardar o restaurar archivos.","Close FIFA 08 before saving or restoring files."},
 {"No se completo la operacion: ","The operation could not be completed: "},{"Exportado: ","Exported: "},
 {"Reemplazo pendiente: ","Replacement pending: "},{". Pulsa Guardar cambios para aplicarlo.",". Click Save changes to apply."},
 {"Original preparado: ","Original prepared: "},{". Pulsa Guardar cambios. Solo se restaura este recurso completo.",". Click Save changes. Only this complete resource will be restored."},
 {"Original del jugador ","Original player "},{" preparado. Pulsa Guardar cambios. Los demas jugadores y recursos conservan sus cambios."," prepared. Click Save changes. Other players and resources keep their edits."},
 {"Base deportiva global cargada: ","Global player database loaded: "},{" jugadores. Los textos de interfaz por idioma no se alteran."," players. Game interface language files are preserved."},
 {"Botines ID ","Boot ID "},{"Botines ","Boots "},{" / color "," / color "},{" — Variante ausente"," — Missing variant"},
 {"Accesorio ","Accessory "},{"Color accesorio ","Accessory color "},{"Rango: ","Range: "},
 {"No hay un respaldo original verificado de ","No verified original backup is available for "},{"No se reconoce el original de ","Unknown original archive: "},
 {". No se ha cambiado ningun recurso.",". No resource has been changed."},{"El recurso no existe en el original verificado: ","Resource missing from the verified original: "},
 {"El jugador no existe en el original verificado.","Player missing from the verified original."},
 {"El BIG cambio desde que se abrio el editor. Vuelve a cargarlo antes de guardar.","The BIG archive changed after the editor opened it. Reload it before saving."},
 {"Restauracion completa: ","Restoration complete: "},{" archivos recuperados; 14 BIG/BH originales verificados."," files recovered; all 14 original BIG/BH files verified."},
 {"Los 14 BIG/BH ya coinciden con los originales.","All 14 BIG/BH files already match their originals."},{"Localizando originales y verificando respaldos...","Finding original files and verifying backups..."}
 ,{"Se aplicaran ","Preparing to save "},{" recursos con respaldo automatico."," resources with automatic backups."},
 {"Las plantillas guardadas pueden tener prioridad sobre los datos base. ¿Guardar?","Saved squads may override base data. Save changes?"},
 {"Cambios guardados.","Changes saved."},{"Respaldo: ","Backup: "},{"Guardado completado. Respaldo: ","Save complete. Backup: "},
 {"Prueba el juego con una plantilla nueva o restablecida para verificar los datos base.","Use a new or reset squad in the game to check the base data."},
 {"Restaurar TODOS los BIG/BH originales verificados, incluyendo datos y texturas de TODOS los guardados del editor.\r\n\r\nSe descartaran cambios pendientes. Tus partidas y plantillas no se modifican. ¿Continuar?","Restore ALL verified original BIG/BH files, including data and textures from ALL editor saves.\r\n\r\nPending edits will be discarded. Saved games and squads are preserved. Continue?"}
 };
 static readonly KeyValuePair<string,string>[] ordered=Words.Where(p=>p.Key.Length>=8).OrderByDescending(p=>p.Key.Length).ToArray();
 static readonly Dictionary<string,string> translated=new Dictionary<string,string>();
 internal static string T(string text) {
  if(!English||String.IsNullOrEmpty(text))return text;
  string exact;if(Words.TryGetValue(text,out exact))return exact;
  if(translated.TryGetValue(text,out exact))return exact;
  string source=text;
  // Dynamic UI messages retain paths, resource IDs and user-entered names.
  foreach(var item in ordered)if(text.Contains(item.Key))text=text.Replace(item.Key,item.Value);
  if(translated.Count>=2048)translated.Clear();translated[source]=text;
  return text;
 }
 internal static string Option(string text) {
  int separator=text.IndexOf(" — ",StringComparison.Ordinal);if(separator<0)return T(text);
  return text.Substring(0,separator+3)+T(text.Substring(separator+3));
 }
 internal static string Preference {get{return Path.Combine(Path.GetDirectoryName(ThemePreference.PathName),"language.txt");}}
 internal static bool ReadPreference(string path){try{return File.ReadAllText(path).Trim()=="en";}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}}
 internal static void WritePreference(string path,bool english){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,english?"en":"es");}
}

public sealed partial class EditorForm {
 readonly Dictionary<Control,string> captions=new Dictionary<Control,string>();
 readonly ToolTip toolbarTips=new ToolTip();Button languageButton;
 string statusSource="";
 void InitializeLanguage(FlowLayoutPanel toolbar) {
  languageButton=Button("ES | EN",()=>SetEnglish(!L.English,true));languageButton.Width=70;
  toolbar.Controls.Add(languageButton);SetEnglish(L.ReadPreference(L.Preference),false);
  toolbar.Height=50;RootPath.Width=Math.Max(220,toolbar.ClientSize.Width-760);
  toolbar.Resize+=(s,e)=>RootPath.Width=Math.Max(220,toolbar.ClientSize.Width-760);
 }
 internal void LocalizeControl(Control root) {
  if(root is Button||root is GroupBox||root is TabPage||root is Label&&root!=Status&&root!=PlayerTitle&&root!=LeagueCaption&&root!=TeamCaption&&root!=ShoeCaption&&!(root.Parent is TexturePanel)) {
   string source;if(!captions.TryGetValue(root,out source)){source=root.Text;captions[root]=source;}
   var label=root as Label;if(label!=null)label.UseMnemonic=false;
   var button=root as Button;if(button!=null)button.UseMnemonic=false;
   root.Text=root is GroupBox?L.T(source).Replace("&","&&"):L.T(source);
  }
  foreach(Control child in root.Controls)LocalizeControl(child);
 }
 public void SetEnglish(bool english,bool persist) {
  if(!Fields.EndEdit()||!AppearanceGrid.EndEdit())throw new InvalidOperationException(L.T("Selecciona un valor valido del catalogo."));
  bool showing=ShowingPlayer;ShowingPlayer=true;
  try {
   L.English=english;LocalizeControl(this);
   foreach(var grid in new[]{Fields,AppearanceGrid}) {
    grid.Columns[0].HeaderText=L.T("Campo");grid.Columns[1].HeaderText=L.T("Valor");
    foreach(DataGridViewRow row in grid.Rows) {
     string source=Convert.ToString(row.Cells[0].Tag??row.Cells[0].Value);row.Cells[0].Tag=source;
     row.Cells[0].Value=L.T(source);
     string tag=Convert.ToString(row.Tag);if(State!=null&&tag.StartsWith("number:")){var field=State.Db.Tables["players"].Fields.Single(f=>f.Name==tag.Substring(7));row.Cells[1].ToolTipText=L.T("Rango: ")+field.Low+" .. "+field.High;}
     var combo=row.Cells[1] as DataGridViewComboBoxCell;
     if(combo!=null) {
      long id=ParseValue(combo.Value);var options=combo.Items.Cast<string>().Select(v=>L.Option(ToSpanishOption(v))).ToArray();
      combo.Value=null;combo.Items.Clear();combo.Items.AddRange(options);combo.Value=options.First(v=>ParseValue(v)==id);
     }
    }
   }
   var league=League.SelectedItem as Choice;var team=Team.SelectedItem as Choice;
   LeagueCaption.Text=L.T("Liga: ")+(league==null?"":league.ToString());TeamCaption.Text=L.T("Equipo: ")+(team==null?"":team.ToString());
   languageButton.Text=english?"EN | ES":"ES | EN";
   toolbarTips.SetToolTip(languageButton,L.T("Cambiar idioma ES / EN"));UpdateThemeButton();
   Faces.RefreshLanguage();Kits.RefreshLanguage();Shoes.RefreshLanguage();
   if(Selected!=null)RefreshShoePreview();Status.Text=L.T(statusSource);
   League.Refresh();Team.Refresh();PlayerList.Refresh();Tabs.Invalidate();
   if(persist)L.WritePreference(L.Preference,english);
  } finally {ShowingPlayer=showing;}
 }
 static string ToSpanishOption(string value) {
  int at=value.IndexOf(" — ",StringComparison.Ordinal);if(at<0)return value;
  string suffix=value.Substring(at+3);var exact=L.Words.FirstOrDefault(p=>p.Value==suffix);
  if(exact.Key!=null)suffix=exact.Key;
  else if(suffix.StartsWith("Boot ID ",StringComparison.Ordinal))suffix="Botines ID "+suffix.Substring(8);
  return value.Substring(0,at+3)+suffix;
 }
 public void ValidateLanguageUi(string preferenceTest) {
  var first=Fields.Rows.Cast<DataGridViewRow>().Single(r=>Convert.ToString(r.Tag)=="name:firstname");
  object old=first.Cells[1].Value;first.Cells[1].Value="Pending Ω";
  var values=AppearanceGrid.Rows.Cast<DataGridViewRow>().Select(r=>ParseValue(r.Cells[1].Value)).ToArray();
  byte[] db=State.Db.Serialize();var pending=State.Pending.ToDictionary(p=>p.Key,p=>p.Value);int selected=Selected.Id;string search=Search.Text;bool english=L.English;
  try {
   SetEnglish(true,false);
   if(Convert.ToString(first.Cells[0].Value)!="First name")throw new Exception("Language: English field caption missing.");
   var shoe=AppearanceGrid.Rows.Cast<DataGridViewRow>().Single(r=>Convert.ToString(r.Tag)=="number:shoetypecode");
   if(!Convert.ToString(shoe.Cells[1].Value).Contains("Boot ID "))throw new Exception("Language: English options missing.");
   SetEnglish(false,false);
   if(Convert.ToString(first.Cells[0].Value)!="Nombre")throw new Exception("Language: Spanish field caption missing.");
   if(Convert.ToString(first.Cells[1].Value)!="Pending Ω"||!values.SequenceEqual(AppearanceGrid.Rows.Cast<DataGridViewRow>().Select(r=>ParseValue(r.Cells[1].Value)))||!db.SequenceEqual(State.Db.Serialize())||Selected.Id!=selected||Search.Text!=search||pending.Count!=State.Pending.Count||pending.Any(p=>!State.Pending.ContainsKey(p.Key)||!p.Value.SequenceEqual(State.Pending[p.Key])))throw new Exception("Language: pending edits or selection changed.");
   L.WritePreference(preferenceTest,true);if(!L.ReadPreference(preferenceTest))throw new Exception("Language: English preference not saved.");
   L.WritePreference(preferenceTest,false);if(L.ReadPreference(preferenceTest))throw new Exception("Language: Spanish preference not saved.");
  } finally {SetEnglish(english,false);first.Cells[1].Value=old;}
 }
}
}
