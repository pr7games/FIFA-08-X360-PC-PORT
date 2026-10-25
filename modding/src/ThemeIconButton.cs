using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Pr7Editor {
public sealed class ThemeIconButton : Button {
 public bool ShowSun {get;set;}
 public ThemeIconButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);Cursor=Cursors.Hand;}
 protected override void OnPaintBackground(PaintEventArgs e){}
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent==null?BackColor:Parent.BackColor);
  using(var path=EditorRound.GetRoundedPath(new Rectangle(1,1,Width-3,Height-3),7)) {
   using(var brush=new SolidBrush(BackColor))g.FillPath(brush,path);
   using(var pen=new Pen(Focused?Color.FromArgb(224,26,34):FlatAppearance.BorderColor))g.DrawPath(pen,path);
  }
  float x=Width/2f,y=Height/2f;
  if(ShowSun) {
   Color color=Color.FromArgb(245,193,82);
   using(var pen=new Pen(color,1.5f)) {
    g.DrawEllipse(pen,x-4,y-4,8,8);
    for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(pen,x+(float)Math.Cos(a)*6,y+(float)Math.Sin(a)*6,x+(float)Math.Cos(a)*8.5f,y+(float)Math.Sin(a)*8.5f);}
   }
  } else {
   using(var brush=new SolidBrush(ForeColor))g.FillEllipse(brush,x-7,y-8,14,16);
   using(var brush=new SolidBrush(BackColor))g.FillEllipse(brush,x-2,y-10,12,15);
  }
 }
}
public sealed partial class EditorForm {
 void UpdateThemeButton() {
  var icon=themeButton as ThemeIconButton;if(icon==null)return;
  icon.ShowSun=DarkTheme;icon.Text="";icon.AccessibleName=L.T(DarkTheme?"Cambiar al tema claro":"Cambiar al tema oscuro");
  toolbarTips.SetToolTip(icon,icon.AccessibleName);icon.Invalidate();
 }
}
}
