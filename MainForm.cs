using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ObsPortableStreamDeckInstaller;

public sealed class MainForm : Form
{
    private readonly TextBox txtObsFolder = new();
    private readonly TextBox txtLog = new();
    private readonly Button btnBrowse = new();
    private readonly Button btnInstall = new();
    private readonly CheckBox chkBackup = new();
    private readonly Label lblStatus = new();

    private static readonly string ProgramDataPath =
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    private static readonly string SourcePluginRoot =
        Path.Combine(ProgramDataPath, "obs-studio", "plugins", "StreamDeckPlugin");

    private static readonly string SourceBinFolder =
        Path.Combine(SourcePluginRoot, "bin", "64bit");

    private static readonly string SourceDataFolder =
        Path.Combine(SourcePluginRoot, "Data");

    public MainForm()
    {
        Text = "Stream Deck Portable OBS Installer";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(860, 600);
        MinimumSize = new Size(760, 500);
        Font = new Font("Segoe UI", 9F);

        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        BuildLayout();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 6
        };

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "Install / Update Elgato Stream Deck plugin for Portable OBS",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        };

        var instructions = new Label
        {
            Text =
                "Choose your OBS portable folder. You can select either the outer portable folder or the actual obs-studio folder.\n" +
                "The app will copy the Stream Deck OBS plugin files from C:\\ProgramData into the portable OBS folder.",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };

        var pathPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 10)
        };

        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        txtObsFolder.Dock = DockStyle.Fill;
        txtObsFolder.Margin = new Padding(0, 0, 8, 0);

        btnBrowse.Text = "Browse...";
        btnBrowse.AutoSize = true;
        btnBrowse.Click += BtnBrowse_Click;

        pathPanel.Controls.Add(txtObsFolder, 0, 0);
        pathPanel.Controls.Add(btnBrowse, 1, 0);

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };

        chkBackup.Text = "Back up existing plugin files first";
        chkBackup.Checked = true;
        chkBackup.AutoSize = true;
        chkBackup.Margin = new Padding(0, 6, 18, 0);

        btnInstall.Text = "Install / Update Plugin";
        btnInstall.AutoSize = true;
        btnInstall.Click += BtnInstall_Click;

        actionPanel.Controls.Add(chkBackup);
        actionPanel.Controls.Add(btnInstall);

        lblStatus.Text = "Ready.";
        lblStatus.AutoSize = true;
        lblStatus.Margin = new Padding(0, 0, 0, 8);

        txtLog.Dock = DockStyle.Fill;
        txtLog.Multiline = true;
        txtLog.ScrollBars = ScrollBars.Vertical;
        txtLog.ReadOnly = true;
        txtLog.WordWrap = false;
        txtLog.Font = new Font("Consolas", 9F);

        var sourceInfo = new Label
        {
            Text = $"Expected source: {SourcePluginRoot}",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 8, 0, 0)
        };

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(instructions, 0, 1);
        root.Controls.Add(pathPanel, 0, 2);
        root.Controls.Add(actionPanel, 0, 3);
        root.Controls.Add(txtLog, 0, 4);
        root.Controls.Add(sourceInfo, 0, 5);

        Controls.Add(root);
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select your OBS portable folder",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtObsFolder.Text = dialog.SelectedPath;
        }
    }

    private void BtnInstall_Click(object? sender, EventArgs e)
    {
        txtLog.Clear();
        lblStatus.Text = "Working...";
        btnInstall.Enabled = false;

        try
        {
            InstallOrUpdatePlugin(txtObsFolder.Text.Trim(), chkBackup.Checked);

            lblStatus.Text = "Done.";
            Log("");
            Log("Success. Launch portable OBS and check Tools > Elgato Stream Deck.");
            MessageBox.Show(
                this,
                "Stream Deck OBS plugin files were copied successfully.",
                "Done",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Failed.";
            Log("");
            Log("ERROR:");
            Log(ex.Message);

            MessageBox.Show(
                this,
                ex.Message,
                "Install failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
        finally
        {
            btnInstall.Enabled = true;
        }
    }

    private void InstallOrUpdatePlugin(string selectedFolder, bool createBackup)
    {
        if (string.IsNullOrWhiteSpace(selectedFolder))
            throw new InvalidOperationException("Please select your OBS portable folder first.");

        if (!Directory.Exists(selectedFolder))
            throw new DirectoryNotFoundException("The selected OBS folder does not exist.");

        Log("Checking source files...");

        if (!Directory.Exists(SourcePluginRoot))
        {
            throw new DirectoryNotFoundException(
                "Could not find the Elgato Stream Deck OBS plugin folder in ProgramData.\n\n" +
                "Expected:\n" +
                SourcePluginRoot + "\n\n" +
                "Install or update the Elgato Stream Deck app first, then try again."
            );
        }

        if (!Directory.Exists(SourceBinFolder))
        {
            throw new DirectoryNotFoundException(
                "Could not find the Stream Deck plugin bin folder:\n" + SourceBinFolder
            );
        }

        string sourceData = FindExistingDataFolder();
        Log("Source found:");
        Log(SourcePluginRoot);

        Log("");
        Log("Checking OBS portable folder...");

        string obsRoot = ResolveObsRoot(selectedFolder)
            ?? throw new DirectoryNotFoundException(
                "This does not look like a valid OBS portable folder.\n\n" +
                "The app expects one of these structures:\n\n" +
                "SelectedFolder\\obs-studio\\obs-plugins\\64bit\n" +
                "SelectedFolder\\obs-studio\\data\\obs-plugins\n\n" +
                "or:\n\n" +
                "SelectedFolder\\obs-plugins\\64bit\n" +
                "SelectedFolder\\data\\obs-plugins"
            );

        Log("OBS root detected:");
        Log(obsRoot);

        WarnIfObsIsRunning();

        string targetBin = Path.Combine(obsRoot, "obs-plugins", "64bit");
        string targetDataRoot = Path.Combine(obsRoot, "data", "obs-plugins");
        string targetStreamDeckData = Path.Combine(targetDataRoot, "StreamDeckPlugin");

        Directory.CreateDirectory(targetStreamDeckData);

        if (createBackup)
        {
            Log("");
            Log("Backing up existing plugin files...");
            BackupExistingPluginFiles(obsRoot, targetBin, targetStreamDeckData);
        }

        Log("");
        Log("Copying plugin binary files...");

        CopyRequiredFile(SourceBinFolder, targetBin, "StreamDeckPlugin.dll");

        // PDB files are debug symbol files. Elgato mentions copying them,
        // but the plugin should not depend on them at runtime.
        CopyOptionalFile(SourceBinFolder, targetBin, "StreamDeckPlugin.pdb");

        Log("");
        Log("Copying plugin data files...");

        CopyRequiredFile(sourceData, targetStreamDeckData, "StreamDeckPluginQt6.dll");
        CopyOptionalFile(sourceData, targetStreamDeckData, "StreamDeckPluginQt6.pdb");

        // OBS 32+ support files. Copy them if the installed Elgato plugin has them.
        CopyOptionalFile(sourceData, targetStreamDeckData, "StreamDeckPluginOBS32.dll");
        CopyOptionalFile(sourceData, targetStreamDeckData, "StreamDeckPluginOBS32.pdb");

        CopyRequiredDirectory(sourceData, targetStreamDeckData, "Locale");

        Log("");
        Log("Install/update complete.");
    }

    private static string? ResolveObsRoot(string selectedFolder)
    {
        // Case 1:
        // User selected the actual obs-studio folder.
        if (LooksLikeObsRoot(selectedFolder))
            return selectedFolder;

        // Case 2:
        // User selected the outer portable OBS folder that contains obs-studio.
        string nestedObsStudio = Path.Combine(selectedFolder, "obs-studio");
        if (LooksLikeObsRoot(nestedObsStudio))
            return nestedObsStudio;

        return null;
    }

    private static bool LooksLikeObsRoot(string folder)
    {
        if (!Directory.Exists(folder))
            return false;

        bool hasPluginFolder = Directory.Exists(Path.Combine(folder, "obs-plugins", "64bit"));
        bool hasDataFolder = Directory.Exists(Path.Combine(folder, "data", "obs-plugins"));

        return hasPluginFolder && hasDataFolder;
    }

    private string FindExistingDataFolder()
    {
        if (Directory.Exists(SourceDataFolder))
            return SourceDataFolder;

        string lowercaseData = Path.Combine(SourcePluginRoot, "data");
        if (Directory.Exists(lowercaseData))
            return lowercaseData;

        throw new DirectoryNotFoundException(
            "Could not find the Stream Deck plugin Data folder.\n\n" +
            "Expected:\n" +
            SourceDataFolder
        );
    }

    private void CopyRequiredFile(string sourceFolder, string targetFolder, string fileName)
    {
        string sourcePath = Path.Combine(sourceFolder, fileName);
        string targetPath = Path.Combine(targetFolder, fileName);

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Required file was not found:\n" + sourcePath);

        Directory.CreateDirectory(targetFolder);
        File.Copy(sourcePath, targetPath, overwrite: true);

        Log($"Copied: {fileName}");
    }

    private void CopyOptionalFile(string sourceFolder, string targetFolder, string fileName)
    {
        string sourcePath = Path.Combine(sourceFolder, fileName);
        string targetPath = Path.Combine(targetFolder, fileName);

        if (!File.Exists(sourcePath))
        {
            Log($"Skipped optional file: {fileName}");
            return;
        }

        Directory.CreateDirectory(targetFolder);
        File.Copy(sourcePath, targetPath, overwrite: true);

        Log($"Copied: {fileName}");
    }

    private void CopyRequiredDirectory(string sourceFolder, string targetFolder, string directoryName)
    {
        string sourcePath = Path.Combine(sourceFolder, directoryName);
        string targetPath = Path.Combine(targetFolder, directoryName);

        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException("Required folder was not found:\n" + sourcePath);

        CopyDirectory(sourcePath, targetPath, overwrite: true);

        Log($"Copied folder: {directoryName}");
    }

    private void BackupExistingPluginFiles(string obsRoot, string targetBin, string targetStreamDeckData)
    {
        string backupRoot = Path.Combine(
            obsRoot,
            "_StreamDeckPluginBackups",
            DateTime.Now.ToString("yyyyMMdd_HHmmss")
        );

        bool backedUpAnything = false;

        string[] binFiles =
        {
            "StreamDeckPlugin.dll",
            "StreamDeckPlugin.pdb"
        };

        foreach (string file in binFiles)
        {
            string sourcePath = Path.Combine(targetBin, file);

            if (!File.Exists(sourcePath))
                continue;

            string targetPath = Path.Combine(backupRoot, "obs-plugins", "64bit", file);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(sourcePath, targetPath, overwrite: true);
            backedUpAnything = true;
        }

        string[] dataFiles =
        {
            "StreamDeckPluginQt6.dll",
            "StreamDeckPluginQt6.pdb",
            "StreamDeckPluginOBS32.dll",
            "StreamDeckPluginOBS32.pdb"
        };

        foreach (string file in dataFiles)
        {
            string sourcePath = Path.Combine(targetStreamDeckData, file);

            if (!File.Exists(sourcePath))
                continue;

            string targetPath = Path.Combine(
                backupRoot,
                "data",
                "obs-plugins",
                "StreamDeckPlugin",
                file
            );

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(sourcePath, targetPath, overwrite: true);
            backedUpAnything = true;
        }

        string localeSource = Path.Combine(targetStreamDeckData, "Locale");

        if (Directory.Exists(localeSource))
        {
            string localeBackup = Path.Combine(
                backupRoot,
                "data",
                "obs-plugins",
                "StreamDeckPlugin",
                "Locale"
            );

            CopyDirectory(localeSource, localeBackup, overwrite: true);
            backedUpAnything = true;
        }

        if (backedUpAnything)
            Log("Backup created:");
        else
            Log("No existing plugin files found to back up.");

        if (backedUpAnything)
            Log(backupRoot);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory, bool overwrite)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, file);
            string targetFile = Path.Combine(targetDirectory, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(file, targetFile, overwrite);
        }
    }

    private void WarnIfObsIsRunning()
    {
        bool obs64Running = IsProcessRunning("obs64");
        bool obs32Running = IsProcessRunning("obs32");

        if (obs64Running || obs32Running)
        {
            Log("");
            Log("WARNING: OBS appears to be running.");
            Log("If copying fails, close OBS and run this installer again.");
        }
    }

    private static bool IsProcessRunning(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Any();
        }
        catch
        {
            return false;
        }
    }

    private void Log(string message)
    {
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}