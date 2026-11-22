using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

class MainForm : Form
{
    readonly TextBox pathBox = new() { AllowDrop = true };
    readonly TextBox startBox = new() { Text = "0:00" };
    readonly TextBox endBox = new();
    readonly ComboBox modeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly Label infoLabel = new() { AutoSize = true,  Text = "Drop a video here or paste its path." };
    readonly Label ffLabel = new() { AutoSize = false, AutoEllipsis = true, Dock = DockStyle.Fill, Height = 24, Font = new Font("Segoe UI", 8.5f), ForeColor = SystemColors.GrayText };
    readonly ProgressBar bar = new() { Minimum = 0, Maximum = 1000 };
    readonly Button goBtn = new() { Text = "Create clip" };
    readonly Button cancelBtn = new() { Text = "Cancel", Enabled = false };
    readonly Button openBtn = new() { Text = "Show in folder", Enabled = false };
    readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

    string? ffmpeg;
    double duration = -1;
    string? lastOutput;
    Process? proc;
    bool cancelled;

    public MainForm()
    {
        Text = "Video Clipper";
        Font = new Font("Segoe UI", 10f);
        AutoScaleMode = AutoScaleMode.Font;
        AutoScaleDimensions = new SizeF(7F, 15F);
        float k = DeviceDpi / 96f;
        ClientSize = new Size((int)(640 * k), (int)(400 * k));
        MinimumSize = new Size((int)(600 * k), (int)(380 * k));
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(14), RowCount = 8 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        Controls.Add(grid);

        foreach (var c in new Control[] { pathBox, startBox, endBox, modeBox, bar, log })
        { c.Dock = DockStyle.Fill; c.Margin = new Padding(4, 5, 4, 5); }
        infoLabel.Margin = new Padding(4, 4, 4, 8);
        ffLabel.Margin = new Padding(4, 6, 4, 0); ffLabel.Height = 26;
        foreach (var b in new[] { goBtn, cancelBtn, openBtn })
        { b.AutoSize = true; b.AutoSizeMode = AutoSizeMode.GrowAndShrink; b.Padding = new Padding(10, 3, 10, 3); b.Margin = new Padding(4, 4, 8, 4); }

        var browse = new Button { Text = "Browse...", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(4, 3, 4, 3) };
        grid.Controls.Add(Lbl("Video"), 0, 0);
        grid.Controls.Add(pathBox, 1, 0); grid.SetColumnSpan(pathBox, 2);
        grid.Controls.Add(browse, 3, 0);

        grid.Controls.Add(Lbl("Start"), 0, 1);
        grid.Controls.Add(startBox, 1, 1);
        grid.Controls.Add(Lbl("End"), 2, 1);
        grid.Controls.Add(endBox, 3, 1);

        grid.Controls.Add(Lbl("Mode"), 0, 2);
        modeBox.Items.AddRange(new object[] {
            "Accurate (re-encode video, exact cut)",
            "Instant (no re-encode, cut snaps to keyframes)" });
        modeBox.SelectedIndex = 1;
        grid.Controls.Add(modeBox, 1, 2); grid.SetColumnSpan(modeBox, 3);

        grid.Controls.Add(infoLabel, 1, 3); grid.SetColumnSpan(infoLabel, 3);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        buttons.Controls.AddRange(new Control[] { goBtn, cancelBtn, openBtn });
        grid.Controls.Add(buttons, 1, 4); grid.SetColumnSpan(buttons, 3);

        bar.Height = 22; bar.Dock = DockStyle.Top;
        grid.Controls.Add(bar, 0, 5); grid.SetColumnSpan(bar, 4);
        grid.Controls.Add(log, 0, 6); grid.SetColumnSpan(log, 4);
        grid.Controls.Add(ffLabel, 0, 7); grid.SetColumnSpan(ffLabel, 4);

        for (int i = 0; i < 8; i++)
            grid.RowStyles.Add(i == 6 ? new RowStyle(SizeType.Percent, 100) : i == 7 ? new RowStyle(SizeType.Absolute, (int)(30 * k)) : new RowStyle(SizeType.AutoSize));

        browse.Click += (_, _) =>
        {
            using var d = new OpenFileDialog { Filter = "Video files|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.m4v|All files|*.*" };
            if (d.ShowDialog() == DialogResult.OK) pathBox.Text = d.FileName;
        };
        DragEnter += OnDrag; DragDrop += OnDrop;
        pathBox.DragEnter += OnDrag; pathBox.DragDrop += OnDrop;
        pathBox.Leave += async (_, _) => await ProbeAsync();
        // Pasting "00:30 -> 9:39" into Start splits it across both boxes.
        startBox.TextChanged += (_, _) =>
        {
            var m = Regex.Match(startBox.Text, @"^\s*(.+?)\s*(?:->|=>|\u2013|\u2014|\bto\b)\s*(.+?)\s*$");
            if (m.Success) { startBox.Text = m.Groups[1].Value; endBox.Text = m.Groups[2].Value; }
        };
        goBtn.Click += async (_, _) => await RunAsync();
        cancelBtn.Click += (_, _) => { cancelled = true; try { proc?.Kill(true); } catch { } };
        openBtn.Click += (_, _) =>
        {
            if (lastOutput != null && File.Exists(lastOutput))
                Process.Start("explorer.exe", "/select,\"" + lastOutput + "\"");
        };
        FindFfmpeg();
    }

