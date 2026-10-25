using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FIFA08Installer {
    internal static class InstallerStyle {
        internal static readonly Color Accent = Color.FromArgb(224, 26, 34);
        internal static GraphicsPath Round(Rectangle rect, int radius) {
            var path = new GraphicsPath();
            int diameter = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            if (diameter <= 0) { path.AddRectangle(rect); return path; }
            var arc = new Rectangle(rect.X, rect.Y, diameter, diameter);
            path.AddArc(arc, 180, 90); arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90); arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90); arc.X = rect.Left;
            path.AddArc(arc, 90, 90); path.CloseFigure(); return path;
        }
    }

    public class InstallerButton : Button {
        private bool hovered, pressed;
        public bool Primary { get; set; }
        public int CornerRadius { get; set; }
        public InstallerButton() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false; Cursor = Cursors.Hand; CornerRadius = 8;
            BackColor = Color.FromArgb(22, 22, 25); ForeColor = Color.FromArgb(220, 220, 225);
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); } base.OnKeyDown(e); }
        protected override void OnKeyUp(KeyEventArgs e) { pressed = false; Invalidate(); base.OnKeyUp(e); }
        protected override void OnLostFocus(EventArgs e) { pressed = false; Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { pressed = false; Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent == null ? Color.FromArgb(14, 14, 15) : Parent.BackColor);
            Color fill = BackColor;
            if (!Enabled) fill = Color.FromArgb(48, 48, 53);
            else if (Primary && BackColor.ToArgb() == InstallerStyle.Accent.ToArgb())
                fill = pressed ? Color.FromArgb(170, 16, 22) : hovered ? Color.FromArgb(238, 38, 46) : BackColor;
            else if (pressed) fill = ControlPaint.Dark(BackColor, .15f);
            else if (hovered) fill = ControlPaint.Light(BackColor, .15f);
            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using (var path = InstallerStyle.Round(rect, CornerRadius)) {
                using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
                Color border = Focused && Enabled ? InstallerStyle.Accent :
                    Primary && Enabled ? ControlPaint.Light(fill, .2f) :
                    hovered && Enabled ? Color.FromArgb(70, 70, 78) : Color.FromArgb(46, 46, 52);
                using (var pen = new Pen(border, 1.2f)) g.DrawPath(pen, path);
            }
            float textSize=Font.Size;
            while(textSize>6.5f){using(var trial=new Font(Font.FontFamily,textSize,Font.Style)){var measured=TextRenderer.MeasureText(g,Text,trial,new Size(10000,10000),TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);if(measured.Width<=Width-10&&measured.Height<=Height-4)break;}textSize-=.5f;}
            using(var fitted=new Font(Font.FontFamily,textSize,Font.Style)){
            TextRenderer.DrawText(g, Text, fitted, ClientRectangle,
                Enabled ? ForeColor : Color.FromArgb(145, 145, 150),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }
            if (Focused && ShowFocusCues) {
                var focus = ClientRectangle; focus.Inflate(-5, -5);
                ControlPaint.DrawFocusRectangle(g, focus, ForeColor, fill);
            }
        }
    }

    public class InstallerTitle : Label {
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (var format = (StringFormat)StringFormat.GenericTypographic.Clone())
            using (var white = new SolidBrush(Color.White))
            using (var red = new SolidBrush(InstallerStyle.Accent))
            using (var author = new Font("Segoe UI", 10.5f, FontStyle.Bold)) {
                format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
                float x = 0;
                foreach (string part in new[] { "FIFA ", "08", " PORT PC" }) {
                    e.Graphics.DrawString(part, Font, part == "08" ? red : white, x, 0, format);
                    x += e.Graphics.MeasureString(part, Font, PointF.Empty, format).Width;
                }
                e.Graphics.DrawString("  by PR7", author, red, x, Font.Height - author.Height, format);
            }
        }
    }

    public class InstallerCheckBox : CheckBox {
        public InstallerCheckBox() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics; g.Clear(Parent == null ? Color.FromArgb(14, 14, 15) : Parent.BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var box = new Rectangle(1, (Height - 17) / 2, 17, 17);
            Color accent = Enabled ? InstallerStyle.Accent : Color.FromArgb(70, 70, 75);
            using (var path = InstallerStyle.Round(box, 4)) {
                using (var brush = new SolidBrush(Checked ? accent : Color.FromArgb(24, 24, 24))) g.FillPath(brush, path);
                using (var pen = new Pen(Checked ? accent : Color.FromArgb(70, 70, 75), 1.4f)) g.DrawPath(pen, path);
            }
            if (Checked) using (var pen = new Pen(Enabled ? Color.White : Color.FromArgb(150, 150, 155), 2.1f)) {
                pen.StartCap = pen.EndCap = LineCap.Round;
                g.DrawLines(pen, new[] {new Point(box.Left+3,box.Top+8),new Point(box.Left+7,box.Top+12),new Point(box.Left+13,box.Top+4)});
            }
            var text = new Rectangle(26, 0, Width - 26, Height);
            TextRenderer.DrawText(g, Text, Font, text, Enabled ? ForeColor : Color.FromArgb(130, 130, 135),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, text);
        }
    }
}
