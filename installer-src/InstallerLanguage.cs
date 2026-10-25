using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace FIFA08Installer {
 public partial class InstallerForm {
  Button uiLanguage;readonly Dictionary<Control,string> uiSources=new Dictionary<Control,string>();string statusSource;
  void InitializeLanguage(){uiLanguage=PortUi.Toggle(()=>SetUiEnglish(!PortUi.English,true));uiLanguage.Location=new Point(Width-158,7);Controls.Add(uiLanguage);uiLanguage.BringToFront();SetUiEnglish(PortUi.English,false);}
  public void SetUiEnglish(bool english,bool persist){PortUi.English=english;PortUi.Apply(this,uiSources,btnInstall,btnCancel,lblStatus,uiLanguage);btnInstall.Text=PortUi.T(isCompleted?"\u25b6   JUGAR AHORA":isInstalling?"INSTALANDO...":"INSTALAR FIFA 08");btnCancel.Text=PortUi.T(isCompleted?"Cerrar":isInstalling?"Cancelar":"Salir");if(statusSource==null)statusSource=lblStatus.Text;lblStatus.Text=PortUi.T(statusSource);uiLanguage.Text=english?"EN | ES":"ES | EN";if(persist)PortUi.Save();}
  void SetInstallerStatus(string text){statusSource=text;lblStatus.Text=PortUi.T(text);}
 }
}
