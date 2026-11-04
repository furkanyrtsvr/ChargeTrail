using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PilTakip
{
    // Buffer nested layouts so their backgrounds and children paint as one update.
    public class BufferedPanel : Panel
    {
        public BufferedPanel() { DoubleBuffered = true; ResizeRedraw = true; }
    }
    public class BufferedLayout : TableLayoutPanel
    {
        public BufferedLayout() { DoubleBuffered = true; ResizeRedraw = true; }
    }
    public class BufferedFlow : FlowLayoutPanel
    {
        public BufferedFlow() { DoubleBuffered = true; ResizeRedraw = true; }
    }
    public static class Brand
    {
        public static readonly Color Ink = Color.FromArgb(27, 42, 45), Green = Color.FromArgb(26, 112, 91), Mint = Color.FromArgb(179, 236, 206), Paper = Color.FromArgb(244, 246, 241), Muted = Color.FromArgb(111, 125, 121), Line = Color.FromArgb(222, 230, 222);
        public static Bitmap Logo(int size)
        {
            var b = new Bitmap(size, size);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.ScaleTransform(size / 128f, size / 128f);
                using (var brush = new SolidBrush(Ink)) g.FillEllipse(brush, 0, 0, 128, 128);
                using (var p = new Pen(Mint, 7)) { p.StartCap = p.EndCap = LineCap.Round; g.DrawArc(p, 24, 24, 80, 80, 38, 287); }
                using (var brush = new SolidBrush(Mint)) g.FillPolygon(brush, new[] { new Point(67, 29), new Point(43, 69), new Point(61, 69), new Point(55, 99), new Point(88, 56), new Point(68, 56) });
            }
            return b;
        }
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
        public static Icon AppIcon()
        {
            using (var bitmap = Logo(64))
            {
                IntPtr h = bitmap.GetHicon();
                try { return (Icon)Icon.FromHandle(h).Clone(); }
                finally { DestroyIcon(h); }
            }
        }
    }
    public static class Records
    {
        public static TestRun Fresh(TrackedDevice d, int idle)
        {
            return new TestRun { Name = d.Name, DeviceId = d.Id, Connection = d.Connection, Manual = d.Manual, IdleSeconds = idle };
        }
        public static bool Archive(State state, TrackedDevice d)
        {
            if (d.Run.Ended != null) return false;
            d.Enabled = false; d.ActiveUntil = 0; d.Run.Ended = DateTime.UtcNow.ToString("o");
            state.History.Add(d.Run); return true;
        }
        public static void Start(TrackedDevice d, int idle)
        {
            if (d.Run.Ended != null) d.Run = Fresh(d, idle);
            if (d.Run.PcSeconds == 0 && d.Run.VisibleSeconds == 0 && d.Run.ActiveSeconds == 0) d.Run.Started = DateTime.UtcNow.ToString("o");
            d.Enabled = true; d.ActiveUntil = 0;
        }
    }
    public class MainForm : Form
    {
        readonly State state;
        readonly bool background;
        readonly EventWaitHandle showEvent;
        readonly NotifyIcon tray;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Dictionary<IntPtr, TrackedDevice> handles = new Dictionary<IntPtr, TrackedDevice>();
        readonly Dictionary<TrackedDevice, CardView> cards = new Dictionary<TrackedDevice, CardView>();
        readonly Panel content = new BufferedPanel();
        readonly Dictionary<string, Button> navigation = new Dictionary<string, Button>();
        readonly Label footer = new Label();
        FlowLayoutPanel cardFlow;
        DataGridView devicesGrid, recordsGrid;
        Label stats;
        bool updating, exiting, locked, identify;
        double lastTick, lastScan, lastSave, identifyUntil;
        string error = "";
        string page = "home";
        string diagnosticDialogFolder;
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr window, IntPtr rect, IntPtr region, uint flags);
        bool English { get { return state.Language == "EN"; } }
        string T(string tr, string en) { return English ? en : tr; }
        class CardView { public Panel Panel; public Label Time, Active, Status; public Button Toggle, Save; }
        class Choice { public string Value { get; set; } public string Display { get; set; } }
        public MainForm(State loaded, bool hidden, EventWaitHandle signal)
        {
            state = loaded; background = hidden; showEvent = signal;
            if (!state.ManualInitialized) { state.AddManual("SC380 Pro"); state.ManualInitialized = true; }
            if (!state.DashboardInitialized)
            {
                foreach (var d in state.Devices) d.Pinned = d.Enabled || d.Manual;
                state.DashboardInitialized = true;
            }
            Text = "ChargeTrail"; Icon = Brand.AppIcon(); Size = new Size(1330, 865); MinimumSize = new Size(1100, 720);
            DoubleBuffered = true;
            StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10); BackColor = Brand.Paper; ForeColor = Brand.Ink;
            lastTick = lastScan = lastSave = Native.Now();
            tray = new NotifyIcon { Icon = Icon, Visible = true };
            tray.DoubleClick += delegate { Reveal(); };
            BuildShell(); UpdateTray();
            timer.Interval = 1000; timer.Tick += TimerTick;
            FormClosing += HandleClosing;
            SystemEvents.PowerModeChanged += PowerChanged; SystemEvents.SessionSwitch += SessionChanged;
        }
        Button Btn(string text, EventHandler action, bool primary = false)
        {
            var b = new Button { Text = text, UseMnemonic = false, AutoSize = true, MinimumSize = new Size(80, 38), FlatStyle = FlatStyle.Flat, BackColor = primary ? Brand.Green : Color.White, ForeColor = primary ? Color.White : Brand.Ink, Padding = new Padding(12, 5, 12, 5), Margin = new Padding(0, 0, 10, 0), Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            b.FlatAppearance.BorderSize = primary ? 0 : 1; b.FlatAppearance.BorderColor = Brand.Line;
            if (action != null) b.Click += action;
            return b;
        }
        Label TextLabel(string text, float size, Color color, bool bold = false)
        {
            return new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = color, UseMnemonic = false };
        }
        void BuildShell()
        {
            SuspendLayout(); var previous = Controls.Cast<Control>().ToArray(); Controls.Clear(); cards.Clear(); navigation.Clear(); devicesGrid = null; recordsGrid = null;
            var shell = new BufferedLayout { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 214)); shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(shell);
            var side = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Brand.Ink, Margin = Padding.Empty };
            side.Controls.Add(new PictureBox { Image = Brand.Logo(64), SizeMode = PictureBoxSizeMode.Zoom, Location = new Point(22, 28), Size = new Size(48, 48) });
            var title = TextLabel("ChargeTrail", 18, Color.White, true); title.Location = new Point(22, 88); side.Controls.Add(title);
            var tagline = TextLabel(T("Her şarjı takip et.", "Track every charge."), 10, Brand.Mint); tagline.Location = new Point(24, 123); side.Controls.Add(tagline);
            int y = 192;
            foreach (var nav in new[] { new[] { "home", T("Genel bakış", "Overview"), "01" }, new[] { "devices", T("Tüm cihazlar", "All devices"), "02" }, new[] { "records", T("Kayıtlar", "Records"), "03" }, new[] { "settings", T("Ayarlar ve rehber", "Settings & guide"), "04" } })
            {
                string target = nav[0];
                var b = Btn(nav[2] + "   " + nav[1], delegate { Navigate(target); });
                b.AutoSize = false; b.SetBounds(14, y, 186, 47); b.TextAlign = ContentAlignment.MiddleLeft;
                b.BackColor = page == target ? Brand.Mint : Brand.Ink; b.ForeColor = page == target ? Brand.Ink : Color.FromArgb(193, 207, 199); b.FlatAppearance.BorderSize = 0;
                navigation[target] = b;
                side.Controls.Add(b); y += 59;
            }
            var bottom = TextLabel("CHARGE. USE. REMEMBER.\n\nv2.0.1  /  Local & private", 8, Color.FromArgb(155, 177, 165));
            bottom.AutoSize = false; bottom.Dock = DockStyle.Bottom; bottom.Height = 92; bottom.Padding = new Padding(24, 12, 0, 0); side.Controls.Add(bottom);
            shell.Controls.Add(side, 0, 0);
            var right = new BufferedLayout { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(30, 22, 30, 12), Margin = Padding.Empty };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); right.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); right.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            var top = new BufferedLayout { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            var breadcrumb = TextLabel(T("ÇALIŞMA ALANI", "WORKSPACE") + "   /   " + T("EKİPMAN GÜNLÜĞÜ", "EQUIPMENT JOURNAL"), 8.5f, Brand.Muted, true); breadcrumb.Dock = DockStyle.Fill; breadcrumb.TextAlign = ContentAlignment.MiddleLeft; top.Controls.Add(breadcrumb, 0, 0);
            var language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 82, Font = new Font("Segoe UI", 10), Margin = new Padding(0, 8, 0, 0) };
            language.Items.AddRange(new object[] { "TR", "EN" }); language.SelectedItem = English ? "EN" : "TR";
            language.SelectedIndexChanged += delegate { CommitEdits(); state.Language = language.SelectedItem.ToString(); Persist(); BuildShell(); UpdateTray(); };
            top.Controls.Add(language, 1, 0); right.Controls.Add(top, 0, 0);
            content.Dock = DockStyle.Fill; content.Margin = Padding.Empty; right.Controls.Add(content, 0, 1);
            footer.Dock = DockStyle.Fill; footer.TextAlign = ContentAlignment.MiddleLeft; footer.Font = new Font("Segoe UI", 8); footer.ForeColor = Brand.Muted; right.Controls.Add(footer, 0, 2);
            shell.Controls.Add(right, 1, 0);
            BuildPage(); ResumeLayout(true); foreach (var control in previous) control.Dispose();
        }
        void Navigate(string target)
        {
            if (page == target) return;
            CommitEdits(); TickCounters(); Persist(); page = target;
            // Keep the sidebar, language selector and window handles alive across tabs.
            BuildPage();
            foreach (var entry in navigation)
            {
                entry.Value.BackColor = entry.Key == page ? Brand.Mint : Brand.Ink;
                entry.Value.ForeColor = entry.Key == page ? Brand.Ink : Color.FromArgb(193, 207, 199);
            }
        }
        void CommitEdits() { if (devicesGrid != null) devicesGrid.EndEdit(); }
        TableLayoutPanel PageLayout(string title, string subtitle, Control actions)
        {
            var layout = new BufferedLayout { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Margin = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, actions == null ? 0 : 56)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var heading = new BufferedPanel { Dock = DockStyle.Fill };
            var h = TextLabel(title, 27, Brand.Ink, true); h.Location = new Point(0, 3); heading.Controls.Add(h);
            var sub = TextLabel(subtitle, 10, Brand.Muted); sub.Location = new Point(2, 58); heading.Controls.Add(sub);
            layout.Controls.Add(heading, 0, 0); if (actions != null) layout.Controls.Add(actions, 0, 1);
            content.Controls.Add(layout); return layout;
        }
        FlowLayoutPanel Actions() { return new BufferedFlow { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, Padding = new Padding(0, 4, 0, 0) }; }
        void BuildPage()
        {
            bool freeze = content.IsHandleCreated && content.Visible;
            IntPtr window = freeze ? content.Handle : IntPtr.Zero;
            if (freeze) SendMessage(window, 0x000b, IntPtr.Zero, IntPtr.Zero); // WM_SETREDRAW
            content.SuspendLayout();
            var previous = content.Controls.Cast<Control>().ToArray();
            try
            {
                cards.Clear(); devicesGrid = null; recordsGrid = null; stats = null; cardFlow = null;
                // Construct the replacement before disposing the currently displayed page.
                if (page == "home") BuildHome(); else if (page == "devices") BuildDevices(); else if (page == "records") BuildRecords(); else BuildSettings();
                foreach (var control in previous) control.Dispose();
                UpdateLive();
            }
            finally
            {
                content.ResumeLayout(true);
                if (freeze)
                {
                    SendMessage(window, 0x000b, new IntPtr(1), IntPtr.Zero);
                    RedrawWindow(window, IntPtr.Zero, IntPtr.Zero, 0x0585); // invalidate, erase, frame, children, update now
                }
            }
            SizeCards();
        }
        void BuildHome()
        {
            var actions = Actions();
            actions.Controls.Add(Btn(T("+ Manuel sayaç", "+ Manual tracker"), AddManual, true));
            actions.Controls.Add(Btn(T("Cihaz seç", "Choose devices"), delegate { Navigate("devices"); }));
            stats = TextLabel("", 9, Brand.Muted); stats.Padding = new Padding(6, 11, 0, 0); actions.Controls.Add(stats);
            var layout = PageLayout(T("Her şarjın bir hikâyesi var.", "Every charge tells a story."), T("Seçtiğin ekipmanlar, geçen süre ve sana ait kullanım notları.", "Your selected gear, elapsed time and the details worth remembering."), actions);
            cardFlow = new BufferedFlow { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, Margin = Padding.Empty, Padding = new Padding(0, 8, 0, 10) };
            layout.Controls.Add(cardFlow, 0, 2);
            foreach (var d in state.Devices.Where(x => x.Pinned)) AddCard(d);
            if (cards.Count == 0)
            {
                var empty = TextLabel(T("Henüz cihaz seçmedin. Cihaz seç ile ana ekranını oluştur.", "No gear selected yet. Choose devices to build your overview."), 14, Brand.Muted); empty.Padding = new Padding(18, 35, 18, 35); cardFlow.Controls.Add(empty);
            }
            cardFlow.Resize += delegate { SizeCards(); }; SizeCards();
        }
        void SizeCards()
        {
            if (cardFlow == null || cardFlow.IsDisposed) return;
            int available = cardFlow.ClientSize.Width - 24;
            int columns = available >= 760 ? 2 : 1;
            foreach (var card in cards.Values) card.Panel.Width = Math.Max(325, available / columns - 14);
        }
        string Kind(TrackedDevice d)
        {
            return d.Manual ? T("MANUEL · PC SÜRESİ", "MANUAL · PC UPTIME") : d.Kind == "Klavye" ? T("KLAVYE", "KEYBOARD") : d.Kind == "Mouse" ? "MOUSE" : T("OYUN KUMANDASI", "CONTROLLER");
        }
        string Connection(string value)
        {
            if (!English) return value;
            return value == "USB / belirsiz" ? "USB / unspecified" : value == "Kablolu / diğer" ? "Wired / other" : value == "Manuel" ? "Manual" : value;
        }
        void AddCard(TrackedDevice d)
        {
            var card = new BufferedPanel { Height = 276, BackColor = Color.White, Margin = new Padding(0, 0, 14, 16), Padding = new Padding(20) };
            card.Paint += delegate(object s, PaintEventArgs e) { using (var p = new Pen(Brand.Line)) e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1); };
            var layout = new BufferedLayout { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Margin = Padding.Empty, BackColor = Color.White };
            foreach (int height in new[] { 23, 35, 59, 29, 35, 47 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            var kind = TextLabel(Kind(d) + (d.Manual ? "" : "   /   " + Connection(d.Connection)), 8, Brand.Green, true); kind.Dock = DockStyle.Fill; layout.Controls.Add(kind, 0, 0);
            var name = TextLabel(d.Name, 17, Brand.Ink, true); name.AutoSize = false; name.AutoEllipsis = true; name.Dock = DockStyle.Fill; layout.Controls.Add(name, 0, 1);
            var time = TextLabel("00:00:00", 32, Brand.Ink, true); time.Font = new Font("Consolas", 31, FontStyle.Bold); time.Dock = DockStyle.Fill; layout.Controls.Add(time, 0, 2);
            var active = TextLabel("", 9, Brand.Muted); active.Dock = DockStyle.Fill; layout.Controls.Add(active, 0, 3);
            var badge = TextLabel("", 9, Brand.Green, true); badge.Dock = DockStyle.Fill; layout.Controls.Add(badge, 0, 4);
            var buttons = Actions(); buttons.Padding = Padding.Empty;
            var toggle = Btn("", delegate { Toggle(d); });
            var save = Btn(T("Kaydet", "Save record"), delegate { SaveRecord(d); }, true);
            var edit = Btn(T("Detaylar", "Details"), delegate { EditDevice(d); });
            foreach (var b in new[] { toggle, save, edit }) { b.Padding = new Padding(9, 4, 9, 4); b.MinimumSize = new Size(60, 36); b.Margin = new Padding(0, 0, 6, 0); buttons.Controls.Add(b); }
            layout.Controls.Add(buttons, 0, 5); card.Controls.Add(layout); cardFlow.Controls.Add(card);
            cards[d] = new CardView { Panel = card, Time = time, Active = active, Status = badge, Toggle = toggle, Save = save };
        }
        void Toggle(TrackedDevice d)
        {
            TickCounters();
            if (d.Enabled) { d.Enabled = false; d.ActiveUntil = 0; }
            else
            {
                if (d.Run.Ended != null && MessageBox.Show(this, T("Önceki sonuç Kayıtlar'da korunacak. Yeni şarj için sayaç sıfırdan başlasın mı?", "Your previous result stays in Records. Start from zero for a new charge?"), "ChargeTrail", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                Records.Start(d, state.IdleSeconds);
            }
            Persist(); UpdateLive();
        }
        void SaveRecord(TrackedDevice d)
        {
            if (d.Run.Ended != null) return;
            TickCounters();
            // Capture a fixed result while notes are entered. Cancelling keeps the same measurement.
            bool wasEnabled = d.Enabled; d.Enabled = false; d.ActiveUntil = 0;
            if (!EditRun(d.Run, false)) { d.Enabled = wasEnabled; Persist(); UpdateLive(); return; }
            Records.Archive(state, d); Persist(); UpdateLive();
            footer.Text = T("Sonuç Kayıtlar'a kaydedildi. Açıp notlarını daha sonra da düzenleyebilirsin.", "Saved to Records. Open it any time to edit your notes.");
        }
        DataGridView Table()
        {
            var g = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, RowHeadersVisible = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, EnableHeadersVisualStyles = false, ColumnHeadersHeight = 46 };
            g.RowTemplate.Height = 46;
            g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(232, 238, 231), ForeColor = Brand.Ink, Font = new Font("Segoe UI", 9, FontStyle.Bold), SelectionBackColor = Color.FromArgb(232, 238, 231) };
            g.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.White, ForeColor = Brand.Ink, SelectionBackColor = Color.FromArgb(219, 242, 225), SelectionForeColor = Brand.Ink, Font = new Font("Segoe UI", 9), Padding = new Padding(6) };
            g.GridColor = Brand.Line;
            return g;
        }
        void BuildDevices()
        {
            var actions = Actions();
            actions.Controls.Add(Btn(T("Listeyi yenile", "Refresh list"), delegate { Scan(); FillDevices(); }));
            actions.Controls.Add(Btn(T("Mouse / klavye tanı", "Identify mouse / keyboard"), delegate { identify = true; identifyUntil = Native.Now() + 15; footer.Text = T("Mouse'u hareket ettir veya klavyede bir tuşa bas. (15 sn)", "Move your mouse or press a key on your keyboard. (15 sec)"); }));
            actions.Controls.Add(Btn(T("+ Manuel sayaç", "+ Manual tracker"), AddManual, true));
            var layout = PageLayout(T("Ekipman koleksiyonun", "Your equipment collection"), T("Ana ekran kutusunu işaretle. Cihaz ayarlarını açmak için Detaylar'ı kullan.", "Tick Overview to pin a device. Open Details to configure your gear."), actions);
            devicesGrid = Table();
            devicesGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = T("Ana ekran", "Overview"), FillWeight = 60 });
            devicesGrid.Columns.Add("name", T("Cihaz", "Device")); devicesGrid.Columns[1].FillWeight = 160;
            devicesGrid.Columns.Add("kind", T("Tür", "Type"));
            devicesGrid.Columns.Add("connection", T("Bağlantı", "Connection"));
            devicesGrid.Columns.Add("status", T("Durum", "Status"));
            devicesGrid.Columns.Add(new DataGridViewButtonColumn { Text = T("Detaylar", "Details"), UseColumnTextForButtonValue = true, HeaderText = "", FillWeight = 65, FlatStyle = FlatStyle.Flat });
            for (int i = 1; i < 5; i++) devicesGrid.Columns[i].ReadOnly = true;
            foreach (DataGridViewColumn c in devicesGrid.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
            devicesGrid.CurrentCellDirtyStateChanged += delegate { if (devicesGrid.IsCurrentCellDirty && devicesGrid.CurrentCell is DataGridViewCheckBoxCell) devicesGrid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            devicesGrid.CellValueChanged += delegate(object s, DataGridViewCellEventArgs e)
            {
                if (updating || e.RowIndex < 0 || e.ColumnIndex != 0) return;
                var d = (TrackedDevice)devicesGrid.Rows[e.RowIndex].Tag; d.Pinned = Convert.ToBoolean(devicesGrid.Rows[e.RowIndex].Cells[0].Value); Persist();
            };
            devicesGrid.CellContentClick += delegate(object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0 && e.ColumnIndex == 5) EditDevice((TrackedDevice)devicesGrid.Rows[e.RowIndex].Tag); };
            layout.Controls.Add(devicesGrid, 0, 2); FillDevices();
        }
        void FillDevices()
        {
            if (devicesGrid == null || devicesGrid.IsDisposed || devicesGrid.IsCurrentCellInEditMode) return;
            updating = true;
            try
            {
                if (devicesGrid.Rows.Count != state.Devices.Count)
                {
                    devicesGrid.Rows.Clear(); foreach (var d in state.Devices) { int i = devicesGrid.Rows.Add(); devicesGrid.Rows[i].Tag = d; }
                }
                foreach (DataGridViewRow row in devicesGrid.Rows)
                {
                    var d = (TrackedDevice)row.Tag;
                    row.Cells[0].Value = d.Pinned; row.Cells[1].Value = d.Name; row.Cells[2].Value = Kind(d); row.Cells[3].Value = Connection(d.Connection);
                    row.Cells[4].Value = d.Manual ? T("Manuel sayaç", "Manual tracker") : d.Present ? T("Windows'ta mevcut", "Present in Windows") : T("Görünmüyor", "Not present");
                }
            }
            finally { updating = false; }
        }
        void BuildRecords()
        {
            var actions = Actions(); actions.Controls.Add(Btn(T("CSV dışa aktar", "Export CSV"), Export, true));
            var layout = PageLayout(T("Şarj günlüğün", "Your charge journal"), T("Kaydettiğin sonuçlar burada. Kaydı açıp Hz, bağlantı ve kullanım notlarını düzenle.", "Your saved results live here. Open a record to edit polling rate and usage notes."), actions);
            recordsGrid = Table(); recordsGrid.ReadOnly = true;
            foreach (string h in new[] { T("Cihaz", "Device"), T("Kaydedildi", "Saved"), T("Bağlantı", "Connection"), T("Toplam süre", "Total time"), "Hz", T("Notlar", "Notes") }) recordsGrid.Columns.Add(h, h);
            recordsGrid.Columns[0].FillWeight = 130; recordsGrid.Columns[4].FillWeight = 45; recordsGrid.Columns[5].FillWeight = 160;
            recordsGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "", Text = T("Aç", "Open"), UseColumnTextForButtonValue = true, FillWeight = 45, FlatStyle = FlatStyle.Flat });
            foreach (var r in state.History.AsEnumerable().Reverse())
            {
                int i = recordsGrid.Rows.Add(r.Name, LocalDate(r.Ended), Connection(r.Connection), Duration(r.Manual ? r.PcSeconds : r.VisibleSeconds), r.PollingRate ?? "—", (r.Notes ?? "").Replace('\n', ' '));
                recordsGrid.Rows[i].Tag = r;
            }
            recordsGrid.CellDoubleClick += delegate(object s, DataGridViewCellEventArgs e) { OpenRecord(e.RowIndex); };
            recordsGrid.CellContentClick += delegate(object s, DataGridViewCellEventArgs e) { if (e.ColumnIndex == 6) OpenRecord(e.RowIndex); };
            layout.Controls.Add(recordsGrid, 0, 2);
        }
        void OpenRecord(int row)
        {
            if (row < 0) return;
            var r = (TestRun)recordsGrid.Rows[row].Tag;
            if (EditRun(r, true)) { Persist(); BuildPage(); }
        }
        bool EditRun(TestRun run, bool archived)
        {
            using (var dialog = Dialog(archived ? T("Kayıt detayları", "Record details") : T("Sonucu kaydet", "Save your result"), 620, 530))
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, Padding = new Padding(24) };
                foreach (int n in new[] { 42, 70, 24, 37, 27, 185, 12, 50 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, n));
                layout.Controls.Add(TextLabel(run.Name, 20, Brand.Ink, true), 0, 0);
                layout.Controls.Add(TextLabel((run.Manual ? T("Manuel PC süresi", "Manual PC uptime") : Connection(run.Connection)) + "   ·   " + Duration(run.Manual ? run.PcSeconds : run.VisibleSeconds) + "\n" + T("Başlangıç: ", "Started: ") + LocalDate(run.Started) + "\n" + T("Etkin süre: ", "Active time: ") + (run.Manual ? "—" : Duration(run.ActiveSeconds)), 10, Brand.Muted), 0, 1);
                layout.Controls.Add(TextLabel(T("Polling rate / Hz (isteğe bağlı)", "Polling rate / Hz (optional)"), 9, Brand.Ink, true), 0, 2);
                var hz = new TextBox { Text = run.PollingRate ?? "", Width = 180, MaxLength = 40 }; layout.Controls.Add(hz, 0, 3);
                layout.Controls.Add(TextLabel(T("Kullanım notların · ör. 1000 Hz, 2.4 GHz, RGB kapalı", "Your notes · e.g. 1000 Hz, 2.4 GHz, RGB off"), 9, Brand.Ink, true), 0, 4);
                var notes = new TextBox { Text = run.Notes ?? "", Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, MaxLength = 20000, BackColor = Color.White }; layout.Controls.Add(notes, 0, 5);
                var buttons = Actions(); var save = Btn(archived ? T("Değişiklikleri kaydet", "Save changes") : T("Kayıtlara kaydet", "Save to records"), null, true); save.DialogResult = DialogResult.OK;
                var cancel = Btn(T("İptal", "Cancel"), null); cancel.DialogResult = DialogResult.Cancel; buttons.Controls.Add(save); buttons.Controls.Add(cancel); layout.Controls.Add(buttons, 0, 7);
                dialog.Controls.Add(layout); dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                run.PollingRate = hz.Text.Trim(); run.Notes = notes.Text; return true;
            }
        }
        Form Dialog(string title, int width, int height)
        {
            var dialog = new Form { Text = "ChargeTrail · " + title, ClientSize = new Size(width, height), StartPosition = FormStartPosition.CenterParent, Font = Font, BackColor = Brand.Paper, ForeColor = Brand.Ink, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, Icon = Icon };
            if (diagnosticDialogFolder != null) dialog.Shown += delegate
            {
                using (var b = new Bitmap(dialog.Width, dialog.Height)) { dialog.DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height)); b.Save(Path.Combine(diagnosticDialogFolder, state.Language + "-dialog-" + width + ".png")); }
                dialog.BeginInvoke((Action)delegate { dialog.DialogResult = DialogResult.Cancel; });
            };
            return dialog;
        }
        void EditDevice(TrackedDevice d)
        {
            using (var dialog = Dialog(T("Ekipman detayları", "Equipment details"), 550, 365))
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(24) };
                layout.Controls.Add(TextLabel(T("Cihaz adı", "Device name"), 10, Brand.Ink, true));
                var name = new TextBox { Text = d.Name, Width = 480, MaxLength = 120 }; layout.Controls.Add(name);
                layout.Controls.Add(TextLabel(T("Bağlantı", "Connection"), 10, Brand.Ink, true));
                var link = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240, DisplayMember = "Display", ValueMember = "Value", Enabled = !d.Manual };
                foreach (string value in d.Manual ? new[] { "Manuel" } : new[] { "USB / belirsiz", "2.4 GHz", "Bluetooth", "Kablolu / diğer" }) link.Items.Add(new Choice { Value = value, Display = Connection(value) });
                link.SelectedIndex = Math.Max(0, link.Items.Cast<Choice>().ToList().FindIndex(x => x.Value == d.Connection)); layout.Controls.Add(link);
                var pin = new CheckBox { Text = T("Ana ekranda göster", "Show on overview"), Checked = d.Pinned, AutoSize = true }; layout.Controls.Add(pin);
                var hint = TextLabel(d.Manual ? T("Cihaz uyusa bile PC uyanıkken sayar.", "Counts PC uptime even when the device sleeps.") : T("Cihaz/alıcı görünürlüğü ve tahmini girdi etkinliği ölçülür.", "Measures device/receiver presence and estimated input activity."), 9, Brand.Muted); hint.MaximumSize = new Size(470, 60); layout.Controls.Add(hint);
                var buttons = Actions(); var save = Btn(T("Kaydet", "Save"), null, true); save.DialogResult = DialogResult.OK; var cancel = Btn(T("İptal", "Cancel"), null); cancel.DialogResult = DialogResult.Cancel; buttons.Controls.Add(save); buttons.Controls.Add(cancel); layout.Controls.Add(buttons);
                dialog.Controls.Add(layout); dialog.AcceptButton = save; dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(name.Text)) return;
                d.Name = name.Text.Trim(); d.Connection = ((Choice)link.SelectedItem).Value; d.Pinned = pin.Checked;
                if (d.Run.Ended == null) { d.Run.Name = d.Name; d.Run.Connection = d.Connection; }
                Persist(); BuildPage();
            }
        }
        void AddManual(object sender, EventArgs e)
        {
            var draft = new TrackedDevice { Name = "SC380 Pro", Manual = true, Connection = "Manuel", Pinned = true, Run = new TestRun() };
            // A dedicated lightweight dialog keeps creation separate from editing existing gear.
            using (var dialog = Dialog(T("Manuel sayaç ekle", "Add manual tracker"), 480, 210))
            {
                var label = TextLabel(T("Ekipman adı", "Equipment name"), 11, Brand.Ink, true); label.Location = new Point(24, 22);
                var name = new TextBox { Text = draft.Name, Location = new Point(24, 58), Width = 430, MaxLength = 120 };
                var hint = TextLabel(T("PC uyanıkken sayar. Cihaz bağlantısı aranmaz.", "Counts PC uptime. No device connection required."), 9, Brand.Muted); hint.Location = new Point(24, 101);
                var add = Btn(T("Ekle", "Add tracker"), null, true); add.Location = new Point(24, 145); add.Click += delegate { if (!string.IsNullOrWhiteSpace(name.Text)) dialog.DialogResult = DialogResult.OK; };
                var cancel = Btn(T("İptal", "Cancel"), null); cancel.Location = new Point(185, 145); cancel.DialogResult = DialogResult.Cancel;
                dialog.Controls.AddRange(new Control[] { label, name, hint, add, cancel }); dialog.AcceptButton = add; dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                TickCounters(); state.AddManual(name.Text); Persist(); if (page == "home") BuildPage(); else Navigate("home");
            }
        }
        void BuildSettings()
        {
            var layout = PageLayout(T("Kendi ritminde takip et.", "Track at your own pace."), T("Arka plan takibi, etkinlik ayarı ve ölçümün anlamı.", "Background tracking, activity settings and what the numbers mean."), null);
            var body = new BufferedFlow { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.White, Padding = new Padding(24) };
            var startup = new CheckBox { Text = T("Windows ile arka planda başlat", "Start in background with Windows"), AutoSize = true, Checked = IsStartup(), Margin = new Padding(0, 0, 0, 20) };
            startup.CheckedChanged += delegate
            {
                try
                {
                    SetStartup(startup.Checked);
                    footer.Text = T("Başlangıç ayarı kaydedildi.", "Startup preference saved.");
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "ChargeTrail"); updating = true; startup.Checked = IsStartup(); updating = false; }
            };
            body.Controls.Add(startup);
            body.Controls.Add(TextLabel(T("Etkinlik penceresi (sonraki ölçümler için, saniye)", "Activity window (for future measurements, seconds)"), 10, Brand.Ink, true));
            var idle = new NumericUpDown { Minimum = 1, Maximum = 600, Value = Math.Max(1, Math.Min(600, state.IdleSeconds)), Width = 100, Margin = new Padding(0, 10, 0, 24) };
            idle.ValueChanged += delegate { state.IdleSeconds = (int)idle.Value; Persist(); }; body.Controls.Add(idle);
            var guide = TextLabel(T(
                "MANUEL PC SÜRESİ\nTakip açıkken PC'nin uyanık kaldığı süreyi biriktirir. Mouse uyusa, Hz düşse veya bağlantı kesilse de sayar. PC uyku/kapalı süresi sayılmaz.\n\nCİHAZ TAKİBİ\nGörünür süre, Windows'un cihazı/alıcıyı listelediği süredir. Etkin süre, o cihazdan gelen girdilerden sonraki etkinlik pencerelerini sayar. Bu değerler doğrudan pil ölçümü değildir; bazı alıcılar cihaz kapalıyken de görünür.\n\nKAYITLAR\nPil bitince karttaki Kaydet'i kullan. Sonuç ve notlar Kayıtlar'a gider. Kaydı açıp Hz, 2.4 GHz, RGB ve kullanım koşullarını not edebilirsin. Tekrar şarj ettiğinde karttan Başlat'ı seçerek yeni ölçüme geçebilirsin.\n\nARKA PLAN\nX pencereyi tepsiye alır. Tepsiden Çık seçersen takip durur. Windows ile başlat açıkken oturum açınca devam eder. Kayıtlar 5 saniyede bir yerel olarak yazılır; tuş içeriği kaydedilmez.",
                "MANUAL PC UPTIME\nAccumulates time while your PC is awake and tracking is enabled. Device sleep, lower polling rate or disconnection do not stop it. PC sleep and shutdown time are excluded.\n\nDEVICE TRACKING\nPresence time measures when Windows lists the device/receiver. Active time counts activity windows following input from that device. These are not direct battery readings; some receivers remain visible when the device is off.\n\nRECORDS\nWhen the battery runs out, use Save record on its card. Your result and notes go to Records. Open a record to note polling rate, 2.4 GHz, RGB and usage conditions. After charging again, choose Start on the card for a fresh measurement.\n\nBACKGROUND\nX hides the window to the tray. Exit from the tray stops tracking. Start with Windows resumes after sign-in. Records are saved locally every 5 seconds; key contents are never stored."), 10, Brand.Muted);
            guide.MaximumSize = new Size(780, 0); body.Controls.Add(guide);
            layout.Controls.Add(body, 0, 2);
        }
        bool IsStartup()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) return key != null && (key.GetValue("PilTakip") != null || key.GetValue("ChargeTrail") != null);
        }
        void SetStartup(bool enabled)
        {
            if (updating) return;
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (enabled)
                {
                    Directory.CreateDirectory(Storage.Folder); string target = Path.Combine(Storage.Folder, "ChargeTrail.exe");
                    if (!string.Equals(Application.ExecutablePath, target, StringComparison.OrdinalIgnoreCase)) File.Copy(Application.ExecutablePath, target, true);
                    key.SetValue("ChargeTrail", "\"" + target + "\" --background");
                }
                else key.DeleteValue("ChargeTrail", false);
                key.DeleteValue("PilTakip", false);
            }
        }
        void UpdateTray()
        {
            var old = tray.ContextMenuStrip; var menu = new ContextMenuStrip();
            menu.Items.Add(T("ChargeTrail'i aç", "Open ChargeTrail"), null, delegate { Reveal(); });
            menu.Items.Add(T("Tümünü duraklat", "Pause all"), null, delegate { TickCounters(); foreach (var d in state.Devices) { d.Enabled = false; d.ActiveUntil = 0; } Persist(); UpdateLive(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(T("Çık · takip durur", "Exit · stops tracking"), null, delegate { Quit(); });
            tray.ContextMenuStrip = menu; tray.Text = "ChargeTrail · " + T("Arka plan takibi", "Background tracking"); if (old != null) old.Dispose();
        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            var registrations = new ushort[] { 2, 6, 4, 5, 8 }.Select(u => new Native.RawRegistration { Page = 1, Usage = u, Flags = 0x2100, Target = Handle }).ToArray();
            if (!Native.RegisterRawInputDevices(registrations, (uint)registrations.Length, (uint)Marshal.SizeOf(typeof(Native.RawRegistration)))) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            Scan(); timer.Start(); UpdateLive();
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e); if (background) Hide();
            if (Storage.Warning != null) MessageBox.Show(this, T("Ana kayıt okunamadı; son yedek kurtarıldı. Hasarlı dosya korundu.", "The main data file could not be read. The latest backup was recovered; the damaged file was preserved."), "ChargeTrail");
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0xff)
            {
                TrackedDevice d;
                if (handles.TryGetValue(Native.InputDevice(m.LParam), out d))
                {
                    double now = Native.Now(); TickCounters(now); d.LastInput = DateTime.Now;
                    if (d.Enabled && d.Run.Ended == null && !locked) d.ActiveUntil = now + d.Run.IdleSeconds;
                    if (identify && now <= identifyUntil && (d.Kind == "Mouse" || d.Kind == "Klavye"))
                    {
                        identify = false; d.Pinned = true; Persist();
                        BeginInvoke((Action)delegate { if (page == "home") BuildPage(); else Navigate("home"); Reveal(); footer.Text = T("Tanındı ve ana ekrana eklendi: ", "Identified and pinned: ") + d.Name; });
                    }
                }
            }
            else if (m.Msg == 0xfe) lastScan = -100;
            base.WndProc(ref m);
        }
        void TickCounters() { TickCounters(Native.Now()); }
        void TickCounters(double now) { foreach (var d in state.Devices) Counter.Advance(d, lastTick, now, locked); lastTick = now; }
        void Scan()
        {
            TickCounters();
            try
            {
                var found = Native.Enumerate(); foreach (var d in state.Devices) d.Present = false; handles.Clear();
                foreach (var info in found)
                {
                    var d = state.Devices.FirstOrDefault(x => string.Equals(x.Id, info.Id, StringComparison.OrdinalIgnoreCase));
                    if (d == null) { d = new TrackedDevice { Id = info.Id, Name = info.Name.Trim(), Kind = info.Kind, Connection = info.Transport }; d.Run = Records.Fresh(d, state.IdleSeconds); state.Devices.Add(d); }
                    d.Present = true; handles[info.Handle] = d;
                }
                foreach (var d in state.Devices.Where(x => !x.Present)) d.ActiveUntil = 0;
                error = "";
            }
            catch { foreach (var d in state.Devices) { d.Present = false; d.ActiveUntil = 0; } handles.Clear(); error = T("Cihaz listesi okunamadı. Listeyi yenile ile tekrar dene.", "Could not read devices. Try Refresh list."); }
            lastScan = Native.Now(); FillDevices();
        }
        void TimerTick(object sender, EventArgs e)
        {
            double now = Native.Now(); TickCounters(now);
            if (showEvent.WaitOne(0)) Reveal();
            if (now - lastScan >= 5) Scan();
            if (now - lastSave >= 5) { Persist(); lastSave = now; }
            if (identify && now > identifyUntil) { identify = false; footer.Text = T("Tanıma süresi doldu. Tekrar deneyebilirsin.", "Identification timed out. You can try again."); }
            UpdateLive();
        }
        void UpdateLive()
        {
            foreach (var pair in cards)
            {
                var d = pair.Key; var v = pair.Value; bool saved = d.Run.Ended != null;
                v.Time.Text = Duration(d.Manual ? d.Run.PcSeconds : d.Run.VisibleSeconds);
                v.Active.Text = d.Manual ? T("PC uyanıkken geçen toplam süre", "Total time while your PC is awake") : T("Etkin süre (tahmini)   ", "Estimated active time   ") + Duration(d.Run.ActiveSeconds);
                v.Status.Text = saved ? T("✓ Kayıtlara kaydedildi", "✓ Saved to records") : !d.Enabled ? T("● Duraklatıldı", "● Paused") : d.Manual ? T("● PC süresi sayılıyor", "● Counting PC uptime") : d.Present ? T("● Takip ediliyor", "● Tracking") : T("● Cihaz bekleniyor", "● Waiting for device");
                v.Toggle.Text = d.Enabled ? T("Duraklat", "Pause") : T("Başlat", "Start");
                v.Save.Enabled = !saved;
            }
            if (stats != null && !stats.IsDisposed && page == "home") stats.Text = state.Devices.Count(x => x.Pinned) + T(" ekipman  /  ", " devices  /  ") + state.Devices.Count(x => x.Enabled && x.Run.Ended == null) + T(" aktif", " active");
            if (error != "") { footer.Text = error; footer.ForeColor = Color.Firebrick; }
            else { footer.ForeColor = Brand.Muted; if (!identify) footer.Text = T("● Yerel kayıt   ·   Arka plan takibi açık   ·   ", "● Local storage   ·   Background tracker running   ·   ") + DateTime.Now.ToString("HH:mm:ss"); }
        }
        bool Persist()
        {
            try { Storage.Save(state); if (error.StartsWith("Kayıt") || error.StartsWith("Could not save")) error = ""; return true; }
            catch { error = T("Kayıt yazılamadı. Disk alanını ve kayıt klasörünü kontrol et.", "Could not save data. Check disk space and the data folder."); footer.Text = error; footer.ForeColor = Color.Firebrick; return false; }
        }
        public static string Duration(double seconds) { long n = (long)Math.Max(0, seconds); return string.Format("{0:00}:{1:00}:{2:00}", n / 3600, n / 60 % 60, n % 60); }
        public static string LocalDate(string value) { DateTime d; return DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out d) ? d.ToLocalTime().ToString("dd.MM.yyyy HH:mm") : "—"; }
        static string Csv(string value)
        {
            value = value ?? "";
            if (value.Length > 0 && "=+-@".IndexOf(value[0]) >= 0) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        public static string ExportCsv(State data)
        {
            var b = new StringBuilder("Device;Connection;Started;Saved;Mode;Presence seconds;Active seconds;PC seconds;PC duration;Polling rate Hz;Notes\r\n");
            foreach (var r in data.History)
                b.AppendLine(string.Join(";", new[] { Csv(r.Name), Csv(r.Connection), Csv(r.Started), Csv(r.Ended), Csv(r.Manual ? "Manual PC uptime" : "Device tracking"), ((long)r.VisibleSeconds).ToString(), ((long)r.ActiveSeconds).ToString(), ((long)r.PcSeconds).ToString(), Csv(Duration(r.PcSeconds)), Csv(r.PollingRate), Csv(r.Notes) }));
            return b.ToString();
        }
        void Export(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "ChargeTrail-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".csv" })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    try { File.WriteAllText(dialog.FileName, ExportCsv(state), new UTF8Encoding(true)); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "ChargeTrail"); }
        }
        void PowerChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate { TickCounters(); foreach (var d in state.Devices) d.ActiveUntil = 0; lastScan = -100; Persist(); });
        }
        void SessionChanged(object sender, SessionSwitchEventArgs e)
        {
            if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate
            {
                TickCounters();
                if (e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.RemoteDisconnect || e.Reason == SessionSwitchReason.ConsoleDisconnect) locked = true;
                if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.ConsoleConnect || e.Reason == SessionSwitchReason.RemoteConnect) locked = false;
                foreach (var d in state.Devices) d.ActiveUntil = 0;
            });
        }
        void Reveal() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        void HandleClosing(object sender, FormClosingEventArgs e)
        {
            CommitEdits(); TickCounters(); Persist();
            if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); tray.ShowBalloonTip(1800, "ChargeTrail", T("Takip arka planda devam ediyor.", "Tracking continues in the background."), ToolTipIcon.Info); }
        }
        void Quit()
        {
            CommitEdits(); TickCounters();
            if (!Persist() && MessageBox.Show(this, T("Son değişiklikler kaydedilemedi. Yine de çıkılsın mı?", "Latest changes could not be saved. Exit anyway?"), "ChargeTrail", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            exiting = true; Close();
        }
        internal void EndDiagnostics() { exiting = true; Close(); }
        internal void CheckNavigation()
        {
            Show();
            Control shell = Controls[0];
            var buttons = navigation.ToDictionary(x => x.Key, x => x.Value);
            IntPtr shellHandle = shell.Handle;
            for (int repeat = 0; repeat < 3; repeat++)
                foreach (string target in new[] { "devices", "records", "settings", "home" })
                {
                    Navigate(target);
                    if (!ReferenceEquals(shell, Controls[0]) || shell.Handle != shellHandle || content.Controls.Count != 1 || !content.Visible || buttons.Any(x => !ReferenceEquals(x.Value, navigation[x.Key])))
                        throw new Exception("Navigation replaced the shell or left the content hidden/duplicated.");
                    Control current = content.Controls[0];
                    Navigate(target);
                    if (!ReferenceEquals(current, content.Controls[0])) throw new Exception("Selecting the current tab rebuilt its page.");
                }
        }
        internal void CaptureDiagnostics(string folder)
        {
            Directory.CreateDirectory(folder);
            Show();
            string originalLanguage = state.Language, originalPage = page;
            foreach (string language in new[] { "TR", "EN" })
            {
                state.Language = language;
                BuildShell();
                foreach (string target in new[] { "home", "devices", "records", "settings" })
                {
                    Navigate(target); PerformLayout();
                    using (var b = new Bitmap(Width, Height)) { DrawToBitmap(b, new Rectangle(0, 0, Width, Height)); b.Save(Path.Combine(folder, language + "-" + target + ".png")); }
                }
                diagnosticDialogFolder = folder;
                EditRun(state.History.Last(), true); EditDevice(state.Devices.First()); AddManual(this, EventArgs.Empty);
                diagnosticDialogFolder = null;
            }
            state.Language = originalLanguage; page = originalPage; BuildShell();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose(); tray.Dispose(); SystemEvents.PowerModeChanged -= PowerChanged; SystemEvents.SessionSwitch -= SessionChanged; }
            base.Dispose(disposing);
        }
    }
}
