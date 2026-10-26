using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Web.Script.Serialization;

namespace FIFA08Launcher
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--apply-update") { Environment.ExitCode = PortUpdater.RunHelper(args[1]); return; }
            PortUpdater.Interactive = args.Length == 0;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 2 && args[0] == "--startup-plan")
            {
                using (LauncherForm form = new LauncherForm())
                    File.WriteAllText(args[1], form.BuildLaunchArguments(true));
                return;
            }
            if (args.Length >= 2 && args[0] == "--preview")
            {
                using (LauncherForm form = new LauncherForm())
                {
                    if (args.Length == 4 || args.Length == 5)
                    {
                        form.Size = new Size(int.Parse(args[2]), int.Parse(args[3]));
                    }
                    if(args.Length==3||args.Length==5)form.SetUiEnglish(args[args.Length-1]=="en",false);
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-20000, -20000);
                    form.ShowInTaskbar = false;
                    form.Show();
                    Application.DoEvents();
                    using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                        bitmap.Save(args[1]);
                    }
                }
                return;
            }
            Application.Run(new LauncherForm());
        }
    }

    public static class UIHelper
    {
        public static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            int d = radius * 2;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            Rectangle arc = new Rectangle(rect.X, rect.Y, d, d);
            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - d;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - d;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.UpdateStyles();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(this.BackColor))
            {
                e.Graphics.FillRectangle(b, this.ClientRectangle);
            }
        }
    }

    public class RoundedButton : Button
    {
        private bool isHovered = false;
        private bool isPressed = false;
        public int CornerRadius { get; set; }

        public RoundedButton()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.CornerRadius = 8;
            this.Cursor = Cursors.Hand;
            this.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            this.ForeColor = Color.White;
            this.BackColor = Color.FromArgb(224, 26, 34);
        }

        protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { isHovered = false; isPressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs mevent) { isPressed = true; Invalidate(); base.OnMouseDown(mevent); }
        protected override void OnMouseUp(MouseEventArgs mevent) { isPressed = false; Invalidate(); base.OnMouseUp(mevent); }

        protected override void OnPaintBackground(PaintEventArgs pevent) { }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color parentBg = this.Parent != null ? this.Parent.BackColor : Color.FromArgb(14, 14, 15);
            using (SolidBrush pb = new SolidBrush(parentBg))
            {
                g.FillRectangle(pb, this.ClientRectangle);
            }

            Color bg = !this.Enabled ? Color.FromArgb(70, 70, 75) :
                       isPressed ? Color.FromArgb(170, 16, 22) :
                       isHovered ? Color.FromArgb(238, 38, 46) : Color.FromArgb(224, 26, 34);

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (GraphicsPath path = UIHelper.GetRoundedPath(rect, CornerRadius))
            {
                using (SolidBrush b = new SolidBrush(bg))
                {
                    g.FillPath(b, path);
                }
                if (this.Enabled && !isPressed)
                {
                    using (Pen p = new Pen(Color.FromArgb(255, 75, 85), 1.2f))
                    {
                        g.DrawPath(p, path);
                    }
                }
            }

            TextRenderer.DrawText(g, this.Text, this.Font, rect, this.ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    public class RoundedBadgeLabel : Control
    {
        public int CornerRadius { get; set; }

        public RoundedBadgeLabel()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.CornerRadius = 6;
            this.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            this.ForeColor = Color.FromArgb(215, 215, 220);
            this.BackColor = Color.FromArgb(22, 22, 24);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color parentBg = this.Parent != null ? this.Parent.BackColor : Color.FromArgb(14, 14, 15);
            using (SolidBrush pb = new SolidBrush(parentBg))
            {
                g.FillRectangle(pb, this.ClientRectangle);
            }

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (GraphicsPath path = UIHelper.GetRoundedPath(rect, CornerRadius))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(22, 22, 24)))
                {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(Color.FromArgb(46, 46, 50), 1.2f))
                {
                    g.DrawPath(p, path);
                }
            }

            int dotSize = 6;
            int dotY = (this.Height - dotSize) / 2;
            using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(50, 205, 50)))
            {
                g.FillEllipse(dotBrush, 10, dotY, dotSize, dotSize);
            }

            Rectangle textRect = new Rectangle(22, 0, this.Width - 26, this.Height);
            TextRenderer.DrawText(g, this.Text, this.Font, textRect, this.ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    // Keep native ComboBox input, capture, accessibility and popup ownership.
    // The previous ToolStrip/ListBox popup stole capture during MouseDown and
    // could close/reopen or select on the opening mouse release.
    public class RoundedComboBox : ComboBox
    {
        private bool isHovered;
        private PopupBorderWindow popupBorder;
        private IntPtr popupBackgroundBrush;
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateSolidBrush(int color);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")]
        private static extern int SetBkColor(IntPtr dc, int color);
        [DllImport("gdi32.dll")]
        private static extern int SetTextColor(IntPtr dc, int color);
        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hwnd, out NativeRect rect);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct PaintStruct
        {
            public IntPtr Hdc;
            public int Erase;
            public NativeRect Rect;
            public int Restore, IncUpdate;
            public int R0, R1, R2, R3, R4, R5, R6, R7;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct ComboInfo
        {
            public int Size;
            public NativeRect Item, Button;
            public int ButtonState;
            public IntPtr Combo, Edit, List;
        }
        [DllImport("user32.dll")]
        private static extern IntPtr BeginPaint(IntPtr hwnd, out PaintStruct paint);
        [DllImport("user32.dll")]
        private static extern bool EndPaint(IntPtr hwnd, ref PaintStruct paint);
        [DllImport("user32.dll")]
        private static extern bool GetComboBoxInfo(IntPtr hwnd, ref ComboInfo info);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string subApp, string subId);
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

        // Keep the native list (including scrolling/capture), but remove its
        // accent-colored theme border. Paint only its non-client outline.
        private sealed class PopupBorderWindow : NativeWindow
        {
            public PopupBorderWindow(IntPtr hwnd) { AssignHandle(hwnd); }
            protected override void WndProc(ref Message m)
            {
                // The popup is a separate native window. Its initial erase
                // must be dark too, before owner-drawn rows are available.
                if (m.Msg == 0x0014 && m.WParam != IntPtr.Zero)
                {
                    NativeRect client;
                    if (GetClientRect(Handle, out client))
                    {
                        using (Graphics g = Graphics.FromHdc(m.WParam))
                        using (SolidBrush background = new SolidBrush(Color.FromArgb(22, 22, 25)))
                            g.FillRectangle(background, client.Left, client.Top,
                                client.Right - client.Left, client.Bottom - client.Top);
                        m.Result = new IntPtr(1);
                        return;
                    }
                }
                if(m.Msg==0x000F){
                    PaintStruct paint;IntPtr popupDC=BeginPaint(Handle,out paint);
                    try{NativeRect client;if(popupDC!=IntPtr.Zero&&GetClientRect(Handle,out client)&&client.Right>0&&client.Bottom>0){
                        using(Graphics target=Graphics.FromHdc(popupDC))
                        using(BufferedGraphics buffer=BufferedGraphicsManager.Current.Allocate(target,new Rectangle(0,0,client.Right,client.Bottom))){
                            buffer.Graphics.Clear(Color.FromArgb(22,22,25));IntPtr offscreen=buffer.Graphics.GetHdc();
                            try{Message print=Message.Create(Handle,0x0318,offscreen,new IntPtr(0x1C));base.WndProc(ref print);}
                            finally{buffer.Graphics.ReleaseHdc(offscreen);}buffer.Render(target);
                        }
                    }}finally{EndPaint(Handle,ref paint);}m.Result=IntPtr.Zero;return;
                }
                base.WndProc(ref m);
                if (m.Msg != 0x0085 || Handle == IntPtr.Zero) return; // WM_NCPAINT
                NativeRect rect;
                if (!GetWindowRect(Handle, out rect)) return;
                IntPtr dc = GetWindowDC(Handle);
                if (dc == IntPtr.Zero) return;
                try
                {
                    using (Graphics g = Graphics.FromHdc(dc))
                    using (Pen border = new Pen(Color.FromArgb(46, 46, 52)))
                    {
                        int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
                        // Native listbox borders may include a two-pixel edge.
                        for (int inset = 0; inset < 2; inset++)
                            if (width > inset * 2 && height > inset * 2)
                                g.DrawRectangle(border, inset, inset,
                                    width - 1 - inset * 2, height - 1 - inset * 2);
                    }
                }
                finally { ReleaseDC(Handle, dc); }
            }
        }

        private void AttachPopupBorder()
        {
            ComboInfo info = new ComboInfo();
            info.Size = Marshal.SizeOf(typeof(ComboInfo));
            if (!GetComboBoxInfo(Handle, ref info) || info.List == IntPtr.Zero) return;
            if (popupBorder == null || popupBorder.Handle != info.List)
            {
                // Reapplying the theme every time the list opens causes an
                // unnecessary theme-change repaint during the show animation.
                SetWindowTheme(info.List, "", "");
                if (popupBorder != null) popupBorder.ReleaseHandle();
                popupBorder = new PopupBorderWindow(info.List);
            }
        }

        public int CornerRadius { get; set; }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        public RoundedComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            BackColor = Color.FromArgb(22, 22, 25);
            ForeColor = Color.White;
            CornerRadius = 6;
            ItemHeight = 26;
            MaxDropDownItems = 10;
            IntegralHeight = true;
        }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Cursor=Enabled?Cursors.Hand:Cursors.Default; Invalidate(); }
        private void SizeSelectionField()
        {
            // CB_SETITEMHEIGHT(-1) sizes the closed selection separately from
            // the 26 px popup rows, preserving the compact launcher layout.
            if (IsHandleCreated)
                SendMessage(Handle, 0x0153, new IntPtr(-1), new IntPtr(Font.Height + 4));
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetWindowTheme(Handle, "", "");
            SizeSelectionField();
            AttachPopupBorder();
        }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (popupBorder != null) { popupBorder.ReleaseHandle(); popupBorder = null; }
            if (popupBackgroundBrush != IntPtr.Zero)
            {
                DeleteObject(popupBackgroundBrush);
                popupBackgroundBrush = IntPtr.Zero;
            }
            base.OnHandleDestroyed(e);
        }
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            SizeSelectionField();
        }
        protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { isHovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnDropDown(EventArgs e) { AttachPopupBorder(); base.OnDropDown(e); Invalidate(); }
        protected override void OnDropDownClosed(EventArgs e) { base.OnDropDownClosed(e); Invalidate(); }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if ((e.State & DrawItemState.ComboBoxEdit) != 0)
            {
                PaintFrame(e.Graphics);
                return;
            }
            if (e.Index < 0 || e.Index >= Items.Count) return;
            Color background = (e.State & DrawItemState.Selected) != 0
                ? Color.FromArgb(224, 26, 34) : BackColor;
            using (SolidBrush brush = new SolidBrush(background))
                e.Graphics.FillRectangle(brush, e.Bounds);
            Rectangle text = new Rectangle(e.Bounds.X + 12, e.Bounds.Y,
                Math.Max(0, e.Bounds.Width - 16), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, text,
                Enabled ? ForeColor : SystemColors.GrayText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0134) // WM_CTLCOLORLISTBOX: native popup's background.
            {
                int background = ColorTranslator.ToWin32(Color.FromArgb(22, 22, 25));
                SetBkColor(m.WParam, background);
                SetTextColor(m.WParam, ColorTranslator.ToWin32(ForeColor));
                if (popupBackgroundBrush == IntPtr.Zero)
                    popupBackgroundBrush = CreateSolidBrush(background);
                if (popupBackgroundBrush != IntPtr.Zero)
                {
                    m.Result = popupBackgroundBrush;
                    return;
                }
            }
            if (m.Msg == 0x0014) // WM_ERASEBKGND: the buffer covers the whole client.
            {
                m.Result = new IntPtr(1);
                return;
            }
            if (m.Msg == 0x000F) // WM_PAINT: one buffered pass, no native underpaint.
            {
                PaintStruct paint;
                IntPtr dc = BeginPaint(Handle, out paint);
                try
                {
                    if (dc != IntPtr.Zero && Width > 1 && Height > 1)
                    {
                        using (Graphics target = Graphics.FromHdc(dc))
                        using (BufferedGraphics buffer = BufferedGraphicsManager.Current.Allocate(target, ClientRectangle))
                        {
                            PaintFrame(buffer.Graphics);
                            buffer.Render(target);
                        }
                    }
                }
                finally { EndPaint(Handle, ref paint); }
                m.Result = IntPtr.Zero;
                return;
            }
            if ((m.Msg == 0x0318 || m.Msg == 0x0317) && m.WParam != IntPtr.Zero)
            {
                using (Graphics graphics = Graphics.FromHdc(m.WParam)) PaintFrame(graphics);
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        private void PaintFrame(Graphics g)
        {
            if (Width < 2 || Height < 2) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color parentBg = this.Parent != null ? this.Parent.BackColor : Color.FromArgb(14, 14, 15);
            using (SolidBrush pb = new SolidBrush(parentBg))
            {
                g.FillRectangle(pb, this.ClientRectangle);
            }

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (GraphicsPath path = UIHelper.GetRoundedPath(rect, CornerRadius))
            {
                Color boxBg = !Enabled ? Color.FromArgb(17,17,19) : DroppedDown ? Color.FromArgb(28, 28, 32) :
                              isHovered ? Color.FromArgb(26, 26, 30) : Color.FromArgb(22, 22, 25);
                using (SolidBrush b = new SolidBrush(boxBg))
                {
                    g.FillPath(b, path);
                }

                Color borderColor = !Enabled ? Color.FromArgb(32,32,36) : (DroppedDown || this.Focused) ? Color.FromArgb(224, 26, 34) :
                                    isHovered ? Color.FromArgb(70, 70, 78) : Color.FromArgb(46, 46, 52);
                using (Pen p = new Pen(borderColor, 1.2f))
                {
                    g.DrawPath(p, path);
                }
            }

            string text = (SelectedIndex >= 0 && SelectedIndex < Items.Count) ? GetItemText(Items[SelectedIndex]) : "";
            Rectangle textRect = new Rectangle(12, 0, this.Width - 36, this.Height);
            TextRenderer.DrawText(g, text, this.Font, textRect, Enabled ? this.ForeColor : Color.FromArgb(91,91,99),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            // Clean custom chevron
            int arrowX = this.Width - 18;
            int arrowY = this.Height / 2 - 2;
            Point[] chevron;
            if (DroppedDown)
            {
                chevron = new Point[] {
                    new Point(arrowX, arrowY + 4),
                    new Point(arrowX + 4, arrowY),
                    new Point(arrowX + 8, arrowY + 4)
                };
            }
            else
            {
                chevron = new Point[] {
                    new Point(arrowX, arrowY),
                    new Point(arrowX + 4, arrowY + 4),
                    new Point(arrowX + 8, arrowY)
                };
            }
            using (Pen arrowPen = new Pen(!Enabled ? Color.FromArgb(65,65,72) : (isHovered || DroppedDown) ? Color.White : Color.FromArgb(160, 160, 168), 1.6f))
            {
                arrowPen.StartCap = LineCap.Round;
                arrowPen.EndCap = LineCap.Round;
                g.DrawLines(arrowPen, chevron);
            }
        }
    }

    public class RedCheckBox : CheckBox
    {
        private static readonly Color RedColor = Color.FromArgb(224, 26, 34);
        private static readonly Color BorderColor = Color.FromArgb(70, 70, 75);
        private static readonly Color BoxBg = Color.FromArgb(24, 24, 24);

        public RedCheckBox()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.Cursor = Cursors.Hand;
            this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            this.ForeColor = Color.FromArgb(220, 220, 225);
            this.BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            Color bg = this.Parent != null ? this.Parent.BackColor : Color.FromArgb(14, 14, 15);
            using (SolidBrush bgBrush = new SolidBrush(bg))
            {
                g.FillRectangle(bgBrush, this.ClientRectangle);
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;

            int boxSize = 17;
            int boxY = (this.Height - boxSize) / 2;
            Rectangle boxRect = new Rectangle(0, boxY, boxSize, boxSize);

            using (GraphicsPath path = UIHelper.GetRoundedPath(boxRect, 4))
            {
                using (SolidBrush b = new SolidBrush(this.Checked ? RedColor : BoxBg))
                {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(this.Checked ? RedColor : BorderColor, 1.4f))
                {
                    g.DrawPath(p, path);
                }
            }

            if (this.Checked)
            {
                using (Pen checkPen = new Pen(Color.White, 2.1f))
                {
                    checkPen.StartCap = LineCap.Round;
                    checkPen.EndCap = LineCap.Round;
                    g.DrawLines(checkPen, new Point[] {
                        new Point(boxRect.Left + 3, boxRect.Top + 8),
                        new Point(boxRect.Left + 7, boxRect.Top + 12),
                        new Point(boxRect.Left + 13, boxRect.Top + 4)
                    });
                }
            }

            Rectangle textRect = new Rectangle(boxSize + 8, 0, this.Width - (boxSize + 8), this.Height);
            TextRenderer.DrawText(g, this.Text, this.Font, textRect, this.ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    public partial class LauncherForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        [DllImport("dxgi.dll")]
        private static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr ppFactory);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DXGI_ADAPTER_DESC1
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Description;
            public uint VendorId;
            public uint DeviceId;
            public uint SubSysId;
            public uint Revision;
            public UIntPtr DedicatedVideoMemory;
            public UIntPtr DedicatedSystemMemory;
            public UIntPtr SharedSystemMemory;
            public long AdapterLuid;
            public uint Flags;
        }

        [ComImport]
        [Guid("770aae78-f26f-4dba-a829-253c83d1b387")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IDXGIFactory1
        {
            void SetPrivateData();
            void SetPrivateDataInterface();
            void GetPrivateData();
            void GetParent();
            [PreserveSig] int EnumAdapters(uint Adapter, out IntPtr ppAdapter);
            void MakeWindowAssociation();
            void GetWindowAssociation();
            void CreateSwapChain();
            void CreateSoftwareAdapter();
            [PreserveSig] int EnumAdapters1(uint Adapter, out IntPtr ppAdapter);
            [PreserveSig] int IsCurrent();
        }

        [ComImport]
        [Guid("29038f61-3839-4626-91fd-086879011a05")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IDXGIAdapter1
        {
            void SetPrivateData();
            void SetPrivateDataInterface();
            void GetPrivateData();
            void GetParent();
            void EnumOutputs();
            [PreserveSig] int GetDesc1(out DXGI_ADAPTER_DESC1 pDesc);
        }

        private Image coverImage;
        private DoubleBufferedPanel panelCover;
        private DoubleBufferedPanel panelControls;
        private DoubleBufferedPanel bottomPanel;

        private class SettingRow
        {
            public Label Label;
            public RoundedComboBox ComboBox;
        }
        private List<SettingRow> settingRows = new List<SettingRow>();

        private RoundedComboBox cbGraphicsApi;
        private RoundedComboBox cbResolution;
        private RoundedComboBox cbDisplayMode;
        private RoundedComboBox cbFrameLimit;
        private RoundedComboBox cbAntiAliasing;
        private RoundedComboBox cbTextLanguage;
        private RoundedComboBox cbCommentaryLanguage;

        private RedCheckBox chkShowFps;
        private RedCheckBox chkVSync;

        private RoundedButton btnPlay;
        private Label lblStatus;
        private RoundedBadgeLabel lblGpuInfo;
        private Timer gameMonitorTimer;
        private Process runningGameProcess;

        private readonly string settingsFile;
        private readonly string rootDir;
        private string detectedGpuName = "Adaptador Direct3D 12";
        private int detectedGpuIndex = -1;
        private int currentContentX = 28;
        private int currentHeaderY = 18;

        public LauncherForm()
        {
            rootDir = Path.GetDirectoryName(typeof(LauncherForm).Assembly.Location);
            if (string.IsNullOrEmpty(rootDir)) rootDir = AppDomain.CurrentDomain.BaseDirectory;
            if(File.Exists(Path.Combine(rootDir,"bin","probar-fifa08.ps1")))rootDir=Path.Combine(rootDir,"bin");
            settingsFile = Path.Combine(rootDir, "launcher-settings.json");

            DetectGpu();
            InitializeComponent();
            ApplyWindowsTheme();
            PopulateLanguages();
            LoadSettings();
            InitializeFeatures();
            InitializeLanguage();ModernInit();
        }

        private void ApplyWindowsTheme()
        {
            try
            {
                bool isDark = true;
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    {
                        if (key != null)
                        {
                            object val = key.GetValue("AppsUseLightTheme");
                            if (val != null && (int)val == 1) isDark = false;
                            object sysVal = key.GetValue("SystemUsesLightTheme");
                            if (sysVal != null && (int)sysVal == 1 && val == null) isDark = false;
                        }
                    }
                }
                catch { }

                int darkModeVal = isDark ? 1 : 0;
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeVal, sizeof(int));
                DwmSetWindowAttribute(this.Handle, 19, ref darkModeVal, sizeof(int));

                int cornerPreference = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(this.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

                if (isDark)
                {
                    int captionColor = 0x000F0E0E;
                    int textColor = 0x00FFFFFF;
                    int borderColor = 0x00262323;

                    DwmSetWindowAttribute(this.Handle, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
                    DwmSetWindowAttribute(this.Handle, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));
                    DwmSetWindowAttribute(this.Handle, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));
                }
            }
            catch { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyWindowsTheme();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyWindowsTheme();
        }

        private void DetectGpu()
        {
            try
            {
                Guid factoryGuid = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
                IntPtr factoryPtr;
                if (CreateDXGIFactory1(ref factoryGuid, out factoryPtr) != 0) return;
                IDXGIFactory1 factory = (IDXGIFactory1)Marshal.GetObjectForIUnknown(factoryPtr);
                ulong maxVram = 0;
                for (uint i = 0; ; i++)
                {
                    IntPtr adapterPtr;
                    if (factory.EnumAdapters1(i, out adapterPtr) != 0) break;
                    IDXGIAdapter1 adapter = (IDXGIAdapter1)Marshal.GetObjectForIUnknown(adapterPtr);
                    DXGI_ADAPTER_DESC1 desc;
                    adapter.GetDesc1(out desc);
                    ulong vram = (ulong)desc.DedicatedVideoMemory;
                    if ((desc.Flags & 2) == 0 && (detectedGpuIndex == -1 || vram > maxVram))
                    {
                        maxVram = vram;
                        detectedGpuIndex = (int)i;
                        detectedGpuName = desc.Description + " (" + (vram / (1024 * 1024)) + " MB)";
                    }
                    Marshal.Release(adapterPtr);
                }
                Marshal.Release(factoryPtr);
            }
            catch
            {
                detectedGpuName = "Adaptador Direct3D 12";
            }
        }

        private void InitializeComponent()
        {
            this.Size = new Size(950, 720);
            this.MinimumSize = new Size(880, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "FIFA 08 PC PORT by PR7";
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(14, 14, 15);

            string iconPath = Path.Combine(rootDir, "launcher-assets", "icon.ico");
            if (File.Exists(iconPath))
            {
                try { this.Icon = new Icon(iconPath); } catch { }
            }

            string coverPath = Path.Combine(rootDir, "launcher-assets", "cover.jpg");
            if (File.Exists(coverPath))
            {
                try { coverImage = Image.FromFile(coverPath); } catch { }
            }

            panelCover = new DoubleBufferedPanel();
            panelCover.BackColor = Color.FromArgb(10, 10, 10);
            panelCover.Paint += PanelCover_Paint;
            this.Controls.Add(panelCover);

            panelControls = new DoubleBufferedPanel();
            panelControls.BackColor = Color.FromArgb(14, 14, 15);
            panelControls.Paint += PanelControls_Paint;
            this.Controls.Add(panelControls);

            lblGpuInfo = new RoundedBadgeLabel();
            lblGpuInfo.Text = detectedGpuName;
            panelControls.Controls.Add(lblGpuInfo);

            // 1. API Gráfica
            cbGraphicsApi = new RoundedComboBox();
            cbGraphicsApi.Items.AddRange(new object[] {
                "Vulkan",
                "Direct3D 12"
            });
            cbGraphicsApi.SelectedIndex = 0;
            AddSettingRow("API Gr\u00e1fica", cbGraphicsApi);

            // 2. Resolución
            cbResolution = new RoundedComboBox();
            cbResolution.Items.AddRange(new object[] {
                "720p - Nativo",
                "1080p - Nativo",
                "2K - Nativo",
                "4K - Nativo",
                "1080p - FSR (720p)",
                "2K - FSR (1080p)",
                "4K - FSR (1440p)"
            });
            cbResolution.SelectedIndex = 1;
            AddSettingRow("Resoluci\u00f3n de pantalla", cbResolution);

            // 3. Modo de Pantalla
            cbDisplayMode = new RoundedComboBox();
            cbDisplayMode.Items.AddRange(new object[] {
                "Ventana",
                "Pantalla completa"
            });
            cbDisplayMode.SelectedIndex = 0;
            AddSettingRow("Modo de visualizaci\u00f3n", cbDisplayMode);

            // 4. FPS - All unlocked options, without "(experimental)", with "Sin límite de FPS" at the end
            cbFrameLimit = new RoundedComboBox();
            cbFrameLimit.Items.AddRange(new object[] {
                "Original",
                "30 FPS",
                "60 FPS",
                "90 FPS",
                "100 FPS",
                "120 FPS",
                "144 FPS",
                "165 FPS",
                "240 FPS",
                "Sin l\u00edmite de FPS"
            });
            cbFrameLimit.SelectedIndex = 2; // Default 60 FPS
            AddSettingRow("Tasa de cuadros (FPS)", cbFrameLimit);

            // 5. Anti-Aliasing
            cbAntiAliasing = new RoundedComboBox();
            cbAntiAliasing.Items.AddRange(new object[] {
                "Desactivado",
                "FXAA (Recomendado - Suave)",
                "FXAA Extremo"
            });
            cbAntiAliasing.SelectedIndex = 0;
            AddSettingRow("Filtro de imagen", cbAntiAliasing);

            // 6. Idioma de textos
            cbTextLanguage = new RoundedComboBox();
            cbTextLanguage.Items.AddRange(new object[] {
                "Espa\u00f1ol",
                "English"
            });
            cbTextLanguage.SelectedIndex = 0;
            AddSettingRow("Idioma de textos e interfaz", cbTextLanguage);

            // 7. Idioma de comentarios
            cbCommentaryLanguage = new RoundedComboBox();
            cbCommentaryLanguage.Items.AddRange(new object[] {
                "Espa\u00f1ol (Latinoam\u00e9rica)",
                "English (US)"
            });
            cbCommentaryLanguage.SelectedIndex = 0;
            AddSettingRow("Idioma de comentarios / relator", cbCommentaryLanguage);

            // 8. Checkboxes
            chkShowFps = new RedCheckBox();
            chkShowFps.Text = "Mostrar FPS";
            chkShowFps.Checked = false;
            panelControls.Controls.Add(chkShowFps);

            chkVSync = new RedCheckBox();
            chkVSync.Text = "VSync (Sincronizaci\u00f3n)";
            chkVSync.Checked = false;
            panelControls.Controls.Add(chkVSync);

            // 9. Panel Inferior
            bottomPanel = new DoubleBufferedPanel();
            bottomPanel.BackColor = Color.FromArgb(14, 14, 15);
            bottomPanel.Paint += BottomPanel_Paint;
            panelControls.Controls.Add(bottomPanel);

            lblStatus = new Label();
            lblStatus.Text = "F10 durante el partido para volver a Windows  \u2022  Alt+Enter para alternar pantalla completa";
            lblStatus.Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
            lblStatus.ForeColor = Color.FromArgb(140, 140, 145);
            lblStatus.BackColor = Color.Transparent;
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            bottomPanel.Controls.Add(lblStatus);

            btnPlay = new RoundedButton();
            btnPlay.Text = PortUi.T("JUGAR");
            btnPlay.Click += BtnPlay_Click;
            bottomPanel.Controls.Add(btnPlay);

            gameMonitorTimer = new Timer();
            gameMonitorTimer.Interval = 1000;
            gameMonitorTimer.Tick += GameMonitorTimer_Tick;

            this.Resize += (s, e) => UpdateResponsiveLayout();
            UpdateResponsiveLayout();
        }

        private void AddSettingRow(string labelText, RoundedComboBox comboBox)
        {
            Label lbl = new Label();
            lbl.Text = labelText;
            lbl.Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
            lbl.ForeColor = Color.FromArgb(170, 170, 175);
            lbl.BackColor = Color.FromArgb(14, 14, 15);
            panelControls.Controls.Add(lbl);
            panelControls.Controls.Add(comboBox);

            settingRows.Add(new SettingRow { Label = lbl, ComboBox = comboBox });
        }

        private void UpdateResponsiveLayout()
        {
            if (modernReady) { ModernLayout(); return; }
            if (this.ClientSize.Width <= 0 || this.ClientSize.Height <= 0) return;

            this.SuspendLayout();
            panelControls.SuspendLayout();
            bottomPanel.SuspendLayout();

            int coverW = Math.Max(380, (int)(this.ClientSize.Width * 0.42f));

            panelCover.Location = new Point(0, 0);
            panelCover.Size = new Size(coverW, this.ClientSize.Height);

            int controlsX = coverW;
            int controlsW = this.ClientSize.Width - coverW;
            panelControls.Location = new Point(controlsX, 0);
            panelControls.Size = new Size(controlsW, this.ClientSize.Height);

            // Responsive content width: balanced and comfortable, not overly long and not too narrow
            int contentWidth = Math.Min(600, controlsW - 60);
            contentWidth = Math.Max(420, contentWidth);
            currentContentX = (controlsW - contentWidth) / 2;

            int bottomH = 95;
            int availableH = this.ClientSize.Height - bottomH;

            int minNeededH = 490;
            int extraH = Math.Max(0, availableH - minNeededH);

            currentHeaderY = 18 + Math.Min(36, extraH / 10);
            int gpuY = currentHeaderY + 40;
            int startOptionsY = gpuY + 36;

            int rowStep = 48 + Math.Min(10, extraH / 18);

            lblGpuInfo.Location = new Point(currentContentX, gpuY);
            lblGpuInfo.Size = new Size(contentWidth-(uiLanguage==null?0:80), 27);
            if(uiLanguage!=null)uiLanguage.Location=new Point(currentContentX+contentWidth-70,gpuY+1);

            for (int i = 0; i < settingRows.Count; i++)
            {
                SettingRow row = settingRows[i];
                int curY = startOptionsY + i * rowStep;
                row.Label.Location = new Point(currentContentX, curY);
                row.Label.Size = new Size(contentWidth, 16);
                row.ComboBox.Location = new Point(currentContentX, curY + 18);
                row.ComboBox.Size = new Size(contentWidth, 27);
            }

            int chkY = startOptionsY + settingRows.Count * rowStep + 8;
            int chkW = (contentWidth - 14) / 2;
            chkShowFps.Location = new Point(currentContentX, chkY);
            chkShowFps.Size = new Size(chkW, 24);

            chkVSync.Location = new Point(currentContentX + chkW + 14, chkY);
            chkVSync.Size = new Size(chkW, 24);
            LayoutFeatures(currentContentX,chkY+31,contentWidth);

            bottomPanel.Location = new Point(0, this.ClientSize.Height - bottomH);
            bottomPanel.Size = new Size(controlsW, bottomH);

            int statusW = Math.Min(controlsW - 40, 620);
            int statusX = (controlsW - statusW) / 2;
            lblStatus.Location = new Point(statusX, 4);
            lblStatus.Size = new Size(statusW, 20);

            btnPlay.Location = new Point(currentContentX, 26);
            btnPlay.Size = new Size(contentWidth, 50);

            bottomPanel.ResumeLayout();
            panelControls.ResumeLayout();
            this.ResumeLayout();

            panelCover.Invalidate();
            panelControls.Invalidate();
            bottomPanel.Invalidate();
        }

        private void PanelCover_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.FromArgb(10, 10, 10));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            if (coverImage != null)
            {
                float scale = Math.Max((float)panelCover.ClientSize.Width / coverImage.Width,
                                       (float)panelCover.ClientSize.Height / coverImage.Height);
                int drawW = (int)(coverImage.Width * scale);
                int drawH = (int)(coverImage.Height * scale);
                int drawX = (panelCover.ClientSize.Width - drawW) / 2;
                int drawY = (panelCover.ClientSize.Height - drawH) / 2;

                g.DrawImage(coverImage, new Rectangle(drawX, drawY, drawW, drawH));
            }

            using (Pen p = new Pen(Color.FromArgb(35, 35, 38), 1))
            {
                g.DrawLine(p, panelCover.ClientSize.Width - 1, 0, panelCover.ClientSize.Width - 1, panelCover.ClientSize.Height);
            }
        }

        private void PanelControls_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (Font fontTitle = new Font("Segoe UI", 14.5f, FontStyle.Bold))
            using (Font fontAuthor = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (SolidBrush brushWhite = new SolidBrush(Color.White))
            using (SolidBrush brushRed = new SolidBrush(Color.FromArgb(224, 26, 34)))
            using (StringFormat sf = (StringFormat)StringFormat.GenericTypographic.Clone())
            {
                sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;

                float curX = currentContentX;
                float titleY = currentHeaderY;

                g.DrawString("FIFA ", fontTitle, brushWhite, curX, titleY, sf);
                curX += g.MeasureString("FIFA ", fontTitle, PointF.Empty, sf).Width;

                g.DrawString("08", fontTitle, brushRed, curX, titleY, sf);
                curX += g.MeasureString("08", fontTitle, PointF.Empty, sf).Width;

                g.DrawString(" PORT PC LAUNCHER", fontTitle, brushWhite, curX, titleY, sf);
                curX += g.MeasureString(" PORT PC LAUNCHER", fontTitle, PointF.Empty, sf).Width;

                float authorY = titleY + (fontTitle.Height - fontAuthor.Height);
                g.DrawString("  by PR7", fontAuthor, brushRed, curX, authorY, sf);
            }
        }

        private void BottomPanel_Paint(object sender, PaintEventArgs e)
        {
            using (Pen p = new Pen(Color.FromArgb(28, 28, 30), 1))
            {
                e.Graphics.DrawLine(p, 0, 0, bottomPanel.ClientSize.Width, 0);
            }
        }

        private static readonly string[] ResolutionKeys = { "720", "1080", "1440", "2160", "1080_fsr", "1440_fsr", "2160_fsr" };
        private static readonly string[] FrameLimitKeys = { "original", "30", "60", "90", "100", "120", "144", "165", "240", "0" };

        private void LoadSettings()
        {
            if (!File.Exists(settingsFile)) return;
            try
            {
                var values = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(settingsFile));
                Func<string, string, string> value = (key, fallback) => values.ContainsKey(key) ? Convert.ToString(values[key]) : fallback;
                cbGraphicsApi.SelectedIndex = value("GraphicsApi", "Vulkan") == "D3D12" ? 1 : 0;
                string[] resolutions = ResolutionKeys;
                int resolution = Array.IndexOf(resolutions, value("Resolution", "1080"));
                cbResolution.SelectedIndex = resolution < 0 ? 1 : resolution;
                cbDisplayMode.SelectedIndex = value("DisplayMode", "window") == "fullscreen" ? 1 : 0;

                string limit = value("FrameLimit", "60");
                int limitIndex = Array.IndexOf(FrameLimitKeys, limit);
                if (limitIndex < 0)
                {
                    if (limit == "unlimited" || limit == "sin_limite") limitIndex = 9;
                    else limitIndex = 2; // Default 60 FPS
                }
                cbFrameLimit.SelectedIndex = limitIndex;

                string post = value("PostEffect", "none");
                if (post == "fsr" && cbResolution.SelectedIndex >= 1 && cbResolution.SelectedIndex <= 3)
                    cbResolution.SelectedIndex += 3;
                cbAntiAliasing.SelectedIndex = post == "fxaa_extreme" ? 2 : post == "fxaa" ? 1 : 0;
                SelectLanguage(cbTextLanguage,value("TextLanguage", "es"));
                SelectLanguage(cbCommentaryLanguage,value("CommentaryLanguage", "en"));
                chkShowFps.Checked = value("ShowFps", "false").Equals("True", StringComparison.OrdinalIgnoreCase);
                chkVSync.Checked = value("VSync", "false").Equals("True", StringComparison.OrdinalIgnoreCase);
                achievementPopup=value("AchievementPopup","true").Equals("True",StringComparison.OrdinalIgnoreCase);
                achievementSound=value("AchievementSound","true").Equals("True",StringComparison.OrdinalIgnoreCase);
            }
            catch { }
        }

        private void SaveSettings()
        {
            SaveModernSettings();
            try
            {
                string[] resolutions = ResolutionKeys;
                string[] displayModes = { "window", "fullscreen" };
                string[] postEffects = { "none", "fxaa", "fxaa_extreme" };
                string[] graphicsApis = { "Vulkan", "D3D12" };

                string api = graphicsApis[Math.Max(0, Math.Min(cbGraphicsApi.SelectedIndex, 1))];
                string res = resolutions[Math.Max(0, Math.Min(cbResolution.SelectedIndex, ResolutionKeys.Length - 1))];
                string disp = displayModes[Math.Max(0, Math.Min(cbDisplayMode.SelectedIndex, 1))];

                int limitIdx = Math.Max(0, Math.Min(cbFrameLimit.SelectedIndex, FrameLimitKeys.Length - 1));
                string fpsMode = FrameLimitKeys[limitIdx];

                string textLang = LanguageCode(cbTextLanguage);
                string commLang = LanguageCode(cbCommentaryLanguage);
                string post = postEffects[Math.Max(0, Math.Min(cbAntiAliasing.SelectedIndex, 2))];

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"GraphicsApi\": \"" + api + "\",");
                sb.AppendLine("  \"Resolution\": \"" + res + "\",");
                sb.AppendLine("  \"DisplayMode\": \"" + disp + "\",");
                sb.AppendLine("  \"FrameLimit\": \"" + fpsMode + "\",");
                sb.AppendLine("  \"PostEffect\": \"" + post + "\",");
                sb.AppendLine("  \"TextLanguage\": \"" + textLang + "\",");
                sb.AppendLine("  \"CommentaryLanguage\": \"" + commLang + "\",");
                sb.AppendLine("  \"ShowFps\": " + (chkShowFps.Checked ? "true" : "false") + ",");
                sb.AppendLine("  \"AchievementPopup\": " + achievementPopup.ToString().ToLowerInvariant() + ",");
                sb.AppendLine("  \"AchievementSound\": " + achievementSound.ToString().ToLowerInvariant() + ",");
                sb.AppendLine("  \"VSync\": " + (chkVSync.Checked ? "true" : "false"));
                sb.AppendLine("}");

                File.WriteAllText(settingsFile, sb.ToString());
            }
            catch (Exception error) { throw new IOException("No se pudo guardar la configuracion", error); }
        }

        public string BuildLaunchArguments(bool planOnly)
        {
            string script = Path.Combine(rootDir, "probar-fifa08.ps1");
            string[] resolutions = ResolutionKeys;
            string[] postEffects = { "none", "fxaa", "fxaa_extreme" };

            int[] frameRates = { 60, 30, 60, 90, 100, 120, 144, 165, 240, 0 };
            int selectedIdx = Math.Max(0, Math.Min(cbFrameLimit.SelectedIndex, frameRates.Length - 1));
            int fps = frameRates[selectedIdx];
            string pacing = (selectedIdx == 0) ? "original" : "guest";

            StringBuilder args = new StringBuilder();
            args.Append("-NoProfile -ExecutionPolicy Bypass -Command \"& '" + script.Replace("'", "''") + "' ");
            args.Append("-GraphicsApi " + (cbGraphicsApi.SelectedIndex == 0 ? "Vulkan" : "D3D12"));
            args.Append(" -Resolution " + resolutions[cbResolution.SelectedIndex]);
            args.Append(" -DisplayMode " + (cbDisplayMode.SelectedIndex == 1 ? "fullscreen" : "window"));
            args.Append(" -TextLanguage " + LanguageCode(cbTextLanguage));
            args.Append(" -CommentaryLanguage " + LanguageCode(cbCommentaryLanguage));
            args.Append(" -PostEffect " + postEffects[cbAntiAliasing.SelectedIndex]);
            args.Append(" -FrameLimit " + fps + " -PacingMode " + pacing);
            args.Append(" -ShowFps:" + (chkShowFps.Checked ? "$true" : "$false"));
            args.Append(" -VSync:" + (chkVSync.Checked ? "$true" : "$false"));
            args.Append(" -AsyncShaders:$false -EdramProtection:$true");
            args.Append(" -AchievementPopup:"+(achievementPopup?"$true":"$false")+" -AchievementSound:"+(achievementSound?"$true":"$false"));
            args.Append(" -InterfaceLanguage "+(PortUi.English?"en":"es"));
            args.Append(ModernArgs());
            if (planOnly) args.Append(" -PlanOnly");
            args.Append("\"");
            return args.ToString();
        }

        private readonly object logLock = new object();
        private void BtnPlay_Click(object sender, EventArgs e)
        {
            try
            {
                SaveSettings();
                PromptMods.Apply(GameDataRoot(),Path.Combine(rootDir,"launcher-assets","ps-buttons"),prompts.SelectedIndex==1);
                string script = Path.Combine(rootDir, "probar-fifa08.ps1");
                if (!File.Exists(script)) throw new FileNotFoundException("Falta el script de inicio", script);
                ProcessStartInfo info = new ProcessStartInfo("powershell.exe", BuildLaunchArguments(false));
                info.WorkingDirectory = rootDir;
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = info.RedirectStandardError = true;
                Process process = new Process();
                process.StartInfo = info;
                DataReceivedEventHandler record = (o, line) => {
                    if (line.Data != null) lock (logLock) File.AppendAllText(Path.Combine(rootDir, "launcher.log"), line.Data + Environment.NewLine);
                };
                process.OutputDataReceived += record;
                process.ErrorDataReceived += record;
                process.Start();
                process.BeginOutputReadLine(); process.BeginErrorReadLine();
                runningGameProcess = process;
                btnPlay.Enabled = false; btnPlay.Text = PortUi.T("EN JUEGO");
                SetLauncherStatus("FIFA 08 en ejecucion");
                WindowState = FormWindowState.Minimized;
                gameMonitorTimer.Start();
            }
            catch (Exception error)
            {
                btnPlay.Enabled = true; btnPlay.Text = PortUi.T("JUGAR");
                MessageBox.Show(PortUi.T(error.Message), "FIFA 08", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GameMonitorTimer_Tick(object sender, EventArgs e)
        {
            if (runningGameProcess == null || runningGameProcess.HasExited)
            {
                gameMonitorTimer.Stop();
                if (runningGameProcess != null)
                {
                    runningGameProcess.WaitForExit();
                    if (runningGameProcess.ExitCode != 0)
                        MessageBox.Show(PortUi.T("No se pudo iniciar FIFA 08. Consulta launcher.log para ver el motivo."), "FIFA 08", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    runningGameProcess.Dispose();
                }
                runningGameProcess = null;
                btnPlay.Enabled = true;
                btnPlay.Text = PortUi.T("JUGAR");
                SetLauncherStatus("F10 durante el partido para volver a Windows");
                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.WindowState = FormWindowState.Normal;
                }
                this.BringToFront();
            }
        }
    }
}