    static Label Lbl(string t) => new() { Text = t, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(4, 8, 10, 8) };

    void OnDrag(object? s, DragEventArgs e) { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; }
    async void OnDrop(object? s, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] f && f.Length > 0) { pathBox.Text = f[0]; await ProbeAsync(); }
    }

    void FindFfmpeg()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe"),
            Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"),
        };
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            try { candidates.Add(Path.Combine(dir.Trim('"'), "ffmpeg.exe")); } catch { }
        ffmpeg = candidates.FirstOrDefault(File.Exists);
        if (ffmpeg == null)
        {
            using var d = new OpenFileDialog { Title = "Locate ffmpeg.exe", Filter = "ffmpeg.exe|ffmpeg.exe" };
            if (d.ShowDialog() == DialogResult.OK) ffmpeg = d.FileName;
        }
        ffLabel.Text = ffmpeg == null ? "ffmpeg: NOT FOUND (place ffmpeg.exe next to this app)" : "ffmpeg: " + ffmpeg;
        goBtn.Enabled = ffmpeg != null;
    }

    // Accepts SS, M:SS, MM:SS, H:MM:SS (optionally with .fraction).
    static bool TryParseTime(string s, out double secs)
    {
        secs = 0;
        var parts = s.Trim().Split(':');
        if (parts.Length is < 1 or > 3) return false;
        double total = 0;
        foreach (var p in parts)
        {
            if (!double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v < 0) return false;
            total = total * 60 + v;
        }
        secs = total; return true;
    }

    static string Fmt(double s) { var t = TimeSpan.FromSeconds(Math.Round(s)); return $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"; }

    async Task ProbeAsync()
    {
        duration = -1;
        var p = pathBox.Text.Trim().Trim('"');
        if (ffmpeg == null || !File.Exists(p)) { infoLabel.Text = p.Length == 0 ? "Drop a video here or paste its path." : "File not found."; return; }
        var psi = new ProcessStartInfo(ffmpeg, "-hide_banner -i \"" + p + "\"") { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        using var pr = Process.Start(psi)!;
        var err = await pr.StandardError.ReadToEndAsync();
        await pr.WaitForExitAsync();
        var m = Regex.Match(err, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
        if (m.Success)
        {
            duration = int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60 + double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            infoLabel.Text = "Video length: " + Fmt(duration);
            if (endBox.Text.Trim().Length == 0) endBox.Text = Fmt(duration);
        }
        else infoLabel.Text = "Couldn't read this file as a video.";
    }

    async Task<bool> HasEncoder(string name)
    {
        var psi = new ProcessStartInfo(ffmpeg!, "-hide_banner -encoders") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        using var pr = Process.Start(psi)!;
        var o = await pr.StandardOutput.ReadToEndAsync();
        await pr.WaitForExitAsync();
        return Regex.IsMatch(o, @"\s" + Regex.Escape(name) + @"\s");
    }

    void Log(string s) => log.AppendText(s + Environment.NewLine);

    static string TimeTag(double s)
    {
        var t = TimeSpan.FromSeconds(Math.Round(s));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h{t.Minutes:00}m{t.Seconds:00}s" : $"{t.Minutes}m{t.Seconds:00}s";
    }

    async Task RunAsync()
    {
        var input = pathBox.Text.Trim().Trim('"');
        if (!File.Exists(input)) { MessageBox.Show("Video file not found."); return; }
        if (!TryParseTime(startBox.Text, out var start) || !TryParseTime(endBox.Text, out var end))
        { MessageBox.Show("Enter times like 0:30, 2:42 or 1:02:03."); return; }
        if (duration < 0) await ProbeAsync();
        if (duration > 0 && end > duration) end = duration;
        if (end <= start) { MessageBox.Show("End must be after Start."); return; }

        var dir = Path.GetDirectoryName(input)!;
        var name = Path.GetFileNameWithoutExtension(input);
        var output = Path.Combine(dir, $"{name} - clip {TimeTag(start)}-{TimeTag(end)}.mp4");
        for (int i = 2; File.Exists(output); i++)
            output = Path.Combine(dir, $"{name} - clip {TimeTag(start)}-{TimeTag(end)} ({i}).mp4");

        var inv = CultureInfo.InvariantCulture;
        var args = "-hide_banner -loglevel error -nostats -progress pipe:1 -y -ss " + start.ToString("0.###", inv)
                 + " -i \"" + input + "\" -t " + (end - start).ToString("0.###", inv) + " ";
        if (modeBox.SelectedIndex == 1) args += "-c copy ";
        else
        {
            string v = await HasEncoder("h264_nvenc") ? "-c:v h264_nvenc -preset p5 -cq 19 -b:v 0"
                     : await HasEncoder("libx264") ? "-c:v libx264 -preset fast -crf 18"
                     : await HasEncoder("h264_qsv") ? "-c:v h264_qsv -global_quality 20"
                     : await HasEncoder("h264_amf") ? "-c:v h264_amf -quality quality"
                     : "-c:v libopenh264 -b:v 40M";
            args += v + " -c:a copy ";
        }
        args += "-movflags +faststart \"" + output + "\"";

        log.Clear(); Log("Output: " + output);
        cancelled = false; lastOutput = output; bar.Value = 0;
        goBtn.Enabled = false; cancelBtn.Enabled = true; openBtn.Enabled = false;

        var psi = new ProcessStartInfo(ffmpeg!, args) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        proc = Process.Start(psi)!;
        double len = end - start;
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            var m = Regex.Match(e.Data, @"^out_time_us=(\d+)");
            if (m.Success) BeginInvoke(() => bar.Value = (int)Math.Clamp(double.Parse(m.Groups[1].Value) / 1e6 / len * 1000, 0, 1000));
        };
        proc.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) BeginInvoke(() => Log(e.Data)); };
        proc.BeginOutputReadLine(); proc.BeginErrorReadLine();
        await proc.WaitForExitAsync();
        int code = proc.ExitCode; proc.Dispose(); proc = null;

        goBtn.Enabled = true; cancelBtn.Enabled = false;
        if (cancelled) { TryDelete(output); Log("Cancelled."); bar.Value = 0; }
        else if (code != 0 || !File.Exists(output) || new FileInfo(output).Length == 0)
        { TryDelete(output); Log("Failed (exit code " + code + ")."); MessageBox.Show("ffmpeg failed. See the log for details."); }
        else
        {
            bar.Value = 1000; openBtn.Enabled = true;
            Log($"Done: {new FileInfo(output).Length / 1048576.0:0.#} MB, {Fmt(len)} long.");
        }
    }

    static void TryDelete(string p) { try { File.Delete(p); } catch { } }
}

