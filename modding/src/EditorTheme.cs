using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Pr7Editor {
internal static class ThemePreference {
 internal static string PathName { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PR7","FIFA08Editor","theme.txt"); } }
 internal static bool Read(string path) { try { return !String.Equals(File.ReadAllText(path).Trim(),"light",StringComparison.OrdinalIgnoreCase); } catch(IOException) { return true; } catch(UnauthorizedAccessException) { return true; } }
 internal static void Write(string path,bool dark) { Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,dark?"dark":"light"); }
}

public sealed class EditorTabs : TabControl {
 Color surface=Color.FromArgb(12,12,14);
 public override Color BackColor {get{return surface;}set{surface=value;Invalidate();}}
 public EditorTabs() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);Padding=new Point(12,5); }
 protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(BackColor); }
 protected override void OnPaint(PaintEventArgs e) {
  e.Graphics.Clear(BackColor);
  for(int i=0;i<TabCount;i++) {
   var rect=GetTabRect(i);bool selected=i==SelectedIndex;
   using(var brush=new SolidBrush(selected?(BackColor.GetBrightness()<.4f?Color.FromArgb(25,25,29):Color.White):BackColor))e.Graphics.FillRectangle(brush,rect);
   TextRenderer.DrawText(e.Graphics,TabPages[i].Text,Font,rect,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
   if(selected)using(var pen=new Pen(Color.FromArgb(224,26,34),2))e.Graphics.DrawLine(pen,rect.Left+3,rect.Bottom-2,rect.Right-3,rect.Bottom-2);
  }
 }
}

