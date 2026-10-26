using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Security.Cryptography;

namespace FIFA08Installer
{
    internal static class Program
    {
        internal static string ContentRoot = AppDomain.CurrentDomain.BaseDirectory;

        internal static bool ExtractEmbedded(string destination)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Fifa08.Payload.zip")) {
                if (stream == null) return false;
                string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Directory.CreateDirectory(root);
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read)) {
                    foreach (var entry in archive.Entries) {
                        string target = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid package path.");
                        if (entry.Name.Length == 0) { Directory.CreateDirectory(target); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        using (var input = entry.Open())
                        using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write)) input.CopyTo(output);
                    }
                }
                return true;
            }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            // Read-only package verification without opening the installer UI.
            if (args.Length == 2 && args[0] == "--extract-payload") {
                if (!ExtractEmbedded(args[1])) Environment.ExitCode = 1;
                return;
            }
            string temporary = null;
            try {
                if (Assembly.GetExecutingAssembly().GetManifestResourceInfo("Fifa08.Payload.zip") != null) {
                    temporary = Path.Combine(Path.GetTempPath(), "Fifa08Installer-" + Guid.NewGuid().ToString("N"));
                    ExtractEmbedded(Path.Combine(temporary, "payload"));
                    ContentRoot = temporary;
                }
                RunInstaller(args);
            } catch (Exception error) {
                MessageBox.Show("No se pudo preparar el instalador / Unable to prepare installer.\n" + error.Message,
                    "FIFA 08", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
            } finally {
                if (temporary != null) { try { Directory.Delete(temporary, true); } catch { } }
            }
        }

        private static void RunInstaller(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if ((args.Length==2||args.Length==3) && args[0]=="--preview") {
                using (var form=new InstallerForm()) {
                    if(args.Length==3)form.SetUiEnglish(args[2]=="en",false);
                    form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-20000,-20000); form.ShowInTaskbar=false;
                    form.Show(); Application.DoEvents();
                    using (var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(args[1]); }
                }
                return;
            }
            using (var form = new InstallerForm()) Application.Run(form);
        }
    }

    public partial class InstallerForm : Form
    {
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        private Image coverImage;
        private int coverWidth = 352;

        private TextBox txtSource;
        private TextBox txtDestination;
        private Button btnBrowseIso;
        private Button btnBrowseGod;
        private Button btnBrowseDest;
        private CheckBox chkShortcut;
        private Label lblStatus;
        private InstallerProgress progressBar;
        private Button btnInstall;
        private Button btnCancel;
        private Button btnClose;
        private Button btnMinimize;

        private BackgroundWorker worker;
        private CancellationTokenSource cancellationTokenSource;
        private bool isInstalling = false;
        private bool isCompleted = false;
        private bool closeWhenStopped;
        private string finalInstalledFolder = "";

        private readonly string rootDir;

        public InstallerForm()
        {
            rootDir = Program.ContentRoot;
            InitializeComponent();InitializeLanguage();InitializeModernInstaller();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(860, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.Text = "FIFA 08 - by PR7"; this.MinimizeBox=true; this.MaximizeBox=true;
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(14, 14, 15); // Negro liso minimalista

            // Icon
            string assetDir = Path.Combine(rootDir,"launcher-assets");
            if (!Directory.Exists(assetDir)) assetDir = Path.Combine(rootDir,"payload","launcher-assets");
            string iconPath = Path.Combine(assetDir, "icon.ico");
            if (File.Exists(iconPath))
            {
                try { this.Icon = new Icon(iconPath); } catch { }
            }

            // Cover image on the left
            string coverPath = Path.Combine(assetDir, "cover.jpg");
            if (File.Exists(coverPath))
            {
                try { using (var image = Image.FromFile(coverPath)) coverImage = new Bitmap(image); } catch { }
            }

            // Window Controls (top right)
            btnClose = new Button();
            btnClose.Text = "✕";
            btnClose.Size = new Size(36, 28);
            btnClose.Location = new Point(this.Width - 42, 6);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.ForeColor = Color.FromArgb(150, 150, 150);
            btnClose.BackColor = Color.Transparent;
            btnClose.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, e) => Application.Exit();
            btnClose.MouseEnter += (s, e) => { btnClose.BackColor = InstallerStyle.Accent; btnClose.ForeColor = Color.White; };
            btnClose.MouseLeave += (s, e) => { btnClose.BackColor = Color.Transparent; btnClose.ForeColor = Color.FromArgb(150, 150, 150); };
            this.Controls.Add(btnClose);

            btnMinimize = new Button();
            btnMinimize.Text = "—";
            btnMinimize.Size = new Size(36, 28);
            btnMinimize.Location = new Point(this.Width - 78, 6);
            btnMinimize.FlatStyle = FlatStyle.Flat;
            btnMinimize.FlatAppearance.BorderSize = 0;
            btnMinimize.ForeColor = Color.FromArgb(150, 150, 150);
            btnMinimize.BackColor = Color.Transparent;
            btnMinimize.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            btnMinimize.Cursor = Cursors.Hand;
            btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            btnMinimize.MouseEnter += (s, e) => { btnMinimize.BackColor = Color.FromArgb(35, 35, 35); btnMinimize.ForeColor = Color.White; };
            btnMinimize.MouseLeave += (s, e) => { btnMinimize.BackColor = Color.Transparent; btnMinimize.ForeColor = Color.FromArgb(150, 150, 150); };
            this.Controls.Add(btnMinimize);

            // Controls on the right side
            int ctrlX = coverWidth + 30;
            int ctrlWidth = this.Width - ctrlX - 30;

            // 1. Header Titles
            Label lblTitle = new InstallerTitle();
            lblTitle.Text = "FIFA 08 PORT PC";
            lblTitle.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(ctrlX, 20);
            lblTitle.Size = new Size(ctrlWidth - 150, 28);
            lblTitle.BackColor = Color.Transparent;
            this.Controls.Add(lblTitle);

            Label lblSubtitle = new Label();
            lblSubtitle.Text = "INSTALADOR DEL JUEGO";
            lblSubtitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblSubtitle.ForeColor = InstallerStyle.Accent; // Rojo FIFA
            lblSubtitle.Location = new Point(ctrlX, 48);
            lblSubtitle.Size = new Size(ctrlWidth, 18);
            lblSubtitle.BackColor = Color.Transparent;
            this.Controls.Add(lblSubtitle);

            Label lblDesc = new Label();
            lblDesc.Text = "Selecciona tu copia original de Xbox 360 en formato ISO o GOD. El instalador extraerá los archivos y preparará el port para PC.";
            lblDesc.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblDesc.ForeColor = Color.FromArgb(161, 161, 170);
            lblDesc.Location = new Point(ctrlX, 68);
            lblDesc.Size = new Size(ctrlWidth, 42);
            lblDesc.BackColor = Color.Transparent;
            this.Controls.Add(lblDesc);

            // 2. Origen (Source ISO / GOD)
            AddSectionLabel("1. Copia original de Xbox 360 (ISO o carpeta GOD):", ctrlX, 118);

            txtSource = CreateStyledTextBox(ctrlX, 140, ctrlWidth - 165);
            this.Controls.Add(txtSource);

            btnBrowseIso = CreateStyledButton("ISO (.iso)", ctrlX + ctrlWidth - 160, 139, 75, 28);
            btnBrowseIso.Click += BtnBrowseIso_Click;
            this.Controls.Add(btnBrowseIso);

            btnBrowseGod = CreateStyledButton("Carpeta GOD", ctrlX + ctrlWidth - 80, 139, 80, 28);
            btnBrowseGod.Click += BtnBrowseGod_Click;
            this.Controls.Add(btnBrowseGod);

            // 3. Destino (Destination Folder)
            AddSectionLabel("2. Carpeta de instalación en tu PC:", ctrlX, 178);

            string defaultDest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games", "FIFA 08 PC");
            txtDestination = CreateStyledTextBox(ctrlX, 200, ctrlWidth - 90);
            txtDestination.Text = defaultDest;
            this.Controls.Add(txtDestination);

            btnBrowseDest = CreateStyledButton("Examinar...", ctrlX + ctrlWidth - 85, 199, 85, 28);
            btnBrowseDest.Click += BtnBrowseDest_Click;
            this.Controls.Add(btnBrowseDest);

            // 4. Opciones (Options)
            chkShortcut = new InstallerCheckBox();
            chkShortcut.Text = "Crear acceso directo en el Escritorio";
            chkShortcut.Font = new Font("Segoe UI", 9, FontStyle.Regular);
            chkShortcut.ForeColor = Color.FromArgb(212, 212, 216);
            chkShortcut.BackColor = Color.Transparent;
            chkShortcut.Checked = true;
            chkShortcut.Location = new Point(ctrlX + 2, 238);
            chkShortcut.Size = new Size(ctrlWidth - 4, 22);
            chkShortcut.Cursor = Cursors.Hand;
            this.Controls.Add(chkShortcut);

            // 5. Progreso y Estado (Progress & Status)
            lblStatus = new Label();
            lblStatus.Text = "Selecciona el origen y destino para comenzar la instalación.";
            lblStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblStatus.ForeColor = Color.FromArgb(161, 161, 170);
            lblStatus.BackColor = Color.Transparent;
            lblStatus.Location = new Point(ctrlX, 274);
            lblStatus.Size = new Size(ctrlWidth, 20);
            this.Controls.Add(lblStatus);

            progressBar = new InstallerProgress();
            progressBar.Location = new Point(ctrlX, 298);
            progressBar.Size = new Size(ctrlWidth, 8);
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
            this.Controls.Add(progressBar);

            // 6. Botones de acción principales
            btnInstall = new InstallerButton { Primary = true, CornerRadius = 10 };
            btnInstall.Text = PortUi.T("INSTALAR FIFA 08");
            btnInstall.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnInstall.ForeColor = Color.White;
            btnInstall.BackColor = InstallerStyle.Accent; // Rojo FIFA
            btnInstall.FlatStyle = FlatStyle.Flat;
            btnInstall.FlatAppearance.BorderSize = 0;
            btnInstall.Location = new Point(ctrlX, 356);
            btnInstall.Size = new Size(ctrlWidth, 44);
            btnInstall.Cursor = Cursors.Hand;
            btnInstall.Click += BtnInstall_Click;
            this.Controls.Add(btnInstall);

            btnCancel = new InstallerButton();
            btnCancel.Text = PortUi.T("Salir");
            btnCancel.Font = new Font("Segoe UI", 9, FontStyle.Regular);
            btnCancel.ForeColor = Color.FromArgb(150, 150, 150);
            btnCancel.BackColor = Color.FromArgb(22, 22, 25);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Location = new Point(ctrlX, 408);
            btnCancel.Size = new Size(ctrlWidth, 26);
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.Click += BtnCancel_Click;
            btnCancel.MouseEnter += (s, e) => btnCancel.ForeColor = Color.White;
            btnCancel.MouseLeave += (s, e) => btnCancel.ForeColor = Color.FromArgb(150, 150, 150);
            this.Controls.Add(btnCancel);

            this.MouseDown += Form_MouseDown;
        }

        private void AddSectionLabel(string text, int x, int y)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lbl.ForeColor = Color.FromArgb(212, 212, 216);
            lbl.BackColor = Color.Transparent;
            lbl.Location = new Point(x, y);
            lbl.Size = new Size(450, 18);
            this.Controls.Add(lbl);
        }

        private TextBox CreateStyledTextBox(int x, int y, int width)
        {
            TextBox tb = new TextBox();
            tb.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            tb.BackColor = Color.FromArgb(20, 20, 20);
            tb.ForeColor = Color.White;
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.Location = new Point(x, y);
            tb.Size = new Size(width, 26);
            return tb;
        }

        private Button CreateStyledButton(string text, int x, int y, int width, int height)
        {
            Button btn = new InstallerButton();
            btn.Text = text;
            btn.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            btn.ForeColor = Color.FromArgb(220, 220, 220);
            btn.BackColor = Color.FromArgb(22, 22, 25);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = Color.FromArgb(45, 45, 45);
            btn.FlatAppearance.BorderSize = 1;
            btn.Location = new Point(x, y);
            btn.Size = new Size(width, height);
            btn.Cursor = Cursors.Hand;
            return btn;
        }

        private void BtnBrowseIso_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = PortUi.T("Seleccionar imagen ISO de FIFA 08 (Xbox 360)");
                ofd.Filter = PortUi.T("Imagen de disco Xbox 360 (*.iso)|*.iso|Todos los archivos (*.*)|*.*");
                ofd.CheckFileExists = true;
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    txtSource.Text = ofd.FileName;
                }
            }
        }

        private void BtnBrowseGod_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = PortUi.T("Seleccionar carpeta GOD (Games On Demand) o carpeta extraída de FIFA 08:");
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    txtSource.Text = fbd.SelectedPath;
                }
            }
        }

        private void BtnBrowseDest_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = PortUi.T("Seleccionar carpeta donde instalar FIFA 08 PC:");
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    txtDestination.Text = fbd.SelectedPath;
                }
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            if (isInstalling)
            {
                if (MessageBox.Show(PortUi.T("¿Deseas cancelar la instalación en curso?"), "FIFA 08 Installer",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    if (cancellationTokenSource != null)
                    {
                        cancellationTokenSource.Cancel();
                        SetInstallerStatus("Cancelando instalación...");
                        btnCancel.Enabled = false;
                    }
                }
            }
            else
            {
                Application.Exit();
            }
        }

        private void BtnInstall_Click(object sender, EventArgs e)
        {
            if (isCompleted)
            {
                // Launch the installed game
                try
                {
                    string exePath = Path.Combine(finalInstalledFolder, "FIFA 08.exe");
                    if (File.Exists(exePath))
                    {
                        ProcessStartInfo psi = new ProcessStartInfo();
                        psi.FileName = exePath;
                        psi.WorkingDirectory = finalInstalledFolder;
                        Process.Start(psi);
                    }
                }
                catch { }
                Application.Exit();
                return;
            }

            string source = txtSource.Text.Trim();
            string dest = txtDestination.Text.Trim();

            if (string.IsNullOrEmpty(source))
            {
                MessageBox.Show(PortUi.T("Por favor selecciona la imagen ISO o carpeta GOD de FIFA 08."), "FIFA 08 Installer",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!File.Exists(source) && !Directory.Exists(source))
            {
                MessageBox.Show(PortUi.T("El origen especificado no existe:\n" + source), "FIFA 08 Installer",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrEmpty(dest))
            {
                MessageBox.Show(PortUi.T("Por favor especifica una carpeta de destino válida."), "FIFA 08 Installer",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Start installation
            SetUiInstalling(true);
            cancellationTokenSource = new CancellationTokenSource();

            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;

            bool createShortcut = chkShortcut.Checked;
            worker.DoWork += (s, workerArgs) =>
            {
                InstallerEngine engine = new InstallerEngine(rootDir);
                engine.ProgressChanged += (percent, statusText) =>
                {
                    worker.ReportProgress(percent, statusText);
                };

                engine.Install(source, dest, createShortcut, cancellationTokenSource.Token);
            };

            worker.ProgressChanged += (s, progressArgs) =>
            {
                progressBar.Value = Math.Max(0, Math.Min(100, progressArgs.ProgressPercentage));
                if (progressArgs.UserState != null)
                {
                    SetInstallerStatus(progressArgs.UserState.ToString());
                }
            };

            worker.RunWorkerCompleted += (s, completedArgs) =>
            {
                isInstalling = false;
                if(closeWhenStopped){Close();return;}
                if (completedArgs.Cancelled || (completedArgs.Error is OperationCanceledException))
                {
                    SetInstallerStatus("Instalación cancelada.");
                    progressBar.Value = 0;
                    SetUiInstalling(false);
                }
                else if (completedArgs.Error != null)
                {
                    SetInstallerStatus("Error en la instalación.");
                    MessageBox.Show(PortUi.T("Ocurrió un error durante la instalación:\n\n" + PortUi.T(completedArgs.Error.Message)),
                        "FIFA 08 Installer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    SetUiInstalling(false);
                }
                else
                {
                    isCompleted = true;
                    finalInstalledFolder = dest;
                    progressBar.Value = 100;
                    SetInstallerStatus("✔  ¡Instalación completada exitosamente!");
                    lblStatus.ForeColor = Color.FromArgb(74, 222, 128); // Verde suave éxito
                    btnInstall.Text = PortUi.T("▶   JUGAR AHORA");
                    btnInstall.Enabled = true;
                    btnInstall.BackColor = InstallerStyle.Accent;
                    btnCancel.Text = PortUi.T("Cerrar");
                    btnCancel.Enabled = true;
                }
            };

            worker.RunWorkerAsync();
        }

        private void SetUiInstalling(bool installing)
        {
            isInstalling = installing;
            txtSource.Enabled = !installing;
            txtDestination.Enabled = !installing;
            btnBrowseIso.Enabled = !installing;
            btnBrowseGod.Enabled = !installing;
            btnBrowseDest.Enabled = !installing;
            chkShortcut.Enabled = !installing;

            if (installing)
            {
                btnInstall.Enabled = false;
                btnInstall.Text = PortUi.T("INSTALANDO...");
                btnCancel.Text = PortUi.T("Cancelar");
            }
            else
            {
                btnInstall.Enabled = true;
                btnInstall.Text = PortUi.T("INSTALAR FIFA 08");
                btnCancel.Text = PortUi.T("Salir");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if(isInstalling)
            {
                e.Cancel=true;
                if(cancellationTokenSource!=null&&!cancellationTokenSource.IsCancellationRequested)
                    BtnCancel_Click(this,EventArgs.Empty);
                closeWhenStopped=cancellationTokenSource!=null&&cancellationTokenSource.IsCancellationRequested;
            }
            base.OnFormClosing(e);
        }

        private void Form_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // 1. Portada en el lado izquierdo
            if (coverImage != null)
            {
                var saved=g.Save();g.SetClip(new Rectangle(0,0,coverWidth,ClientSize.Height));
                float scale=Math.Max((float)coverWidth/coverImage.Width,(float)ClientSize.Height/coverImage.Height);
                float dw=coverImage.Width*scale,dh=coverImage.Height*scale;
                g.DrawImage(coverImage,(coverWidth-dw)/2,(ClientSize.Height-dh)/2,dw,dh);g.Restore(saved);
            }
            else
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(14, 14, 15)))
                {
                    g.FillRectangle(b, 0, 0, coverWidth, this.Height);
                }
            }

            // 2. Panel derecho negro liso minimalista
            using (SolidBrush b = new SolidBrush(Color.FromArgb(14, 14, 15)))
            {
                g.FillRectangle(b, coverWidth, 0, this.Width - coverWidth, this.Height);
            }

            // 3. Separador sutil vertical
            using (Pen p = new Pen(Color.FromArgb(32, 32, 32), 1))
            {
                g.DrawLine(p, coverWidth, 0, coverWidth, this.Height);
            }

            // 4. Borde exterior minimalista
            using (Pen borderPen = new Pen(Color.FromArgb(38, 38, 38), 1))
            {
                g.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
            }
        }
    }

    public class GameEntry
    {
        public string Path { get; set; }
        public uint Sector { get; set; }
        public uint Size { get; set; }
        public bool IsDirectory { get; set; }
    }

    public interface IGameReader : IDisposable
    {
        bool Open(string source);
        void ReadBytes(long virtualOffset, byte[] buffer, int count);
        List<GameEntry> GetEntries();
    }

    public class XboxIsoReader : IGameReader
    {
        private FileStream fs;
        private long partitionStartOffset = 0;

        public bool Open(string source)
        {
            if (!File.Exists(source)) return false;
            fs = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);

            long[] probeOffsets = new long[] {
                32L * 2048,                         // XISO / Extracted
                (64928L + 32L) * 2048,              // XGD2 retail dump
                64928L * 2048,
                (130848L + 32L) * 2048,             // XGD3 retail dump
                130848L * 2048,
                0L
            };

            byte[] magic = new byte[20];
            foreach (long off in probeOffsets)
            {
                if (off + 2048 <= fs.Length)
                {
                    fs.Position = off;
                    if (fs.Read(magic, 0, 20) == 20)
                    {
                        if (Encoding.ASCII.GetString(magic) == "MICROSOFT*XBOX*MEDIA")
                        {
                            partitionStartOffset = off - (32L * 2048);
                            return true;
                        }
                    }
                }
            }

            // Fast scan if not at known offsets
            long maxScan = Math.Min(fs.Length - 2048, 300L * 1024 * 1024);
            byte[] buffer = new byte[2048];
            for (long off = 0; off < maxScan; off += 2048)
            {
                fs.Position = off;
                if (fs.Read(buffer, 0, 20) == 20)
                {
                    if (Encoding.ASCII.GetString(buffer, 0, 20) == "MICROSOFT*XBOX*MEDIA")
                    {
                        partitionStartOffset = off - (32L * 2048);
                        return true;
                    }
                }
            }

            return false;
        }

        public void ReadBytes(long virtualOffset, byte[] buffer, int count)
        {
            fs.Position = partitionStartOffset + virtualOffset;
            int read = fs.Read(buffer, 0, count);
            if (read != count) throw new EndOfStreamException("Lectura incompleta del archivo ISO.");
        }

        public List<GameEntry> GetEntries()
        {
            byte[] desc = new byte[2048];
            ReadBytes(32L * 2048, desc, 2048);
            if (Encoding.ASCII.GetString(desc, 0, 20) != "MICROSOFT*XBOX*MEDIA")
                throw new InvalidDataException("Descriptores de volumen no encontrados en la imagen ISO.");

            uint rootSector = BitConverter.ToUInt32(desc, 20);
            uint rootSize = BitConverter.ToUInt32(desc, 24);

            List<GameEntry> entries = new List<GameEntry>();
            ReadDirectory(rootSector, rootSize, "", entries);
            return entries;
        }

        private void ReadDirectory(uint sector, uint size, string parent, List<GameEntry> entries)
        {
            if (size == 0 || size > 64 * 1024 * 1024) return;
            byte[] data = new byte[size];
            ReadBytes((long)sector * 2048L, data, (int)size);

            HashSet<int> seen = new HashSet<int>();
            WalkDirectory(data, 0, parent, seen, entries);
        }

        private void WalkDirectory(byte[] data, int offset, string parent, HashSet<int> seen, List<GameEntry> entries)
        {
            if (offset + 14 > data.Length || seen.Contains(offset)) return;
            seen.Add(offset);

            ushort left = BitConverter.ToUInt16(data, offset);
            ushort right = BitConverter.ToUInt16(data, offset + 2);
            if (left == 0xFFFF || right == 0xFFFF) return;

            uint entrySector = BitConverter.ToUInt32(data, offset + 4);
            uint entrySize = BitConverter.ToUInt32(data, offset + 8);
            byte attributes = data[offset + 12];
            byte nameLength = data[offset + 13];

            if (nameLength == 0 || offset + 14 + nameLength > data.Length) return;
            string name = Encoding.ASCII.GetString(data, offset + 14, nameLength);
            if (name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return;

            if (left != 0) WalkDirectory(data, left * 4, parent, seen, entries);

            string relative = string.IsNullOrEmpty(parent) ? name : (parent + "/" + name);
            bool isDir = (attributes & 0x10) != 0;
            entries.Add(new GameEntry { Path = relative, Sector = entrySector, Size = entrySize, IsDirectory = isDir });

            if (isDir)
            {
                ReadDirectory(entrySector, entrySize, relative, entries);
            }

            if (right != 0) WalkDirectory(data, right * 4, parent, seen, entries);
        }

        public void Dispose()
        {
            if (fs != null)
            {
                fs.Dispose();
                fs = null;
            }
        }
    }

    public class XboxGodReader : IGameReader
    {
        private string dataDir;
        private readonly Dictionary<int, FileStream> partStreams = new Dictionary<int, FileStream>();

        private const long BLOCK = 4096;
        private const long GROUP = 204;
        private const long GROUPS = 203;

        public bool Open(string source)
        {
            if (Directory.Exists(source))
            {
                if (source.EndsWith(".data", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(source, "Data0000")))
                {
                    dataDir = source;
                }
                else
                {
                    string[] dataDirs = Directory.GetDirectories(source, "*.data", SearchOption.AllDirectories);
                    if (dataDirs.Length > 0 && File.Exists(Path.Combine(dataDirs[0], "Data0000")))
                    {
                        dataDir = dataDirs[0];
                    }
                }
            }
            else if (File.Exists(source))
            {
                string candidateData = source + ".data";
                if (Directory.Exists(candidateData) && File.Exists(Path.Combine(candidateData, "Data0000")))
                {
                    dataDir = candidateData;
                }
            }

            if (string.IsNullOrEmpty(dataDir)) return false;

            byte[] desc = new byte[20];
            try
            {
                ReadBytes(32L * 2048, desc, 20);
                return Encoding.ASCII.GetString(desc) == "MICROSOFT*XBOX*MEDIA";
            }
            catch
            {
                return false;
            }
        }

        public void ReadBytes(long offset, byte[] bytes, int size)
        {
            int done = 0;
            while (done < size)
            {
                long pos = offset + done;
                long block = pos / BLOCK;
                int part = (int)(block / (GROUP * GROUPS));
                long local = block % (GROUP * GROUPS);
                long physical = BLOCK + (local / GROUP) * (GROUP + 1) * BLOCK + BLOCK + (local % GROUP) * BLOCK + (pos % BLOCK);

                FileStream fs = GetPartStream(part);
                fs.Position = physical;
                int count = (int)Math.Min((long)(size - done), (long)((GROUP - (local % GROUP)) * BLOCK - (pos % BLOCK)));
                int read = fs.Read(bytes, done, count);
                if (read != count) throw new EndOfStreamException("Lectura incompleta de parte GOD " + part);
                done += count;
            }
        }

        private FileStream GetPartStream(int part)
        {
            FileStream fs;
            if (!partStreams.TryGetValue(part, out fs))
            {
                string partPath = Path.Combine(dataDir, "Data" + part.ToString("D4"));
                if (!File.Exists(partPath)) throw new FileNotFoundException("Falta la parte GOD: " + partPath);
                fs = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                partStreams[part] = fs;
            }
            return fs;
        }

        public List<GameEntry> GetEntries()
        {
            byte[] desc = new byte[2048];
            ReadBytes(32L * 2048, desc, 2048);
            if (Encoding.ASCII.GetString(desc, 0, 20) != "MICROSOFT*XBOX*MEDIA")
                throw new InvalidDataException("Descriptores de volumen no encontrados en el paquete GOD.");

            uint rootSector = BitConverter.ToUInt32(desc, 20);
            uint rootSize = BitConverter.ToUInt32(desc, 24);

            List<GameEntry> entries = new List<GameEntry>();
            ReadDirectory(rootSector, rootSize, "", entries);
            return entries;
        }

        private void ReadDirectory(uint sector, uint size, string parent, List<GameEntry> entries)
        {
            if (size == 0 || size > 64 * 1024 * 1024) return;
            byte[] data = new byte[size];
            ReadBytes((long)sector * 2048L, data, (int)size);

            HashSet<int> seen = new HashSet<int>();
            WalkDirectory(data, 0, parent, seen, entries);
        }

        private void WalkDirectory(byte[] data, int offset, string parent, HashSet<int> seen, List<GameEntry> entries)
        {
            if (offset + 14 > data.Length || seen.Contains(offset)) return;
            seen.Add(offset);

            ushort left = BitConverter.ToUInt16(data, offset);
            ushort right = BitConverter.ToUInt16(data, offset + 2);
            if (left == 0xFFFF || right == 0xFFFF) return;

            uint entrySector = BitConverter.ToUInt32(data, offset + 4);
            uint entrySize = BitConverter.ToUInt32(data, offset + 8);
            byte attributes = data[offset + 12];
            byte nameLength = data[offset + 13];

            if (nameLength == 0 || offset + 14 + nameLength > data.Length) return;
            string name = Encoding.ASCII.GetString(data, offset + 14, nameLength);
            if (name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return;

            if (left != 0) WalkDirectory(data, left * 4, parent, seen, entries);

            string relative = string.IsNullOrEmpty(parent) ? name : (parent + "/" + name);
            bool isDir = (attributes & 0x10) != 0;
            entries.Add(new GameEntry { Path = relative, Sector = entrySector, Size = entrySize, IsDirectory = isDir });

            if (isDir)
            {
                ReadDirectory(entrySector, entrySize, relative, entries);
            }

            if (right != 0) WalkDirectory(data, right * 4, parent, seen, entries);
        }

        public void Dispose()
        {
            foreach (var fs in partStreams.Values)
            {
                try { fs.Dispose(); } catch { }
            }
            partStreams.Clear();
        }
    }

    public class InstallerEngine
    {
        private readonly string baseAppDir;
        public delegate void ProgressHandler(int percentage, string status);
        public event ProgressHandler ProgressChanged;

        public InstallerEngine(string baseAppDir)
        {
            this.baseAppDir = baseAppDir;
        }

        public void Install(string sourcePath, string destPath, bool createShortcut, CancellationToken token)
        {
            Report(2, "Analizando archivo de origen...");
            token.ThrowIfCancellationRequested();

            string sourceFull = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar);
            string destFull = Path.GetFullPath(destPath).TrimEnd(Path.DirectorySeparatorChar);
            if (Directory.Exists(sourcePath) && (destFull.Equals(sourceFull, StringComparison.OrdinalIgnoreCase) || destFull.StartsWith(sourceFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("El destino debe estar fuera de la carpeta original del juego.");
            Directory.CreateDirectory(destPath);
            string gameDataDir = Path.Combine(destPath, "game");
            Directory.CreateDirectory(gameDataDir);

            // Determine if source is already extracted folder containing default.xex
            if (Directory.Exists(sourcePath) && File.Exists(Path.Combine(sourcePath, "default.xex")))
            {
                CopyExtractedFolder(sourcePath, gameDataDir, token);
            }
            else
            {
                // Try as ISO
                IGameReader reader = new XboxIsoReader();
                bool isIso = false;
                try
                {
                    isIso = reader.Open(sourcePath);
                }
                catch { }

                if (!isIso)
                {
                    reader.Dispose();
                    reader = new XboxGodReader();
                    bool isGod = false;
                    try
                    {
                        isGod = reader.Open(sourcePath);
                    }
                    catch { }

                    if (!isGod)
                    {
                        reader.Dispose();
                        throw new InvalidDataException("No se reconoció el origen como una imagen ISO ni contenedor GOD de Xbox 360 válido.");
                    }
                }

                using (reader)
                {
                    Report(5, "Leyendo estructura de archivos Xbox 360...");
                    List<GameEntry> entries = reader.GetEntries();

                    bool hasDefaultXex = false;
                    long totalFileBytes = 0;
                    List<GameEntry> filesToExtract = new List<GameEntry>();

                    foreach (var e in entries)
                    {
                        if (!e.IsDirectory)
                        {
                            filesToExtract.Add(e);
                            totalFileBytes += e.Size;
                            if (string.Equals(e.Path, "default.xex", StringComparison.OrdinalIgnoreCase) ||
                                e.Path.EndsWith("/default.xex", StringComparison.OrdinalIgnoreCase))
                            {
                                hasDefaultXex = true;
                            }
                        }
                    }

                    if (!hasDefaultXex)
                    {
                        throw new InvalidDataException("No se encontró el ejecutable principal 'default.xex' en el juego seleccionado.");
                    }

                    long bytesProcessed = 0;
                    byte[] chunk = new byte[1024 * 1024]; // 1MB buffer para máxima velocidad

                    for (int i = 0; i < filesToExtract.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        GameEntry fe = filesToExtract[i];
                        string outPath = Path.Combine(gameDataDir, fe.Path.Replace('/', Path.DirectorySeparatorChar));
                        if (!Path.GetFullPath(outPath).StartsWith(Path.GetFullPath(gameDataDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Ruta fuera del destino en la imagen del juego");
                        Directory.CreateDirectory(Path.GetDirectoryName(outPath));

                        int progressBase = 5 + (int)((bytesProcessed * 75L) / Math.Max(1L, totalFileBytes));
                        Report(progressBase, "Extrayendo: " + fe.Path);

                        using (FileStream outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            long remaining = fe.Size;
                            long currentOffset = (long)fe.Sector * 2048L;

                            while (remaining > 0)
                            {
                                token.ThrowIfCancellationRequested();
                                int toRead = (int)Math.Min((long)chunk.Length, remaining);
                                reader.ReadBytes(currentOffset, chunk, toRead);
                                outFs.Write(chunk, 0, toRead);
                                currentOffset += toRead;
                                remaining -= toRead;
                                bytesProcessed += toRead;

                                int p = 5 + (int)((bytesProcessed * 75L) / Math.Max(1L, totalFileBytes));
                                if (p > progressBase)
                                {
                                    progressBase = p;
                                    Report(p, "Extrayendo: " + fe.Path);
                                }
                            }
                        }
                    }
                }
            }

            // Install PC Port binaries and assets
            string manifest = FindFileInBase("port-manifest.json");
            if (String.IsNullOrEmpty(manifest)) throw new FileNotFoundException("Falta el manifiesto del port");
            var metadata = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(manifest));
            using (var hash = SHA256.Create()) using (var input = File.OpenRead(Path.Combine(gameDataDir, "default.xex"))) {
                string actual = BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "");
                if (!actual.Equals(Convert.ToString(metadata["SourceDefaultXexSHA256"]), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Esta copia no coincide con la version de FIFA 08 compatible con este port.");
            }
            Report(82, "Instalando binarios optimizados del Port PC...");
            token.ThrowIfCancellationRequested();
            InstallPortFiles(destPath, token);

            Report(92,"Detectando idiomas disponibles...");
            token.ThrowIfCancellationRequested();
            Fifa08Languages.AdoptExtractedLocale(gameDataDir);
            var languages=Fifa08Languages.Detect(gameDataDir,null);
            var text=Fifa08Languages.Resolve(languages.Text,languages.DefaultText);
            var commentary=Fifa08Languages.Resolve(languages.Commentary,text.Code)??Fifa08Languages.Resolve(languages.Commentary,languages.DefaultCommentary);
            string commentaryCode=commentary==null?"original":commentary.Code;
            Fifa08Languages.Configure(gameDataDir,text.Code,commentaryCode,null);
            var serializer=new JavaScriptSerializer();
            File.WriteAllText(Path.Combine(destPath,"bin","game-languages.json"),serializer.Serialize(languages));
            var initialSettings=new Dictionary<string,object>{{"GraphicsApi","Vulkan"},{"Resolution","1080"},{"DisplayMode","window"},{"FrameLimit","60"},{"PostEffect","none"},{"TextLanguage",text.Code},{"CommentaryLanguage",commentaryCode},{"ShowFps",false},{"VSync",false}};
            File.WriteAllText(Path.Combine(destPath,"bin","launcher-settings.json"),serializer.Serialize(initialSettings));
            foreach(string folder in new[]{"mods","data"})Directory.CreateDirectory(Path.Combine(destPath,folder));
            // Desktop shortcut
            if (createShortcut)
            {
                Report(97, "Creando acceso directo en el Escritorio...");
                token.ThrowIfCancellationRequested();
                string shortcutTarget = File.Exists(Path.Combine(destPath, "FIFA 08 Launcher.exe")) ? Path.Combine(destPath, "FIFA 08 Launcher.exe") : Path.Combine(destPath, "FIFA 08.exe");
                CreateDesktopShortcut(shortcutTarget, Path.Combine(destPath, "bin", "launcher-assets", "icon.ico"));
            }

            Report(100, "Instalación completada.");
        }

        private void CopyExtractedFolder(string sourceDir, string destDir, CancellationToken token)
        {
            string[] files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            long totalBytes = 0;
            foreach (string f in files) totalBytes += new FileInfo(f).Length;
            long copiedBytes = 0;

            byte[] buffer = new byte[1024 * 1024];
            foreach (string file in files)
            {
                token.ThrowIfCancellationRequested();
                string relative = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar);
                string target = Path.Combine(destDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));

                int p = 5 + (int)((copiedBytes * 75L) / Math.Max(1L, totalBytes));
                Report(p, "Copiando archivo: " + relative);

                using (FileStream inFs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream outFs = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    int read;
                    while ((read = inFs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        outFs.Write(buffer, 0, read);
                        copiedBytes += read;
                    }
                }
            }
        }

        private void InstallPortFiles(string destDir, CancellationToken token)
        {
            string payload = PayloadDirectory();
            if (!Directory.Exists(payload)) throw new DirectoryNotFoundException("Falta la carpeta payload del instalador. Ejecuta crear-distribucion.ps1 para preparar el paquete.");
            foreach (string required in new [] { "native/build/fifa08.exe", "native/build/Backends/D3D12/manifest.json", "native/build/Backends/Vulkan/manifest.json", "FIFA 08 Launcher.exe", "FIFA08 editor by pr7.exe", "modding/LEEME.txt", "modding/THIRD_PARTY_NOTICES.txt", "probar-fifa08.ps1", "configurar-idiomas.ps1", "launcher-src/GameLanguages.cs", "sounds/logro-xbox.mp3" })
                if (!File.Exists(Path.Combine(payload,required.Replace('/',Path.DirectorySeparatorChar)))) throw new FileNotFoundException("Paquete incompleto: "+required);
            foreach (string file in Directory.GetFiles(payload,"*",SearchOption.AllDirectories)) {
                token.ThrowIfCancellationRequested();
                string relative=file.Substring(payload.Length).TrimStart(Path.DirectorySeparatorChar);
                bool executable=relative.Equals("FIFA 08.exe",StringComparison.OrdinalIgnoreCase)||relative.Equals("FIFA 08 Launcher.exe",StringComparison.OrdinalIgnoreCase)||relative.Equals("FIFA08 editor by pr7.exe",StringComparison.OrdinalIgnoreCase);
                string target=Path.Combine(executable?destDir:Path.Combine(destDir,"bin"),relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file,target,true);
            }
        }
        private string PayloadDirectory()
        {
            string direct=Path.Combine(baseAppDir,"payload");
            return Directory.Exists(direct)?direct:Path.Combine(baseAppDir,"dist","FIFA 08 PC Installer","payload");
        }
        private string FindFileInBase(string relativePath)
        {
            string file=Path.Combine(PayloadDirectory(),relativePath);
            return File.Exists(file)?file:null;
        }
        private static uint ReadUInt32BE(byte[] b, int offset)
        {
            return (uint)((b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3]);
        }

        private static void WriteUInt32BE(byte[] b, int offset, uint val)
        {
            b[offset] = (byte)((val >> 24) & 0xFF);
            b[offset + 1] = (byte)((val >> 16) & 0xFF);
            b[offset + 2] = (byte)((val >> 8) & 0xFF);
            b[offset + 3] = (byte)(val & 0xFF);
        }

        private static void CreateDesktopShortcut(string targetExePath, string iconPath)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shortcutPath = Path.Combine(desktop, "FIFA 08 PC.lnk");
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType);
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetExePath;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(targetExePath);
                    shortcut.Description = "FIFA 08 PC Port";
                    if (File.Exists(iconPath))
                    {
                        shortcut.IconLocation = iconPath + ",0";
                    }
                    shortcut.Save();
                }
            }
            catch { }
        }

        private void Report(int percent, string text)
        {
            if (ProgressChanged != null)
            {
                ProgressChanged(percent, text);
            }
        }
    }
}
