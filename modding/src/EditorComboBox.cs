using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace Pr7Editor {
    public static class EditorRound
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

    public class EditorComboBox : ComboBox
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
            readonly EditorComboBox owner;
            public PopupBorderWindow(IntPtr hwnd, EditorComboBox combo) { owner=combo; AssignHandle(hwnd); }
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
                        using (SolidBrush background = new SolidBrush(owner.BackColor))
                            g.FillRectangle(background, client.Left, client.Top,
                                client.Right - client.Left, client.Bottom - client.Top);
                        m.Result = new IntPtr(1);
                        return;
                    }
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
                popupBorder = new PopupBorderWindow(info.List,this);
            }
        }

        public int CornerRadius { get; set; }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        public EditorComboBox()
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

        private void SizeSelectionField()
        {
            // CB_SETITEMHEIGHT(-1) sizes the closed selection separately from
            // the 26 px popup rows, preserving the compact launcher layout.
            if (IsHandleCreated)
                SendMessage(Handle, 0x0153, new IntPtr(-1), new IntPtr(Font.Height + 4));
        }
        protected override void OnBackColorChanged(EventArgs e) { if (popupBackgroundBrush != IntPtr.Zero) { DeleteObject(popupBackgroundBrush); popupBackgroundBrush=IntPtr.Zero; } base.OnBackColorChanged(e);Invalidate(); }
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
                Enabled ? ((e.State & DrawItemState.Selected) != 0 ? Color.White : ForeColor) : SystemColors.GrayText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0134) // WM_CTLCOLORLISTBOX: native popup's background.
            {
                int background = ColorTranslator.ToWin32(BackColor);
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
            using (GraphicsPath path = EditorRound.GetRoundedPath(rect, CornerRadius))
            {
                Color boxBg = DroppedDown ? ControlPaint.Light(BackColor,.15f) :
                              isHovered ? ControlPaint.Light(BackColor,.1f) : BackColor;
                using (SolidBrush b = new SolidBrush(boxBg))
                {
                    g.FillPath(b, path);
                }

                Color borderColor = (DroppedDown || this.Focused) ? Color.FromArgb(224, 26, 34) :
                                    isHovered ? Color.FromArgb(70, 70, 78) : Color.FromArgb(46, 46, 52);
                using (Pen p = new Pen(borderColor, 1.2f))
                {
                    g.DrawPath(p, path);
                }
            }

            string text = (SelectedIndex >= 0 && SelectedIndex < Items.Count) ? GetItemText(Items[SelectedIndex]) : "";
            Rectangle textRect = new Rectangle(12, 0, this.Width - 36, this.Height);
            TextRenderer.DrawText(g, text, this.Font, textRect, this.ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

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
            using (Pen arrowPen = new Pen((isHovered || DroppedDown) ? ForeColor : Color.FromArgb(145, 145, 153), 1.6f))
            {
                arrowPen.StartCap = LineCap.Round;
                arrowPen.EndCap = LineCap.Round;
                g.DrawLines(arrowPen, chevron);
            }
        }
    }

}