internal static class EditorPalette {
 internal static void Apply(Control control,bool dark) {
  Color page=dark?Color.FromArgb(12,12,14):Color.FromArgb(244,246,250);
  Color field=dark?Color.FromArgb(23,23,27):Color.White;
  Color text=dark?Color.FromArgb(225,225,230):Color.FromArgb(25,27,32);
  control.BackColor=page;control.ForeColor=text;
  if(control is PictureBox)control.BackColor=dark?Color.FromArgb(8,8,10):Color.FromArgb(43,47,56);
  var box=control as TextBoxBase;
  if(box!=null) { box.BackColor=field;box.ForeColor=text;box.BorderStyle=BorderStyle.FixedSingle; }
  var list=control as ListBox;
  var players=control as PlayerListControl;if(players!=null){players.BackColor=field;players.ForeColor=text;players.Invalidate();}
  if(list!=null) {
   list.BackColor=field;list.ForeColor=text;list.BorderStyle=BorderStyle.FixedSingle;
   if(list.DrawMode!=DrawMode.OwnerDrawFixed) { list.DrawMode=DrawMode.OwnerDrawFixed;list.ItemHeight=Math.Max(20,list.Font.Height+4);list.DrawItem+=DrawListItem; }
  }
  var combo=control as ComboBox;
  if(combo!=null) {
   combo.BackColor=field;combo.ForeColor=text;combo.FlatStyle=FlatStyle.Flat;
   if(combo.DrawMode!=DrawMode.OwnerDrawFixed) { combo.DrawMode=DrawMode.OwnerDrawFixed;combo.DrawItem+=DrawComboItem; }
  }
  var button=control as Button;
  if(button!=null) {
   button.UseVisualStyleBackColor=false;button.FlatStyle=FlatStyle.Flat;
   button.BackColor=dark?Color.FromArgb(28,28,33):Color.FromArgb(236,239,245);
   button.ForeColor=text;button.FlatAppearance.BorderColor=dark?Color.FromArgb(65,65,73):Color.FromArgb(185,190,200);
   button.FlatAppearance.MouseOverBackColor=dark?Color.FromArgb(43,34,38):Color.FromArgb(250,225,228);
   button.FlatAppearance.MouseDownBackColor=dark?Color.FromArgb(76,28,34):Color.FromArgb(240,201,207);
  }
  var grid=control as DataGridView;
  if(grid!=null) {
   grid.BackgroundColor=field;grid.GridColor=dark?Color.FromArgb(51,51,58):Color.FromArgb(205,208,215);
   grid.EnableHeadersVisualStyles=false;grid.BorderStyle=BorderStyle.FixedSingle;
   grid.DefaultCellStyle.BackColor=field;grid.DefaultCellStyle.ForeColor=text;
   grid.DefaultCellStyle.SelectionBackColor=dark?Color.FromArgb(112,25,34):Color.FromArgb(174,29,40);grid.DefaultCellStyle.SelectionForeColor=Color.White;
   grid.AlternatingRowsDefaultCellStyle.BackColor=dark?Color.FromArgb(27,27,31):Color.FromArgb(248,249,252);
   grid.ColumnHeadersDefaultCellStyle.BackColor=dark?Color.FromArgb(34,34,39):Color.FromArgb(232,235,241);
   grid.ColumnHeadersDefaultCellStyle.ForeColor=text;
   grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=grid.ColumnHeadersDefaultCellStyle.BackColor;
   grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=text;
   grid.RowHeadersDefaultCellStyle=grid.ColumnHeadersDefaultCellStyle.Clone();
   if(grid.EditingControl!=null)Apply(grid.EditingControl,dark);
   // Scrollbars and editing panels are native children; do not recolor them as
   // ordinary panels, which would override the cell's selected colors.
   grid.Invalidate();return;
  }
  foreach(Control child in control.Controls)Apply(child,dark);
  control.Invalidate();
 }
 static void DrawListItem(object sender,DrawItemEventArgs e) {
  var list=(ListBox)sender;DrawItem(e,list,e.Index>=0&&e.Index<list.Items.Count?Convert.ToString(list.Items[e.Index]):"");
 }
 static void DrawComboItem(object sender,DrawItemEventArgs e) {
  var combo=(ComboBox)sender;DrawItem(e,combo,e.Index>=0&&e.Index<combo.Items.Count?Convert.ToString(combo.Items[e.Index]):combo.Text);
 }
 static void DrawItem(DrawItemEventArgs e,Control owner,string text) {
  bool selected=(e.State&DrawItemState.Selected)!=0;
  Color background=selected?Color.FromArgb(174,29,40):owner.BackColor;
  using(var brush=new SolidBrush(background))e.Graphics.FillRectangle(brush,e.Bounds);
  var rect=e.Bounds;rect.X+=3;rect.Width-=3;
  TextRenderer.DrawText(e.Graphics,text,owner.Font,rect,owner.Enabled?(selected?Color.White:owner.ForeColor):SystemColors.GrayText,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
  if((e.State&DrawItemState.Focus)!=0)ControlPaint.DrawFocusRectangle(e.Graphics,e.Bounds,owner.ForeColor,background);
 }
}

public class EditorComboCell : DataGridViewComboBoxCell {
 protected override void Paint(Graphics graphics,Rectangle clipBounds,Rectangle cellBounds,int rowIndex,DataGridViewElementStates state,object value,object formattedValue,string errorText,DataGridViewCellStyle style,DataGridViewAdvancedBorderStyle borders,DataGridViewPaintParts parts) {
  base.Paint(graphics,clipBounds,cellBounds,rowIndex,state,value,formattedValue,errorText,style,borders,parts);
  if((parts&DataGridViewPaintParts.ContentBackground)==0)return;
  bool selected=(state&DataGridViewElementStates.Selected)!=0;
  Color bg=selected?style.SelectionBackColor:style.BackColor,fg=selected?style.SelectionForeColor:style.ForeColor;
  var button=new Rectangle(cellBounds.Right-19,cellBounds.Top+1,17,cellBounds.Height-3);
  using(var brush=new SolidBrush(bg))graphics.FillRectangle(brush,button);
  int x=button.Left+5,y=button.Top+button.Height/2-1;
  using(var pen=new Pen(fg,1.4f))graphics.DrawLines(pen,new[]{new Point(x,y),new Point(x+3,y+3),new Point(x+6,y)});
 }
}

public sealed partial class EditorForm {
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 Button themeButton;
 public bool DarkTheme {get;private set;}
 void InitializeTheme(FlowLayoutPanel toolbar) {
  themeButton=new ThemeIconButton{Width=40,Height=28};themeButton.Click+=(s,e)=>Run(ToggleTheme);toolbar.Controls.Add(themeButton);
  SetDarkTheme(ThemePreference.Read(ThemePreference.PathName));
 }
 void ToggleTheme() {
  SetDarkTheme(!DarkTheme);
  ThemePreference.Write(ThemePreference.PathName,DarkTheme);
 }
 public void SetDarkTheme(bool dark) {
  DarkTheme=dark;SuspendLayout();EditorPalette.Apply(this,dark);
  UpdateThemeButton();
  Status.BackColor=dark?Color.FromArgb(20,20,24):Color.FromArgb(225,231,240);
  ResumeLayout(true);Invalidate(true);if(IsHandleCreated)ApplyTitleTheme();
 }
 void ApplyTitleTheme() {
  try { int value=DarkTheme?1:0;DwmSetWindowAttribute(Handle,20,ref value,4); } catch(DllNotFoundException) { } catch(EntryPointNotFoundException) { }
 }
 protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e);ApplyTitleTheme(); }
 public void ValidateThemeUi() {
  var rating=Fields.Rows.Cast<DataGridViewRow>().Single(r=>Convert.ToString(r.Tag)=="number:overallrating");
  object value=rating.Cells[1].Value;rating.Cells[1].Value="92";
  byte[] before=State.Db.Serialize();var pending=State.Pending.ToDictionary(p=>p.Key,p=>p.Value);
  bool wasDark=DarkTheme;
  SetDarkTheme(!wasDark);Application.DoEvents();SetDarkTheme(wasDark);Application.DoEvents();
  if(Convert.ToString(rating.Cells[1].Value)!="92"||!before.SequenceEqual(State.Db.Serialize())||pending.Count!=State.Pending.Count||pending.Any(p=>!State.Pending.ContainsKey(p.Key)||!p.Value.SequenceEqual(State.Pending[p.Key])))throw new Exception("Tema: se perdieron ediciones al alternar.");
  rating.Cells[1].Value=value;
 }
}
}
