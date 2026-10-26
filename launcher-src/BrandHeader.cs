using System;using System.Drawing;using System.Drawing.Drawing2D;using System.IO;using System.Windows.Forms;
namespace FIFA08Launcher {
// Fit the viewport before WinForms computes scroll ranges. Always reserve the
// vertical gutter so showing it cannot push a child into horizontal overflow.
public class DarkFlowPanel:FlowLayoutPanel {
 public DarkFlowPanel(){DoubleBuffered=true;BackColor=Color.FromArgb(17,17,20);}
 protected override void OnLayout(LayoutEventArgs e){
  if(AutoScroll&&FlowDirection==FlowDirection.TopDown&&!WrapContents){
   int available=Math.Max(1,Math.Min(ClientSize.Width,Parent==null?ClientSize.Width:Parent.ClientSize.Width-14)-Padding.Horizontal-8);
   foreach(Control c in Controls)c.Width=Math.Max(1,available-c.Margin.Horizontal);
  }
  base.OnLayout(e);
 }
}
public class DarkScrollPanel:Panel {public DarkScrollPanel(){DoubleBuffered=true;BackColor=Color.FromArgb(17,17,20);}}
public class BrandHeader:Control {Image art;public BrandHeader(string path){SetStyle(ControlStyles.ResizeRedraw|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);DoubleBuffered=true;try{art=Image.FromFile(path);}catch{}ForeColor=Color.White;}protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.Clear(BackColor);int h=Math.Min(62,Math.Min(Height,Math.Max(24,Width/5)));int w=(int)(h*960f/448);using(var authorFont=new Font("Segoe UI",h*.23f,FontStyle.Bold)){float total=w+h*.86f+g.MeasureString("by PR7",authorFont).Width;g.TranslateTransform(Math.Max(0,(Width-total)/2f),Math.Max(0,(Height-h)/2f));}if(art!=null){int badge=(int)(h*.90f);g.DrawImage(art,new Rectangle(0,(h-badge)/2,badge,badge),new Rectangle(0,0,448,448),GraphicsUnit.Pixel);using(var attributes=new System.Drawing.Imaging.ImageAttributes()){var matrix=new System.Drawing.Imaging.ColorMatrix(new float[][]{new float[]{-1,0,0,0,0},new float[]{0,-1,0,0,0},new float[]{0,0,-1,0,0},new float[]{0,0,0,1,0},new float[]{1,1,1,0,1}});attributes.SetColorMatrix(matrix);g.DrawImage(art,new Rectangle(h,0,w-h,h),448,0,512,448,GraphicsUnit.Pixel,attributes);}}else g.DrawString("EA SPORTS  FIFA",Font,Brushes.White,0,0);using(var font=new Font("Bahnschrift",h*.42f,FontStyle.Bold))using(var red=new SolidBrush(PortUi.Accent))g.DrawString("08",font,red,w-2,h*.22f);using(var font=new Font("Segoe UI",h*.23f,FontStyle.Bold))g.DrawString("by PR7",font,Brushes.White,w+h*.86f,h*.30f);}protected override void Dispose(bool disposing){if(disposing&&art!=null)art.Dispose();base.Dispose(disposing);}}
public class TileStrip:FlowLayoutPanel {public override Size GetPreferredSize(Size proposedSize){return new Size(Width,Height);}}
public class NavigationTab:Button {
 bool selected,hovered;
 public bool Selected{get{return selected;}set{selected=value;Invalidate();}}
 public NavigationTab(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.PageTab;}
 protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
 protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
 protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Color.FromArgb(17,17,20):Parent.BackColor);}
 protected override void OnPaint(PaintEventArgs e){
  OnPaintBackground(e);if(Width<12||Height<12)return;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
  if(selected||hovered){using(var path=new GraphicsPath()){const int d=10;path.AddArc(0,0,d,d,180,90);path.AddArc(Width-d-1,0,d,d,270,90);path.AddLine(Width-1,d,Width-1,Height);path.AddLine(Width-1,Height,0,Height);path.CloseFigure();using(var brush=new SolidBrush(selected?Color.FromArgb(17,17,20):Color.FromArgb(28,28,32)))e.Graphics.FillPath(brush,path);}}
  TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(4,0,Width-8,Height),Enabled?(selected||hovered?Color.White:Color.Silver):Color.DimGray,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
  if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(5,4,Width-10,Height-8),Color.Silver,BackColor);
 }
}
public class ChoiceTile:Button {public ChoiceTile(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);DoubleBuffered=true;UseVisualStyleBackColor=false;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=Color.FromArgb(24,24,28);ForeColor=Color.Silver;Cursor=Cursors.Hand;}protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Color.FromArgb(16,16,19):Parent.BackColor);}protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?Color.FromArgb(16,16,19):Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var r=new Rectangle(1,1,Width-3,Height-3);using(var p=new GraphicsPath()){int d=14;p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();using(var b=new SolidBrush(!Enabled?Color.FromArgb(20,20,22):BackColor.ToArgb()==PortUi.Accent.ToArgb()?Color.FromArgb(56,PortUi.Accent):BackColor))e.Graphics.FillPath(b,p);using(var pen=new Pen(BackColor.R>50?PortUi.Accent:Color.FromArgb(48,48,53)))e.Graphics.DrawPath(pen,p);}float size=Font.Size;while(size>6.5f){using(var trial=new Font(Font.FontFamily,size,Font.Style)){if(TextRenderer.MeasureText(Text,trial,new Size(10000,10000),TextFormatFlags.NoPadding).Width<=r.Width-6&&TextRenderer.MeasureText(Text,trial,new Size(10000,10000),TextFormatFlags.NoPadding).Height<=r.Height-4)break;}size-=.5f;}using(var fitted=new Font(Font.FontFamily,size,Font.Style)){TextRenderer.DrawText(e.Graphics,Text,fitted,r,Enabled?ForeColor:Color.DimGray,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}}}
public class PremiumPanel:Panel {
 public PremiumPanel(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=Color.FromArgb(17,17,20);Padding=new Padding(12);}
 protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Color.FromArgb(14,14,15):Parent.BackColor);var r=new Rectangle(1,1,Width-3,Height-3);if(r.Width<12||r.Height<12)return;using(var p=new GraphicsPath()){const int d=18;p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var b=new LinearGradientBrush(r,Color.FromArgb(24,24,28),BackColor,90f))e.Graphics.FillPath(b,p);using(var pen=new Pen(Color.FromArgb(54,54,60)))e.Graphics.DrawPath(pen,p);}}
}}
