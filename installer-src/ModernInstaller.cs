using System;using System.Drawing;using System.Windows.Forms;using System.Collections.Generic;using System.IO;using System.Linq;
namespace FIFA08Installer {public partial class InstallerForm {
 Dictionary<Control,Rectangle> modernRects;int originalCover;FIFA08Launcher.PremiumPanel installCard;Panel installFields,installActions;Control modernTitle;
 void InitializeModernInstaller(){
  btnClose.Visible=false;btnMinimize.Visible=false;originalCover=coverWidth;
  var fields=Controls.Cast<Control>().Where(c=>c.Top>=68&&c.Top<=336).ToArray();
  var subtitle=Controls.Cast<Control>().FirstOrDefault(c=>c is Label&&c.Top==48);
  foreach(Control c in Controls)if(c is InstallerTitle)c.Visible=false;
  modernTitle=new FIFA08Launcher.BrandHeader(Path.Combine(rootDir,Directory.Exists(Path.Combine(rootDir,"launcher-assets"))?"launcher-assets":"payload/launcher-assets","ea-fifa-reference.png"));
  Controls.Add(modernTitle);
  installCard=new FIFA08Launcher.PremiumPanel();Controls.Add(installCard);
  installFields=new FIFA08Launcher.DarkScrollPanel{AutoScroll=true,BackColor=Color.FromArgb(17,17,20)};
  installActions=new Panel{BackColor=Color.FromArgb(17,17,20)};
  installCard.Controls.Add(new FIFA08Launcher.DarkScrollHost(installFields));installCard.Controls.Add(installActions);
  modernRects=new Dictionary<Control,Rectangle>();
  foreach(var c in fields){var r=c.Bounds;if(c is Label&&r.Y==68)r.Height=48;installFields.Controls.Add(c);if(c is Label){((Label)c).AutoEllipsis=true;c.BackColor=Color.Transparent;}modernRects[c]=new Rectangle(r.X-originalCover-18,r.Y-68+34,r.Width,r.Height);}
  if(subtitle!=null){installFields.Controls.Add(subtitle);modernRects[subtitle]=new Rectangle(12,4,448,20);}
  installActions.Controls.Add(btnInstall);installActions.Controls.Add(btnCancel);
  MinimumSize=new Size(760,500);var area=Screen.FromControl(this).WorkingArea;
  ClientSize=new Size(Math.Min(1040,area.Width-48),Math.Min(640,area.Height-90));
  Resize+=(s,e)=>ModernInstallerLayout();ModernInstallerLayout();
 }
 void ModernInstallerLayout(){
  if(modernRects==null)return;SuspendLayout();
  coverWidth=(int)(ClientSize.Width*.43);int w=ClientSize.Width-coverWidth;
  int cw=Math.Min(880,w-Math.Max(32,(int)(w*.12))),cx=coverWidth+(w-cw)/2;
  modernTitle.SetBounds(cx,32,cw,62);
  installCard.SetBounds(cx,126,cw,Math.Max(260,ClientSize.Height-156));
  int ch=installCard.ClientSize.Height;
  installFields.Parent.SetBounds(12,12,cw-24,Math.Max(100,ch-108));
  installActions.SetBounds(12,ch-88,cw-24,78);
  Point scroll=installFields.AutoScrollPosition;installFields.AutoScrollPosition=Point.Empty;
  float sx=Math.Max(.1f,(installFields.Parent.Width-34)/472f);
  foreach(var pair in modernRects){var r=pair.Value;pair.Key.SetBounds((int)(r.X*sx),r.Y,Math.Max(1,(int)(r.Width*sx)),r.Height);}
  installFields.AutoScrollMinSize=new Size(0,286);
  installFields.AutoScrollPosition=new Point(-scroll.X,-scroll.Y);
  btnInstall.SetBounds((installActions.Width-180)/2,2,180,40);
  btnCancel.SetBounds((installActions.Width-90)/2,48,90,26);
  if(uiLanguage!=null){uiLanguage.SetBounds(ClientSize.Width-100,6,80,24);uiLanguage.Visible=true;uiLanguage.BringToFront();}
  ResumeLayout();Invalidate();
 }
}}
