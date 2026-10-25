using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
namespace Pr7Editor {
// Paint only visible rows without a native virtual ListView or native row objects.
public sealed class PlayerListControl : Control {
 public sealed class PlayerItems : IEnumerable {
  readonly List<object> values=new List<object>();readonly PlayerListControl owner;
  internal PlayerItems(PlayerListControl control){owner=control;}
  public int Count {get{return values.Count;}}
  public object this[int index] {get{return values[index];}}
  public void Clear(){values.Clear();owner.selected=-1;owner.scroll.Value=0;owner.UpdateScroll();}
  public void AddRange(object[] items){values.AddRange(items);owner.UpdateScroll();}
  internal int IndexOf(object value){return values.IndexOf(value);}
  public IEnumerator GetEnumerator(){return values.GetEnumerator();}
 }
 public PlayerItems Items {get;private set;}
 readonly VScrollBar scroll=new VScrollBar();int selected=-1,updateDepth,wheelRemainder;string prefix="";DateTime lastKey;
 int RowHeight {get{return Math.Max(20,Font.Height+5);}}
 int VisibleRows {get{return Math.Max(1,(ClientSize.Height-2)/RowHeight);}}
 public event EventHandler SelectedIndexChanged;
 public int SelectedIndex {get{return selected;}set{
  if(value < -1||value>=Items.Count)throw new ArgumentOutOfRangeException("value");
  if(value==selected)return;selected=value;EnsureVisible();Redraw();
  var handler=SelectedIndexChanged;if(handler!=null)handler(this,EventArgs.Empty);
 }}
 public object SelectedItem {get{return selected<0?null:Items[selected];}set{SelectedIndex=Items.IndexOf(value);}}
 public PlayerListControl(){Items=new PlayerItems(this);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);TabStop=true;AccessibleRole=AccessibleRole.List;scroll.Dock=DockStyle.Right;scroll.TabStop=false;scroll.ValueChanged+=(s,e)=>Redraw();Controls.Add(scroll);}
 public void BeginUpdate(){updateDepth++;}
 public void EndUpdate(){if(updateDepth>0)updateDepth--;if(updateDepth==0)Invalidate();}
 void Redraw(){if(updateDepth==0)Invalidate();}
 void UpdateScroll(){if(Items==null)return;int rows=VisibleRows;scroll.Value=Math.Min(scroll.Value,Math.Max(0,Items.Count-rows));scroll.LargeChange=rows;scroll.SmallChange=1;scroll.Maximum=Math.Max(0,Items.Count-1);scroll.Visible=Items.Count>rows;Redraw();}
 void ScrollTo(int row){scroll.Value=Math.Max(0,Math.Min(row,Math.Max(0,Items.Count-VisibleRows)));}
 void EnsureVisible(){if(selected<0)return;if(selected<scroll.Value)ScrollTo(selected);else if(selected>=scroll.Value+VisibleRows)ScrollTo(selected-VisibleRows+1);}
 protected override void OnResize(EventArgs e){base.OnResize(e);UpdateScroll();}
 protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);if(scroll!=null){UpdateScroll();EnsureVisible();}}
 protected override void OnPaint(PaintEventArgs e){
  e.Graphics.Clear(BackColor);int width=ClientSize.Width-(scroll.Visible?scroll.Width:0)-2;
  for(int i=scroll.Value;i<Items.Count;i++){int y=1+(i-scroll.Value)*RowHeight;if(y>=ClientSize.Height-1)break;var rect=new Rectangle(1,y,width,RowHeight);if(!e.ClipRectangle.IntersectsWith(rect))continue;bool active=i==selected;Color bg=active?Color.FromArgb(174,29,40):BackColor;
   using(var brush=new SolidBrush(bg))e.Graphics.FillRectangle(brush,rect);rect.X+=4;rect.Width-=5;
   TextRenderer.DrawText(e.Graphics,Convert.ToString(Items[i]),Font,rect,active?Color.White:ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
   if(active&&Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(2,y+1,Math.Max(0,width-2),RowHeight-2),Color.White,bg);
  }
  ControlPaint.DrawBorder(e.Graphics,ClientRectangle,BackColor.GetBrightness()<.4f?Color.FromArgb(65,65,73):Color.FromArgb(150,150,150),ButtonBorderStyle.Solid);
 }
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();if(e.Y<1||e.Y>=ClientSize.Height-1)return;int row=scroll.Value+(e.Y-1)/RowHeight;if(row<Items.Count)SelectedIndex=row;}
 protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);wheelRemainder+=e.Delta;int steps=wheelRemainder/SystemInformation.MouseWheelScrollDelta;wheelRemainder%=SystemInformation.MouseWheelScrollDelta;int lines=SystemInformation.MouseWheelScrollLines;ScrollTo(scroll.Value-steps*(lines<0?VisibleRows:lines));}
 protected override bool IsInputKey(Keys key){switch(key&Keys.KeyCode){case Keys.Up:case Keys.Down:case Keys.Home:case Keys.End:case Keys.PageUp:case Keys.PageDown:return true;}return base.IsInputKey(key);}
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(Items.Count==0)return;int next=selected;switch(e.KeyCode){case Keys.Up:next=Math.Max(0,selected-1);break;case Keys.Down:next=Math.Min(Items.Count-1,selected+1);break;case Keys.Home:next=0;break;case Keys.End:next=Items.Count-1;break;case Keys.PageUp:next=Math.Max(0,selected-VisibleRows);break;case Keys.PageDown:next=Math.Min(Items.Count-1,selected+VisibleRows);break;default:return;}SelectedIndex=next;e.Handled=true;e.SuppressKeyPress=true;}
 protected override void OnKeyPress(KeyPressEventArgs e){base.OnKeyPress(e);if(Char.IsControl(e.KeyChar)||Items.Count==0)return;DateTime now=DateTime.UtcNow;prefix=(now-lastKey).TotalSeconds>1?"":prefix;lastKey=now;prefix+=e.KeyChar;for(int n=0;n<Items.Count;n++){int i=(Math.Max(0,selected)+n)%Items.Count;if(CultureInfo.InvariantCulture.CompareInfo.IsPrefix(Convert.ToString(Items[i]),prefix,CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)){SelectedIndex=i;break;}}e.Handled=true;}
 protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Redraw();}
 protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Redraw();}
 protected override AccessibleObject CreateAccessibilityInstance(){return new PlayerListAccessible(this);}
 sealed class PlayerListAccessible : ControlAccessibleObject {
  readonly PlayerListControl list;
  internal PlayerListAccessible(PlayerListControl control):base(control){list=control;}
  public override string Value {get{return Convert.ToString(list.SelectedItem);}set{}}
  public override int GetChildCount(){return list.scroll.Visible?1:0;}
  public override AccessibleObject GetChild(int index){return index==0&&list.scroll.Visible?list.scroll.AccessibilityObject:null;}
 }
}
}
