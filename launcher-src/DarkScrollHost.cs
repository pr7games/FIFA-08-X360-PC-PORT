using System;using System.Drawing;using System.Windows.Forms;
namespace FIFA08Launcher {
// The OS scrollbar remains functional, but lives outside this clipped viewport.
// A fully owner-drawn client control provides the visible scrollbar instead.
public sealed class DarkScrollHost:Panel {
 readonly ScrollableControl view;readonly Thumb bar;bool sizing;
 public DarkScrollHost(ScrollableControl content){view=content;BackColor=Color.FromArgb(17,17,20);DoubleBuffered=true;
  view.Dock=DockStyle.None;Controls.Add(view);bar=new Thumb(view);Controls.Add(bar);bar.BringToFront();
  view.Scroll+=(s,e)=>bar.Invalidate();view.Layout+=(s,e)=>bar.Invalidate();
  view.MouseWheel+=(s,e)=>bar.Invalidate();view.VisibleChanged+=(s,e)=>bar.Invalidate();
 }
 protected override void OnLayout(LayoutEventArgs e){if(sizing||bar==null)return;sizing=true;try{
  base.OnLayout(e);view.SetBounds(0,0,ClientSize.Width+SystemInformation.VerticalScrollBarWidth,ClientSize.Height);
  var oldRegion=view.Region;view.Region=new Region(new Rectangle(0,0,Math.Max(1,ClientSize.Width-14),ClientSize.Height));if(oldRegion!=null)oldRegion.Dispose();
  bar.SetBounds(Math.Max(0,ClientSize.Width-14),0,14,ClientSize.Height);bar.BringToFront();
 }finally{sizing=false;}}
 sealed class Thumb:Control {
  readonly ScrollableControl target;bool dragging;int grab;
  public Thumb(ScrollableControl target){this.target=target;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=Color.FromArgb(20,20,23);Cursor=Cursors.Default;}
  int Maximum{get{return Math.Max(0,target.VerticalScroll.Maximum-target.VerticalScroll.LargeChange+1);}}
  Rectangle HandleRect(){if(!target.VerticalScroll.Visible)return Rectangle.Empty;int h=Math.Min(Height,Math.Max(26,(int)((long)Height*target.VerticalScroll.LargeChange/Math.Max(1,target.VerticalScroll.Maximum+1))));int top=(int)((long)Math.Max(0,Height-h)*target.VerticalScroll.Value/Math.Max(1,Maximum));return new Rectangle(4,top,Math.Max(2,Width-8),h);}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);Rectangle r=HandleRect();if(!r.IsEmpty)using(var b=new SolidBrush(dragging?Color.FromArgb(150,150,160):Color.FromArgb(92,92,102)))e.Graphics.FillRectangle(b,r);}
  void MoveTo(int y){var r=HandleRect();int value=(int)((long)Math.Max(0,y-grab)*Maximum/Math.Max(1,Height-r.Height));target.AutoScrollPosition=new Point(0,Math.Max(0,Math.Min(Maximum,value)));Invalidate();}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||!target.VerticalScroll.Visible)return;var r=HandleRect();grab=r.Contains(e.Location)?e.Y-r.Top:r.Height/2;dragging=true;Capture=true;MoveTo(e.Y);}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)MoveTo(e.Y);}
  protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;Invalidate();}
  protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){dragging=false;Invalidate();}}
  protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);target.AutoScrollPosition=new Point(0,Math.Max(0,Math.Min(Maximum,target.VerticalScroll.Value-e.Delta/120*48)));Invalidate();}
 }
}
public class BufferedPanel:Panel {public BufferedPanel(){BackColor=Color.FromArgb(17,17,20);SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}}
}
