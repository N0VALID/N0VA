# N0VA desktop build: 2.1
# Seemless 0S MODE - N0VA's keyboard-driven terminal desktop.
# Windows PowerShell 5.1+, no third-party modules required.
[CmdletBinding()]
param([string]$InstallDir = '', [switch]$NoRun)

$ErrorActionPreference = 'Stop'
if (-not $InstallDir) {
    $desktop = [Environment]::GetFolderPath('Desktop')
    if (-not $desktop) { $desktop = [Environment]::GetFolderPath('UserProfile') }
    $InstallDir = Join-Path $desktop 'N0VA'
}
if (-not ('SeemlessCanvas' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public sealed class SeemlessCanvas {
    public readonly int Width, Height;
    private readonly char[] chars;
    private readonly int[] foreground, background;
    public SeemlessCanvas(int width, int height, int fg, int bg) {
        Width=width; Height=height;
        chars=new char[width*height]; foreground=new int[chars.Length]; background=new int[chars.Length];
        for(int i=0;i<chars.Length;i++){chars[i]=' ';foreground[i]=fg;background[i]=bg;}
    }
    public void Fill(int x,int y,int width,int height,int fg,int bg) {
        for(int row=Math.Max(0,y);row<Math.Min(Height,y+height);row++)
            for(int col=Math.Max(0,x);col<Math.Min(Width,x+width);col++) {
                int i=row*Width+col;chars[i]=' ';foreground[i]=fg;background[i]=bg;
            }
    }
    public void Text(int x,int y,string text,int fg,int bg) {
        if(y<0||y>=Height||text==null)return;
        for(int j=0;j<text.Length;j++) {
            int col=x+j;if(col<0)continue;if(col>=Width)break;
            char c=text[j];if(c<32||c==127)c=' ';if(c>126)c='?';
            int i=y*Width+col;chars[i]=c;foreground[i]=fg;background[i]=bg;
        }
    }
    public string Frame(bool ansi) {
        StringBuilder b=new StringBuilder(Width*Height*2);int fg=-1,bg=-1;
        for(int row=0;row<Height;row++) {
            for(int col=0;col<Width;col++) {
                int i=row*Width+col;
                if(ansi&&(fg!=foreground[i]||bg!=background[i])) {
                    fg=foreground[i];bg=background[i];
                    b.Append("\x1b[38;2;").Append((fg>>16)&255).Append(';').Append((fg>>8)&255).Append(';').Append(fg&255)
                     .Append(";48;2;").Append((bg>>16)&255).Append(';').Append((bg>>8)&255).Append(';').Append(bg&255).Append('m');
                }
                b.Append(chars[i]);
            }
            if(row<Height-1)b.Append("\r\n");
        }
        if(ansi)b.Append("\x1b[0m");return b.ToString();
    }
}
public static class SeemlessNative {
    [DllImport("kernel32.dll")] public static extern IntPtr GetStdHandle(int kind);
    [DllImport("kernel32.dll")] public static extern bool GetConsoleMode(IntPtr handle,out uint mode);
    [DllImport("kernel32.dll")] public static extern bool SetConsoleMode(IntPtr handle,uint mode);
}
'@
}
$script:SC = @{ Ink=0xE7EDF8; Muted=0x94A8C9; Accent=0x55C8FF; Blue=0x1675DF; Desktop=0x061A36; Panel=0x11243E; Side=0x0B1C33; Line=0x2C4A71; Select=0x174E7A; Green=0x6CE8AD; Gold=0xF8D580; Red=0xFFACB0 }
$script:SeemlessApps = @(
    @{Name='Brave';Id='Brave.Brave';Category='Browser';Detail='Privacy-focused web browser'},
    @{Name='Steam';Id='Valve.Steam';Category='Games';Detail='Games library and downloads'},
    @{Name='RustDesk';Id='RustDesk.RustDesk';Category='Remote';Detail='Remote desktop client'},
    @{Name='7-Zip';Id='7zip.7zip';Category='Files';Detail='Open and create compressed archives'},
    @{Name='Notepad++';Id='Notepad++.Notepad++';Category='Editor';Detail='Lightweight text and code editor'},
    @{Name='Visual Studio Code';Id='Microsoft.VisualStudioCode';Category='Develop';Detail='Code editor with extensions'},
    @{Name='Git';Id='Git.Git';Category='Develop';Detail='Version control and Git Bash'},
    @{Name='VLC';Id='VideoLAN.VLC';Category='Media';Detail='Video and audio player'},
    @{Name='Everything';Id='voidtools.Everything';Category='Files';Detail='Fast local file search'},
    @{Name='PowerToys';Id='Microsoft.PowerToys';Category='Windows';Detail='Microsoft desktop utilities'},
    @{Name='Python 3.14';Id='Python.Python.3.14';Category='Develop';Detail='Python interpreter and standard library'},
    @{Name='Windows Terminal';Id='Microsoft.WindowsTerminal';Category='Windows';Detail='Modern tabs and terminal profiles'},
    @{Name='ShareX';Id='ShareX.ShareX';Category='Capture';Detail='Screenshots and screen recording'},
    @{Name='OBS Studio';Id='OBSProject.OBSStudio';Category='Capture';Detail='Video recording and streaming'}
)
$script:SeemlessState = @{
    View='Desktop'; DesktopIndex=0; StartIndex=0; FileIndex=0; AppIndex=0; ControlIndex=0
    Path=[Environment]::GetFolderPath('UserProfile'); Files=@(); AppQuery=''; Status=''
    Modal=$null; Prompt=$null; Running=$true; Width=120; Height=40; Ansi=$false
}
function ConvertTo-SeemlessSafeText {
    param([AllowNull()][string]$Text)
    if (-not $Text) { return '' }
    return [regex]::Replace($Text, '[\x00-\x08\x0B-\x1F\x7F]', '')
}
function Get-SeemlessLines {
    param([string]$Text,[int]$Width)
    $result = New-Object 'System.Collections.Generic.List[string]'
    $Width=[Math]::Max(10,$Width)
    foreach($paragraph in ($Text -split '\r?\n')) {
        $line=''
        foreach($word in ($paragraph -split '\s+')) {
            if(-not $word){continue}
            if($line.Length -gt 0 -and ($line.Length+1+$word.Length) -gt $Width) { $result.Add($line);$line='' }
            while($word.Length -gt $Width) { if($line){$result.Add($line);$line=''};$result.Add($word.Substring(0,$Width));$word=$word.Substring($Width) }
            if($word){if($line){$line+=' '};$line+=$word}
        }
        if($line){$result.Add($line)}else{$result.Add('')}
    }
    return $result.ToArray()
}
function Set-SeemlessStatus { param([string]$Text) $script:SeemlessState.Status=ConvertTo-SeemlessSafeText $Text }
function Get-SeemlessVisibleApps {
    $q=$script:SeemlessState.AppQuery
    return @($script:SeemlessApps | Where-Object { (-not $q) -or $_.Name.IndexOf($q,[StringComparison]::OrdinalIgnoreCase) -ge 0 -or $_.Category.IndexOf($q,[StringComparison]::OrdinalIgnoreCase) -ge 0 })
}
function Update-SeemlessFiles {
    param([string]$Path=$script:SeemlessState.Path)
    try {
        $resolved=Resolve-Path -LiteralPath $Path -ErrorAction Stop
        if($resolved.Provider.Name -ne 'FileSystem' -or -not (Test-Path -LiteralPath $resolved.ProviderPath -PathType Container)) { throw 'Choose a filesystem folder.' }
        $items=@(Get-ChildItem -LiteralPath $resolved.ProviderPath -Force -ErrorAction Stop | Sort-Object @{Expression={-not $_.PSIsContainer}},Name)
        $script:SeemlessState.Path=$resolved.ProviderPath
        $script:SeemlessState.Files=$items
        $script:SeemlessState.FileIndex=0
        Set-SeemlessStatus ('{0} items. Enter opens; Backspace goes up.' -f $items.Count)
    }catch{Set-SeemlessStatus ('Explorer: '+$_.Exception.Message)}
}
function Open-SeemlessView {
    param([string]$View)
    if($View -eq 'Web'){Start-SeemlessBrowser;return}
    $script:SeemlessState.View=$View
    if($View -eq 'Explorer'){Update-SeemlessFiles;return}
    Set-SeemlessStatus ''
}
function Start-SeemlessBrowser {
    $version='1.0.3537.50'
    $cache=Join-Path $env:LOCALAPPDATA "N0VA\WebView2\$version"
    $arch=if([IntPtr]::Size -eq 4){'x86'}elseif($env:PROCESSOR_ARCHITECTURE -eq 'ARM64'){'arm64'}else{'x64'}
    $managed=Join-Path $cache 'lib\net462'
    $core=Join-Path $managed 'Microsoft.Web.WebView2.Core.dll'
    $forms=Join-Path $managed 'Microsoft.Web.WebView2.WinForms.dll'
    $native=Join-Path $cache "runtimes\win-$arch\native"
    if(!(Test-Path $core) -or !(Test-Path $forms) -or !(Test-Path (Join-Path $native 'WebView2Loader.dll'))){
        Set-SeemlessStatus 'Downloading browser components...'
        Show-SeemlessScreen
        $stage=Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString())
        try{
            [void][IO.Directory]::CreateDirectory($stage)
            $zip=Join-Path $stage 'sdk.zip'
            [Net.ServicePointManager]::SecurityProtocol=[Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -UseBasicParsing -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$version/microsoft.web.webview2.$version.nupkg" -OutFile $zip -TimeoutSec 90
            if((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne '5ea526bbd728adda0da4d31219267e96460494a427e4894c4e09d9f320f4b9aa'){throw 'The browser component download was incomplete or changed.'}
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            $archive=[IO.Compression.ZipFile]::OpenRead($zip)
            try{
                $files=@('lib/net462/Microsoft.Web.WebView2.Core.dll','lib/net462/Microsoft.Web.WebView2.WinForms.dll',"runtimes/win-$arch/native/WebView2Loader.dll")
                foreach($file in $files){
                    $target=Join-Path $cache $file
                    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
                    $tmp=$target+'.'+[guid]::NewGuid().ToString()+'.tmp'
                    try{
                        [IO.Compression.ZipFileExtensions]::ExtractToFile($archive.GetEntry($file),$tmp)
                        try{[IO.File]::Move($tmp,$target)}catch [IO.IOException]{if(!(Test-Path -LiteralPath $target)){throw}}
                    }finally{if([IO.File]::Exists($tmp)){[IO.File]::Delete($tmp)}}
                }
            }finally{$archive.Dispose()}
        }finally{if([IO.Directory]::Exists($stage)){[IO.Directory]::Delete($stage,$true)}}
    }
    if(!('SeemlessBrowser' -as [type])){
        Add-Type -AssemblyName System.Windows.Forms,System.Drawing
        [void][Reflection.Assembly]::LoadFrom($core)
        [void][Reflection.Assembly]::LoadFrom($forms)
        [Microsoft.Web.WebView2.Core.CoreWebView2Environment]::SetLoaderDllFolderPath($native)
        Add-Type -ReferencedAssemblies @('System.dll','System.Drawing.dll','System.Windows.Forms.dll',$core,$forms) -TypeDefinition @'
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

public sealed class SeemlessBrowser : Form {
    readonly TabControl tabs = new TabControl();
    readonly ToolStripTextBox address = new ToolStripTextBox();
    readonly ToolStrip bar = new ToolStrip();
    readonly ToolStripButton back, forward;
    readonly string profile, proxy;
    WebView2 ActiveView {
        get { return tabs.SelectedTab == null ? null : tabs.SelectedTab.Tag as WebView2; }
    }
    public SeemlessBrowser(string profile, string proxy) {
        this.profile = profile; this.proxy = proxy;
        Text = "Seemless Browser"; Width = 1100; Height = 750;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(580, 400);
        tabs.Dock = DockStyle.Fill; Controls.Add(tabs);
        bar.GripStyle = ToolStripGripStyle.Hidden;
        bar.BackColor = Color.FromArgb(25,36,48); bar.ForeColor = Color.WhiteSmoke;
        bar.Padding = new Padding(6,5,6,5);
        back = Button("Back", delegate { if(ActiveView != null && ActiveView.CanGoBack) ActiveView.GoBack(); });
        forward = Button("Forward", delegate { if(ActiveView != null && ActiveView.CanGoForward) ActiveView.GoForward(); });
        Button("Reload", delegate { if(ActiveView != null && ActiveView.CoreWebView2 != null) ActiveView.Reload(); });
        address.AutoSize = false;
        address.ToolTipText = "Search or enter an address"; address.AccessibleName = address.ToolTipText;
        address.KeyDown += (s,e) => {
            if(e.KeyCode != Keys.Enter || ActiveView == null) return;
            e.SuppressKeyPress = true;
            var url = Destination(address.Text); if(url != null) ActiveView.Source = url;
        };
        bar.Items.Add(address);
        Button("+ Tab", delegate { OpenTab(null); });
        Button("Close tab", delegate { CloseTab(); });
        Controls.Add(bar);
        SizeChanged += delegate { address.Width = Math.Max(120, ClientSize.Width-370); };
        tabs.SelectedIndexChanged += delegate { Sync(); };
        Shown += delegate { address.Width=Math.Max(120, ClientSize.Width-370); OpenTab(null); };
    }
    ToolStripButton Button(string label, EventHandler action) {
        var button = new ToolStripButton(label); button.Click += action; bar.Items.Add(button); return button;
    }
    static Uri Destination(string input) {
        string text = (input ?? "").Trim(); if(text.Length==0) return null;
        Uri u;
        if(Uri.TryCreate(text, UriKind.Absolute, out u) && (u.Scheme=="http" || u.Scheme=="https")) return u;
        if(text.IndexOfAny(new[]{' ','\t'}) < 0 && Uri.TryCreate("https://"+text,UriKind.Absolute,out u)
            && (u.Host.Contains(".") || u.IsLoopback)) return u;
        return new Uri("https://duckduckgo.com/?q="+Uri.EscapeDataString(text));
    }
    void Sync() {
        var view = ActiveView;
        var uri = view == null ? null : view.Source;
        address.Text = uri == null || uri.AbsoluteUri=="about:blank" ? "" : uri.AbsoluteUri;
        back.Enabled = view != null && view.CoreWebView2 != null && view.CanGoBack;
        forward.Enabled = view != null && view.CoreWebView2 != null && view.CanGoForward;
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys key) {
        if(key==(Keys.Control|Keys.L)){ address.Focus(); address.SelectAll(); return true; }
        if(key==(Keys.Control|Keys.T)){ OpenTab(null); return true; }
        if(key==(Keys.Control|Keys.W)){ CloseTab(); return true; }
        return base.ProcessCmdKey(ref msg,key);
    }
    void CloseTab() {
        if(tabs.TabCount<=1){Close();return;}
        var page=tabs.SelectedTab; tabs.TabPages.Remove(page); page.Dispose(); Sync();
    }
    void OpenTab(Uri first) {
        var page=new TabPage("New tab");
        var view=new WebView2(); view.Dock=DockStyle.Fill;
        var properties=new CoreWebView2CreationProperties(); properties.UserDataFolder=profile;
        if(!String.IsNullOrEmpty(proxy)) properties.AdditionalBrowserArguments="--proxy-server="+proxy;
        view.CreationProperties=properties;
        page.Tag=view; page.Controls.Add(view); tabs.TabPages.Add(page); tabs.SelectedTab=page;
        view.CoreWebView2InitializationCompleted += (s,e) => {
            if(view.IsDisposed) return;
            if(!e.IsSuccess) {
                MessageBox.Show(this,e.InitializationException.Message,"WebView2",MessageBoxButtons.OK,MessageBoxIcon.Error);
                return;
            }
            var core=view.CoreWebView2;
            core.DocumentTitleChanged += delegate {
                var title=core.DocumentTitle;
                if(String.IsNullOrWhiteSpace(title) || title=="about:blank") title="New tab";
                page.Text=title.Substring(0,Math.Min(36,title.Length)).Replace("&","&&");
            };
            core.SourceChanged += delegate { if(ActiveView==view) Sync(); };
            core.HistoryChanged += delegate { if(ActiveView==view) Sync(); };
            core.NewWindowRequested += (sender,args) => {
                args.Handled=true;
                Uri url;
                if(Uri.TryCreate(args.Uri,UriKind.Absolute,out url) && (url.Scheme=="https" || url.Scheme=="http"))
                    BeginInvoke((Action)(() => OpenTab(url)));
            };
        };
        view.Source=first ?? new Uri("about:blank"); address.Focus();
    }
}
'@
    }
    try{[void][Microsoft.Web.WebView2.Core.CoreWebView2Environment]::GetAvailableBrowserVersionString($null)}
    catch{throw 'Install Microsoft Edge WebView2 Runtime, then open Browser again.'}
    $proxy=Get-SeemlessProxy
    $window=New-Object SeemlessBrowser((Join-Path $env:LOCALAPPDATA 'N0VA\BrowserProfile'),$proxy)
    try{Set-SeemlessStatus '';[void]$window.ShowDialog()}finally{$window.Dispose()}
}
function ConvertTo-SeemlessProxy {
    param([string]$Value)
    $Value=$Value.Trim()
    if(!$Value){return ''}
    $uri=$null
    if($Value -notmatch '^(?:https?|socks5)://(?:\[[a-f0-9:]+\]|[a-z0-9.-]+):[0-9]{1,5}/?$' -or
       ![uri]::TryCreate($Value,[UriKind]::Absolute,[ref]$uri) -or $uri.Port -lt 1){throw 'Use http://host:port, https://host:port, or socks5://host:port.'}
    return ('{0}://{1}:{2}' -f $uri.Scheme,$uri.Host,$uri.Port)
}
function Get-SeemlessProxy {
    $file=Join-Path $InstallDir 'browser-proxy.txt'
    if(Test-Path -LiteralPath $file){return ConvertTo-SeemlessProxy ([IO.File]::ReadAllText($file))}
    return ''
}
function Start-SeemlessExternal {
    param([string]$Target)
    try { Start-Process -FilePath $Target -ErrorAction Stop | Out-Null;Set-SeemlessStatus ('Opened '+$Target) }
    catch { Set-SeemlessStatus ('Could not open: '+$_.Exception.Message) }
}
# An installer runs without stealing the desktop or a PowerShell runspace.
if(-not ('SeemlessInstallJob' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
public sealed class SeemlessInstallJob : IDisposable {
    private readonly Process process;
    private readonly Queue<string> lines=new Queue<string>();
    private readonly object gate=new object();
    public SeemlessInstallJob(string executable,string package) {
        process=new Process();
        process.StartInfo=new ProcessStartInfo(executable,"install --id "+package+" --exact --source winget --silent --accept-package-agreements --accept-source-agreements --disable-interactivity");
        process.StartInfo.UseShellExecute=false;process.StartInfo.CreateNoWindow=true;
        process.StartInfo.RedirectStandardOutput=true;process.StartInfo.RedirectStandardError=true;
        process.OutputDataReceived+=Receive;process.ErrorDataReceived+=Receive;
        if(!process.Start())throw new InvalidOperationException("The installer could not start.");
        process.BeginOutputReadLine();process.BeginErrorReadLine();
    }
    private void Receive(object sender,DataReceivedEventArgs e) {
        if(String.IsNullOrWhiteSpace(e.Data))return;
        lock(gate){lines.Enqueue(e.Data);while(lines.Count>160)lines.Dequeue();}
    }
    public bool Running {get{return !process.HasExited;}}
    public int ExitCode {get{process.WaitForExit();return process.ExitCode;}}
    public string[] Snapshot(){lock(gate){return lines.ToArray();}}
    public void Dispose(){process.Dispose();}
}
'@
}
$script:SeemlessState.Document=$null
$script:SeemlessState.Preview=$null
$script:SeemlessState.InstallJob=$null
$script:SeemlessState.InstallApp=$null
$script:SeemlessState.InstallResult=''
$script:SeemlessState.InstallLog=@()
$script:SeemlessState.SettingsCache=@{}
$script:SeemlessState.SettingsScroll=0
$script:SeemlessState.Theme='Ocean'
$script:SeemlessControls=@(@{Name='This PC'},@{Name='Storage'},@{Name='Network'},@{Name='Running apps'},@{Name='Appearance'},@{Name='Browser proxy'})
function Set-SeemlessTheme {
    param([string]$Name,[switch]$Save)
    switch($Name){
        'Emerald' {$script:SC.Accent=0x6CE8AD;$script:SC.Blue=0x157951;$script:SC.Desktop=0x081F1D;$script:SC.Select=0x205747}
        'Violet' {$script:SC.Accent=0xC7A8FF;$script:SC.Blue=0x7051B8;$script:SC.Desktop=0x18182E;$script:SC.Select=0x493878}
        default {$Name='Ocean';$script:SC.Accent=0x55C8FF;$script:SC.Blue=0x1675DF;$script:SC.Desktop=0x061A36;$script:SC.Select=0x174E7A}
    }
    $script:SeemlessState.Theme=$Name
    if($Save){
        try{[void][IO.Directory]::CreateDirectory($InstallDir);@{Theme=$Name}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $InstallDir 'seemless-settings.json') -Encoding UTF8;Set-SeemlessStatus ('Appearance saved: '+$Name)}
        catch{Set-SeemlessStatus ('Could not save appearance: '+$_.Exception.Message)}
    }
}
try{$prefs=Get-Content -LiteralPath (Join-Path $InstallDir 'seemless-settings.json') -Raw -ErrorAction Stop|ConvertFrom-Json;Set-SeemlessTheme $prefs.Theme}catch{}
function Get-SeemlessSettingLines {
    param([string]$Category)
    $cache=$script:SeemlessState.SettingsCache
    if($cache.ContainsKey($Category) -and ((Get-Date)-$cache[$Category].At).TotalSeconds -lt 30){return $cache[$Category].Lines}
    $lines=New-Object 'System.Collections.Generic.List[string]'
    try{
        switch($Category){
            'This PC' {
                $lines.Add('Device: '+[Environment]::MachineName);$lines.Add('Signed in: '+[Environment]::UserName)
                $lines.Add('System: '+[Environment]::OSVersion.VersionString);$lines.Add('Processors: '+[Environment]::ProcessorCount)
                $lines.Add('64-bit system: '+$(if([Environment]::Is64BitOperatingSystem){'Yes'}else{'No'}));$lines.Add('Home: '+[Environment]::GetFolderPath('UserProfile'))
                $lines.Add('N0VA files: '+$InstallDir)
            }
            'Storage' {
                foreach($drive in [IO.DriveInfo]::GetDrives()){
                    try{if($drive.IsReady){$lines.Add(('{0}  {1:N1} GB free / {2:N1} GB  [{3}]' -f $drive.Name,($drive.AvailableFreeSpace/1GB),($drive.TotalSize/1GB),$drive.DriveFormat))}}catch{}
                }
            }
            'Network' {
                foreach($adapter in [Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()){
                    if($adapter.OperationalStatus -ne 'Up'){continue}
                    $lines.Add($adapter.Name+'  -  Connected')
                    foreach($address in $adapter.GetIPProperties().UnicastAddresses){if($address.Address.AddressFamily -eq 'InterNetwork'){$lines.Add('  IPv4: '+$address.Address)}}
                }
            }
            'Running apps' {
                $lines.Add('APP                           MEMORY')
                foreach($process in (Get-Process|Sort-Object WorkingSet64 -Descending|Select-Object -First 30)){$lines.Add(('{0,-28} {1,7:N0} MB' -f $process.ProcessName,($process.WorkingSet64/1MB)))}
            }
            'Browser proxy' {$proxy=Get-SeemlessProxy;if(!$proxy){$proxy='System default'};$lines.Add($proxy);$lines.Add('Enter to change')}
            'Appearance' {$lines.Add('Current theme: '+$script:SeemlessState.Theme);$lines.Add('');$lines.Add('Enter changes theme: Ocean / Emerald / Violet')}
        }
        if(-not $lines.Count){$lines.Add('No information available on this PC.')}
    }catch{$lines.Add('Some details are unavailable: '+$_.Exception.Message)}
    $cache[$Category]=@{At=(Get-Date);Lines=$lines.ToArray()};return $lines.ToArray()
}
function Open-SeemlessFile {
    param([string]$Path)
    $item=Get-Item -LiteralPath $Path -ErrorAction Stop
    $info=@('Name: '+$item.Name,'Size: '+('{0:N0} bytes' -f $item.Length),'Changed: '+$item.LastWriteTime.ToString('g'),'Location: '+$item.DirectoryName)
    $preview=@{Path=$item.FullName;Name=$item.Name;Editable=$false;Text='';Lines=$info;Scroll=0}
    if($item.Length -gt 512KB){$preview.Lines=$info+@('','This file is too large for the built-in reader.','Press O to open it in its Windows app.')}
    else{
        $bytes=[IO.File]::ReadAllBytes($item.FullName)
        $isText=($bytes -notcontains [byte]0) -or ($bytes.Length -ge 2 -and (($bytes[0] -eq 255 -and $bytes[1] -eq 254) -or ($bytes[0] -eq 254 -and $bytes[1] -eq 255)))
        if($isText -and -not ($bytes.Length -ge 2 -and (($bytes[0] -eq 255 -and $bytes[1] -eq 254) -or ($bytes[0] -eq 254 -and $bytes[1] -eq 255)))){
            try{$decoded=(New-Object Text.UTF8Encoding($false,$true)).GetString($bytes);if($decoded -match '[\x00-\x08\x0B\x0C\x0E-\x1F]'){$isText=$false}}catch{$isText=$false}
        }
        if($isText){
            $reader=New-Object IO.StreamReader($item.FullName,$true)
            try{$text=$reader.ReadToEnd();$encoding=$reader.CurrentEncoding}finally{$reader.Dispose()}
            $preview.Text=$text;$preview.Encoding=$encoding;$preview.Editable=($item.Length -le 256KB -and -not $item.IsReadOnly)
            $preview.Lines=@(Get-SeemlessLines $text ([Math]::Max(10,$script:SeemlessState.Width-10)))
            if(-not $preview.Editable){$preview.Lines=@('Read-only preview (file is read-only or larger than 256 KB).','')+$preview.Lines}
            $preview.LastWrite=$item.LastWriteTimeUtc.Ticks
        }else{
            $preview.Lines=$info+@('','This file uses a format the desktop cannot edit.','Press O to open it in its Windows app.')
            if($item.Extension -match '^\.(png|jpe?g|gif|bmp|ico)$'){
                try{Add-Type -AssemblyName System.Drawing;$image=[Drawing.Image]::FromFile($item.FullName);try{$preview.Lines=$info+@('','Image: '+$image.Width+' x '+$image.Height+' pixels','Press O to view in your Windows photo app.')}finally{$image.Dispose()}}catch{}
            }
        }
    }
    $script:SeemlessState.Preview=$preview;$script:SeemlessState.View='Preview';Set-SeemlessStatus 'Preview stays on your desktop. E: edit text  O: open outside  Esc: files'
}
function Start-SeemlessEditor {
    param($Preview)
    if(-not $Preview.Editable){Set-SeemlessStatus 'This file is available as a preview only.';return}
    $lines=New-Object 'System.Collections.Generic.List[string]'
    foreach($line in ($Preview.Text -split '\r?\n',0)){$lines.Add($line)}
    if(-not $lines.Count){$lines.Add('')}
    $script:SeemlessState.Document=@{Path=$Preview.Path;Name=$Preview.Name;Lines=$lines;CursorX=0;CursorY=0;Scroll=0;Dirty=$false;New=$false;Encoding=$Preview.Encoding;Newline="`n";LastWrite=$Preview.LastWrite}
    if($Preview.Text.Contains("`r`n")){$script:SeemlessState.Document.Newline="`r`n"}
    $script:SeemlessState.View='Editor';Set-SeemlessStatus 'Ctrl+S  Save'
}
function Save-SeemlessDocument {
    $doc=$script:SeemlessState.Document;$temporary=$null;$createdNew=$false
    try{
        $text=[string]::Join($doc.Newline,$doc.Lines.ToArray())
        if($doc.New){
            # CreateNew refuses an existing file, including one created since the prompt.
            $stream=New-Object IO.FileStream($doc.Path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
            $createdNew=$true
            try{$writer=New-Object IO.StreamWriter($stream,$doc.Encoding);try{$writer.Write($text);$writer.Flush()}finally{$writer.Dispose()}}finally{$stream.Dispose()}
            $createdNew=$false
        }else{
            $existing=Get-Item -LiteralPath $doc.Path -ErrorAction Stop
            if($existing.LastWriteTimeUtc.Ticks -ne $doc.LastWrite){throw 'This file changed outside the desktop. Save was stopped to protect those changes.'}
            $temporary=Join-Path $existing.DirectoryName ('.seemless-'+[Guid]::NewGuid().ToString('N')+'.tmp')
            [IO.File]::WriteAllText($temporary,$text,$doc.Encoding)
            if((Get-Item -LiteralPath $doc.Path).LastWriteTimeUtc.Ticks -ne $doc.LastWrite){throw 'This file changed while saving. Your original file has been left untouched.'}
            # Replace only after the complete text has been written beside the original.
            [IO.File]::Replace($temporary,$doc.Path,[NullString]::Value)
            $temporary=$null
        }
        $doc.LastWrite=(Get-Item -LiteralPath $doc.Path).LastWriteTimeUtc.Ticks;$doc.Dirty=$false;$doc.New=$false
        Set-SeemlessStatus ('Saved '+$doc.Name);return $true
    }catch{
        if($createdNew){try{[IO.File]::Delete($doc.Path)}catch{}}
        Set-SeemlessStatus ('Could not save: '+$_.Exception.Message);return $false
    }finally{if($temporary){try{[IO.File]::Delete($temporary)}catch{}}}
}
function New-SeemlessItem {
    param([string]$Name,[switch]$Folder)
    $Name=$Name.Trim()
    if(-not $Name -or $Name -in @('.','..') -or $Name.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or $Name -match '[/\\]' -or $Name -match '^(?i:con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\..*)?$' -or $Name.EndsWith('.')){throw 'Choose a normal filename without a path or reserved device name.'}
    $target=Join-Path $script:SeemlessState.Path $Name
    if(Test-Path -LiteralPath $target){throw 'That name already exists. Choose a different name.'}
    if($Folder){[void][IO.Directory]::CreateDirectory($target);Update-SeemlessFiles;Set-SeemlessStatus ('Created folder '+$Name)}
    else{
        $lines=New-Object 'System.Collections.Generic.List[string]';$lines.Add('')
        $script:SeemlessState.Document=@{Path=$target;Name=$Name;Lines=$lines;CursorX=0;CursorY=0;Scroll=0;Dirty=$true;New=$true;Encoding=(New-Object Text.UTF8Encoding($false));Newline="`r`n";LastWrite=0}
        $script:SeemlessState.View='Editor';Set-SeemlessStatus 'New text file. Type here, then Ctrl+S to save.'
    }
}
function Request-SeemlessLeave {
    param([string]$Destination)
    $st=$script:SeemlessState
    if($Destination -eq 'Exit' -and $st.InstallJob -and $st.InstallJob.Running){Set-SeemlessStatus 'An installation is still running. You can use the desktop while it finishes.';return $false}
    if($st.View -eq 'Editor' -and $st.Document.Dirty){$st.Modal=@{Kind='Unsaved';Name=$st.Document.Name;PendingView=$Destination};return $false}
    if($Destination -eq 'Exit'){$st.Running=$false}else{Open-SeemlessView $Destination};return $true
}
function Invoke-SeemlessEditorKey {
    param([ConsoleKeyInfo]$Key)
    $doc=$script:SeemlessState.Document;$line=$doc.Lines[$doc.CursorY]
    if($Key.Key -eq 'S' -and ($Key.Modifiers -band [ConsoleModifiers]::Control)){[void](Save-SeemlessDocument);return}
    switch($Key.Key){
        'UpArrow' {$doc.CursorY=[Math]::Max(0,$doc.CursorY-1)}
        'DownArrow' {$doc.CursorY=[Math]::Min($doc.Lines.Count-1,$doc.CursorY+1)}
        'LeftArrow' {if($doc.CursorX -gt 0){$doc.CursorX--}elseif($doc.CursorY -gt 0){$doc.CursorY--;$doc.CursorX=$doc.Lines[$doc.CursorY].Length}}
        'RightArrow' {if($doc.CursorX -lt $line.Length){$doc.CursorX++}elseif($doc.CursorY -lt $doc.Lines.Count-1){$doc.CursorY++;$doc.CursorX=0}}
        'Home' {$doc.CursorX=0}
        'End' {$doc.CursorX=$line.Length}
        'PageUp' {$doc.CursorY=[Math]::Max(0,$doc.CursorY-10)}
        'PageDown' {$doc.CursorY=[Math]::Min($doc.Lines.Count-1,$doc.CursorY+10)}
        'Enter' {$doc.Lines[$doc.CursorY]=$line.Substring(0,$doc.CursorX);$doc.Lines.Insert($doc.CursorY+1,$line.Substring($doc.CursorX));$doc.CursorY++;$doc.CursorX=0;$doc.Dirty=$true}
        'Backspace' {if($doc.CursorX -gt 0){$doc.Lines[$doc.CursorY]=$line.Remove($doc.CursorX-1,1);$doc.CursorX--;$doc.Dirty=$true}elseif($doc.CursorY -gt 0){$previous=$doc.Lines[$doc.CursorY-1];$doc.Lines[$doc.CursorY-1]=$previous+$line;$doc.Lines.RemoveAt($doc.CursorY);$doc.CursorY--;$doc.CursorX=$previous.Length;$doc.Dirty=$true}}
        'Delete' {if($doc.CursorX -lt $line.Length){$doc.Lines[$doc.CursorY]=$line.Remove($doc.CursorX,1);$doc.Dirty=$true}elseif($doc.CursorY -lt $doc.Lines.Count-1){$doc.Lines[$doc.CursorY]+=$doc.Lines[$doc.CursorY+1];$doc.Lines.RemoveAt($doc.CursorY+1);$doc.Dirty=$true}}
        default {if(-not [char]::IsControl($Key.KeyChar)){$doc.Lines[$doc.CursorY]=$line.Insert($doc.CursorX,[string]$Key.KeyChar);$doc.CursorX++;$doc.Dirty=$true}}
    }
    $doc.CursorX=[Math]::Min($doc.CursorX,$doc.Lines[$doc.CursorY].Length)
}
function Start-SeemlessInstall {
    param($App)
    $st=$script:SeemlessState
    if($st.InstallJob -and $st.InstallJob.Running){$st.View='Install';Set-SeemlessStatus 'One app is already installing. Please wait for it to finish.';return}
    $winget=Get-Command winget.exe -ErrorAction SilentlyContinue
    $st.InstallApp=$App;$st.InstallLog=@();$st.InstallResult='';$st.View='Install'
    try{if(-not $winget){throw 'App Installer is missing. Install Microsoft App Installer to enable downloads.'};$st.InstallJob=New-Object SeemlessInstallJob($winget.Source,$App.Id);Set-SeemlessStatus ('Installing '+$App.Name+' in the background. You can keep using your desktop.')}
    catch{$st.InstallJob=$null;$st.InstallResult=$_.Exception.Message;Set-SeemlessStatus $st.InstallResult}
}
function Update-SeemlessInstall {
    $st=$script:SeemlessState
    if(-not $st.InstallJob){return $false}
    $st.InstallLog=@($st.InstallJob.Snapshot()|ForEach-Object{ConvertTo-SeemlessSafeText $_})
    if(-not $st.InstallJob.Running){
        $code=$st.InstallJob.ExitCode
        $st.InstallLog=@($st.InstallJob.Snapshot()|ForEach-Object{ConvertTo-SeemlessSafeText $_})
        $st.InstallJob.Dispose();$st.InstallJob=$null
        if($code -eq 0){$st.InstallResult=$st.InstallApp.Name+' is ready.'}else{$st.InstallResult='Installation did not finish (code '+$code+'). Details are shown below.'}
        Set-SeemlessStatus $st.InstallResult
    }
    return $true
}

function Get-SeemlessGeometry {
    $w=[Math]::Max(20,[Console]::WindowWidth-1);$h=[Math]::Max(8,[Console]::WindowHeight)
    return @{W=$w;H=$h;X=2;Y=3;WW=$w-4;WH=$h-7;BodyX=4;BodyY=7;BodyW=$w-8;BodyH=[Math]::Max(1,$h-13)}
}
function Draw-SeemlessBox {
    param($Canvas,[int]$X,[int]$Y,[int]$Width,[int]$Height,[string]$Title)
    $c=$script:SC
    $Canvas.Fill($X+1,$Y+1,$Width,$Height,$c.Ink,0x020A18)
    $Canvas.Fill($X,$Y,$Width,$Height,$c.Ink,$c.Panel)
    $Canvas.Fill($X,$Y,$Width,1,$c.Ink,$c.Blue)
    $Canvas.Text($X+2,$Y,$Title,$c.Ink,$c.Blue)
    $Canvas.Text($X+$Width-13,$Y,' _   []   X ',$c.Ink,$c.Blue)
    $Canvas.Fill($X,$Y+$Height-1,$Width,1,$c.Muted,$c.Side)
}
function Draw-SeemlessList {
    param($Canvas,$Items,[int]$Index,[int]$X,[int]$Y,[int]$Width,[int]$Height,[string]$Kind)
    $c=$script:SC
    if($Items.Count -eq 0){$Canvas.Text($X,$Y,'No items to show.',$c.Muted,$c.Panel);return}
    $top=[Math]::Max(0,[Math]::Min($Index-[int][Math]::Floor($Height/2),[Math]::Max(0,$Items.Count-$Height)))
    for($row=0;$row -lt $Height -and ($top+$row) -lt $Items.Count;$row++) {
        $i=$top+$row;$item=$Items[$i];$bg=$c.Panel;$fg=$c.Ink
        if($i -eq $Index){$bg=$c.Select;$fg=$c.Accent}
        $Canvas.Fill($X,$Y+$row,$Width,1,$fg,$bg)
        switch($Kind){
            'Files' {$label='[FILE] '+$item.Name;if($item.PSIsContainer){$label='[DIR]  '+$item.Name};if(-not $item.PSIsContainer -and $Width -gt 65){$size='{0,10:N0} B' -f $item.Length;$Canvas.Text($X+$Width-15,$Y+$row,$size,$c.Muted,$bg);$label=$label.Substring(0,[Math]::Min($label.Length,$Width-17))}}
            'Apps' {$label='{0,-22} {1}' -f $item.Name,$item.Category}
            default {$label=[string]$item.Name}
        }
        if($label.Length -gt $Width-2){$label=$label.Substring(0,[Math]::Max(0,$Width-5))+'...'}
        $Canvas.Text($X+1,$Y+$row,$label,$fg,$bg)
    }
}
function Show-SeemlessScreen {
    $st=$script:SeemlessState;$c=$script:SC;$g=Get-SeemlessGeometry
    $st.Width=$g.W;$st.Height=$g.H
    $screen=New-Object SeemlessCanvas($g.W,$g.H,$c.Ink,$c.Desktop)
    # A quiet blue desktop with the familiar four-pane window motif.
    for($y=1;$y -lt $g.H-2;$y++){
        $offset=[int]($g.W*.55)+[int](6*[Math]::Sin($y/5.0))
        $screen.Fill($offset,$y,[Math]::Max(0,$g.W-$offset),1,$c.Ink,$c.Side)
    }
    $screen.Text(2,0,'N0VA  /  Seemless 0S MODE',$c.Accent,$c.Desktop)
    $screen.Text([Math]::Max(2,$g.W-7),0,'v2.1',$c.Muted,$c.Desktop)
    $screen.Fill(0,$g.H-3,$g.W,1,$c.Muted,$c.Side)
    $screen.Text(2,$g.H-3,(ConvertTo-SeemlessSafeText $st.Status),$c.Muted,$c.Side)
    $screen.Fill(0,$g.H-2,$g.W,2,$c.Ink,0x0A1326)
    $clock=(Get-Date).ToString('h:mm tt',[Globalization.CultureInfo]::InvariantCulture)
    $taskbar=if($g.W -ge 78){'[S] Start    F2 Files    F3 Browser    F4 Apps    F5 Settings'}else{'[S] Start'}
    $screen.Text(2,$g.H-2,$taskbar,$c.Ink,0x0A1326)
    $screen.Text($g.W-$clock.Length-2,$g.H-2,$clock,$c.Accent,0x0A1326)
    $screen.Text(2,$g.H-1,'Arrows / Enter    Esc  Back    F10  Exit',$c.Muted,0x0A1326)
    if($st.View -in @('Desktop','Start')) {
        $cards=@('Files','Browser','Apps','Settings')
        for($i=0;$i -lt 4;$i++){
            $y=4+$i*3;if($y -ge $g.H-5){break}
            $bg=$c.Desktop;$fg=$c.Ink;if($st.DesktopIndex -eq $i){$bg=$c.Select;$fg=$c.Accent}
            $screen.Fill(4,$y,28,1,$fg,$bg);$screen.Text(5,$y,('[ '+($i+1)+' ]  '+$cards[$i]),$fg,$bg)
        }
        if($g.W -ge 95){
            $wx=[int]($g.W*.65);$wy=[int]($g.H*.3)
            $screen.Fill($wx,$wy,10,4,$c.Ink,$c.Blue);$screen.Fill($wx+11,$wy,10,4,$c.Ink,$c.Accent)
            $screen.Fill($wx,$wy+5,10,4,$c.Ink,$c.Select);$screen.Fill($wx+11,$wy+5,10,4,$c.Ink,$c.Blue)

        }
        if($st.View -eq 'Start') {
            $sx=3;$sy=[Math]::Max(3,$g.H-19);$sw=[Math]::Min(43,$g.W-6)
            Draw-SeemlessBox $screen $sx $sy $sw 14 'Start'
            $screen.Text($sx+2,$sy+2,'PINNED',$c.Muted,$c.Panel)
            $startItems=@(@{Name='Files'},@{Name='Browser'},@{Name='Apps'},@{Name='Settings'},@{Name='Leave desktop'})
            Draw-SeemlessList $screen $startItems $st.StartIndex ($sx+2) ($sy+4) ($sw-4) 6 'Start'
        }
    }else{
        $titles=@{Explorer='Files';Apps='Apps';Control='Settings';Preview='Quick look';Editor='Notes';Install='App download'}
        Draw-SeemlessBox $screen $g.X $g.Y $g.WW $g.WH $titles[$st.View]
        switch($st.View){
            'Explorer' {
                $screen.Text($g.BodyX,5,('This PC > '+$st.Path),$c.Accent,$c.Panel)
                $screen.Text($g.BodyX,$g.H-6,'Enter: preview   T: new note   M: new folder   L: location',$c.Muted,$c.Side)
                Draw-SeemlessList $screen $st.Files $st.FileIndex $g.BodyX $g.BodyY $g.BodyW ($g.BodyH-1) 'Files'
                if($st.Files.Count){$selected=$st.Files[$st.FileIndex];$screen.Text($g.BodyX,$g.H-7,('Selected: '+$selected.Name+'  |  '+$selected.LastWriteTime.ToString('g')),$c.Muted,$c.Panel)}
            }
            'Apps' {
                $apps=@(Get-SeemlessVisibleApps)
                $screen.Text($g.BodyX,5,$(if($st.AppQuery){'Search: '+$st.AppQuery}else{'All apps'}),$c.Accent,$c.Panel)
                $listWidth=$g.BodyW
                if($g.W -gt 110){$listWidth=[int]($g.BodyW*.57)}
                Draw-SeemlessList $screen $apps $st.AppIndex $g.BodyX $g.BodyY $listWidth $g.BodyH 'Apps'
                if($g.W -gt 110 -and $apps.Count -gt 0){
                    $app=$apps[$st.AppIndex];$dx=$g.BodyX+$listWidth+3
                    $screen.Text($dx,8,$app.Name,$c.Accent,$c.Panel)
                    $screen.Text($dx,10,$app.Category,$c.Muted,$c.Panel)
                    $detail=@(Get-SeemlessLines $app.Detail ($g.BodyW-$listWidth-4))
                    for($i=0;$i -lt $detail.Count -and $i -lt 4;$i++){$screen.Text($dx,12+$i,$detail[$i],$c.Ink,$c.Panel)}
                    $screen.Text($dx,18,'Enter to install',$c.Green,$c.Panel)
                }
                $screen.Text($g.BodyX,$g.H-6,'/ Search   Enter Install selected app   R Reset search',$c.Muted,$c.Side)
            }
            'Control' {
                $category=$script:SeemlessControls[$st.ControlIndex].Name
                $detail=@(Get-SeemlessSettingLines $category)
                $left=[Math]::Min(23,[int]($g.BodyW*.3))
                Draw-SeemlessList $screen $script:SeemlessControls $st.ControlIndex $g.BodyX $g.BodyY $left ([Math]::Min($script:SeemlessControls.Count,$g.BodyH)) 'Control'
                $dx=$g.BodyX+$left+2;$dw=$g.BodyW-$left-2
                $wrapped=@(Get-SeemlessLines ([string]::Join("`n",$detail)) $dw)
                $st.SettingsScroll=[Math]::Min($st.SettingsScroll,[Math]::Max(0,$wrapped.Count-$g.BodyH))
                for($i=0;$i -lt $g.BodyH -and ($i+$st.SettingsScroll) -lt $wrapped.Count;$i++){$screen.Text($dx,$g.BodyY+$i,$wrapped[$i+$st.SettingsScroll],$c.Ink,$c.Panel)}
                $screen.Text($g.BodyX,$g.H-6,'Arrows: category   PageUp/Down: details   R: refresh',$c.Muted,$c.Side)
            }
            'Preview' {
                $p=$st.Preview
                $screen.Text($g.BodyX,5,$p.Name,$c.Accent,$c.Panel)
                $lines=$p.Lines
                $p.Scroll=[Math]::Min($p.Scroll,[Math]::Max(0,$lines.Count-$g.BodyH))
                for($i=0;$i -lt $g.BodyH -and ($i+$p.Scroll) -lt $lines.Count;$i++){$screen.Text($g.BodyX,$g.BodyY+$i,$lines[$i+$p.Scroll],$c.Ink,$c.Panel)}
                $screen.Text($g.BodyX,$g.H-6,'E: edit text    O: open in Windows app    Esc: files',$c.Muted,$c.Side)
            }
            'Editor' {
                $d=$st.Document;$dirty='';if($d.Dirty){$dirty='  * Unsaved'}
                $screen.Text($g.BodyX,5,($d.Name+$dirty),$c.Accent,$c.Panel)
                if($d.CursorY -lt $d.Scroll){$d.Scroll=$d.CursorY};if($d.CursorY -ge $d.Scroll+$g.BodyH){$d.Scroll=$d.CursorY-$g.BodyH+1}
                $horizontal=[Math]::Max(0,$d.CursorX-$g.BodyW+9)
                for($i=0;$i -lt $g.BodyH -and ($i+$d.Scroll) -lt $d.Lines.Count;$i++){
                    $index=$i+$d.Scroll;$line=$d.Lines[$index];if($horizontal -lt $line.Length){$line=$line.Substring($horizontal)}else{$line=''}
                    $screen.Text($g.BodyX,$g.BodyY+$i,('{0,3} ' -f ($index+1)),$c.Muted,$c.Panel)
                    $screen.Text($g.BodyX+4,$g.BodyY+$i,$line,$c.Ink,$c.Panel)
                }
                $cursor=$d.Lines[$d.CursorY];$char=' ';if($d.CursorX -lt $cursor.Length){$char=[string]$cursor[$d.CursorX]}
                $screen.Text($g.BodyX+4+$d.CursorX-$horizontal,$g.BodyY+$d.CursorY-$d.Scroll,$char,$c.Desktop,$c.Accent)
                $screen.Text($g.BodyX,$g.H-6,'Ctrl+S: save your note    Esc: back to files',$c.Muted,$c.Side)
            }
            'Install' {
                $name='App';if($st.InstallApp){$name=$st.InstallApp.Name}
                $screen.Text($g.BodyX,5,('Getting '+$name+' ready'),$c.Accent,$c.Panel)
                $message=$st.InstallResult
                if($st.InstallJob){$message='Installing... You can keep using your desktop.'}
                $screen.Text($g.BodyX,7,$message,$c.Green,$c.Panel)
                $logs=@($st.InstallLog|Select-Object -Last ([Math]::Max(1,$g.BodyH-5)))
                if($st.InstallResult -and $st.InstallResult -notmatch 'is ready'){
                    for($i=0;$i -lt $logs.Count;$i++){$screen.Text($g.BodyX,12+$i,$logs[$i],$c.Muted,$c.Panel)}
                }elseif($st.InstallJob){$tick=[int]((Get-Date).Second%4);$screen.Text($g.BodyX,12,(@('Working   .','Working  ..','Working ...','Working  ..')[$tick]),$c.Accent,$c.Panel)}
                $screen.Text($g.BodyX,$g.H-6,'Esc: apps    F2: files    F5: settings',$c.Muted,$c.Side)
            }

        }
    }
    if($st.Modal -or $st.Prompt){
        $mw=[Math]::Min(66,$g.W-6);$mx=[int](($g.W-$mw)/2);$my=[Math]::Max(2,[int](($g.H-8)/2))
        $screen.Fill(0,0,$g.W,1,$c.Muted,$c.Desktop)
        if($st.Modal){
            if($st.Modal.Kind -eq 'Unsaved'){
                Draw-SeemlessBox $screen $mx $my $mw 8 'Keep your changes?'
                $screen.Text($mx+2,$my+2,('You have unsaved changes to '+$st.Modal.Name),$c.Ink,$c.Panel)
                $screen.Text($mx+2,$my+4,'[S] Save   [D] Discard   [Esc] Keep editing',$c.Accent,$c.Panel)
            }else{
                Draw-SeemlessBox $screen $mx $my $mw 8 'Get this app'
                $screen.Text($mx+2,$my+2,('Install '+$st.Modal.Name+'?'),$c.Ink,$c.Panel)
                $screen.Text($mx+2,$my+3,'It will download and install on this PC.',$c.Muted,$c.Panel)
                $screen.Text($mx+2,$my+5,'[Y] Install     [Esc / N] Not now',$c.Green,$c.Panel)
            }
        }else{
            Draw-SeemlessBox $screen $mx $my $mw 8 $st.Prompt.Title
            $screen.Text($mx+2,$my+2,$st.Prompt.Hint,$c.Muted,$c.Panel)
            $value=$st.Prompt.Value
            if($value.Length -gt $mw-6){$value=$value.Substring($value.Length-($mw-6))}
            $screen.Fill($mx+2,$my+4,$mw-4,1,$c.Ink,$c.Select)
            $screen.Text($mx+3,$my+4,($value+'_'),$c.Ink,$c.Select)
            $screen.Text($mx+2,$my+6,'Enter: go    Ctrl+A: clear    Esc: cancel',$c.Accent,$c.Panel)
        }
    }
    if($g.W -lt 70 -or $g.H -lt 23){
        $screen.Fill(0,1,$g.W,1,$c.Gold,$c.Side)
        $screen.Text(1,1,'Use at least 80 x 25 for the full desktop.',$c.Gold,$c.Side)
    }
    [Console]::SetCursorPosition(0,[Console]::WindowTop)
    [Console]::Write($screen.Frame($st.Ansi))
}
function Start-SeemlessPrompt {
    param([string]$Kind,[string]$Title,[string]$Hint,[string]$Value='')
    $script:SeemlessState.Prompt=@{Kind=$Kind;Title=$Title;Hint=$Hint;Value=$Value}
}
function Invoke-SeemlessPrompt {
    $st=$script:SeemlessState;$prompt=$st.Prompt;$st.Prompt=$null
    switch($prompt.Kind){
        'Location' {Update-SeemlessFiles $prompt.Value}
        'Search' {$st.AppQuery=$prompt.Value.Trim();$st.AppIndex=0}
        'Proxy' {
            $value=ConvertTo-SeemlessProxy $prompt.Value
            [void][IO.Directory]::CreateDirectory($InstallDir)
            [IO.File]::WriteAllText((Join-Path $InstallDir 'browser-proxy.txt'),$value)
            $st.SettingsCache=@{};Set-SeemlessStatus 'Browser proxy saved.'
        }
        'NewText' {New-SeemlessItem $prompt.Value}
        'NewFolder' {New-SeemlessItem $prompt.Value -Folder}
    }
}
function Invoke-SeemlessKey {
    param([ConsoleKeyInfo]$Key)
    $st=$script:SeemlessState
    if($Key.Key -eq 'F10' -and -not $st.Modal){[void](Request-SeemlessLeave Exit);return}
    if($st.Prompt){
        if($Key.Key -eq 'Escape'){$st.Prompt=$null;Set-SeemlessStatus 'Canceled.';return}
        if($Key.Key -eq 'A' -and ($Key.Modifiers -band [ConsoleModifiers]::Control)){$st.Prompt.Value='';return}
        if($Key.Key -eq 'Enter'){Invoke-SeemlessPrompt;return}
        if($Key.Key -eq 'Backspace'){if($st.Prompt.Value.Length){$st.Prompt.Value=$st.Prompt.Value.Substring(0,$st.Prompt.Value.Length-1)};return}
        if(-not [char]::IsControl($Key.KeyChar) -and $st.Prompt.Value.Length -lt 2048){$st.Prompt.Value += [string]$Key.KeyChar}
        return
    }
    if($st.Modal){
        if($st.Modal.Kind -eq 'Unsaved'){
            $target=$st.Modal.PendingView
            if($Key.Key -eq 'Escape'){$st.Modal=$null}
            elseif($Key.Key -eq 'S'){if(Save-SeemlessDocument){$st.Modal=$null;[void](Request-SeemlessLeave $target)}}
            elseif($Key.Key -eq 'D'){$st.Document.Dirty=$false;$st.Modal=$null;[void](Request-SeemlessLeave $target)}
        }else{
            if($Key.Key -eq 'Y'){$app=$st.Modal;$st.Modal=$null;Start-SeemlessInstall $app}
            elseif($Key.Key -in @('Escape','N')){$st.Modal=$null;Set-SeemlessStatus 'Not installed.'}
        }
        return
    }
    $destination=$null
    switch($Key.Key){
        'F1' {$destination='Desktop'}
        'F2' {$destination='Explorer'}
        'F3' {$destination='Web'}
        'F4' {$destination='Apps'}
        'F5' {$destination='Control'}
        'Escape' {$destination='Desktop';if($st.View -in @('Preview','Editor')){$destination='Explorer'};if($st.View -eq 'Install'){$destination='Apps'}}
        'S' {if($st.View -ne 'Editor'){$destination='Start';if($st.View -eq 'Start'){$destination='Desktop'}}}
    }
    if($destination){[void](Request-SeemlessLeave $destination);return}
    if($st.View -eq 'Editor'){Invoke-SeemlessEditorKey $Key;return}
    $delta=0
    if($Key.Key -in @('DownArrow','RightArrow')){$delta=1}
    if($Key.Key -in @('UpArrow','LeftArrow')){$delta=-1}
    switch($st.View){
        'Desktop' {
            $st.DesktopIndex=[Math]::Max(0,[Math]::Min(3,$st.DesktopIndex+$delta))
            if($Key.Key -eq 'Enter'){Open-SeemlessView @('Explorer','Web','Apps','Control')[$st.DesktopIndex]}
            if($Key.KeyChar -ge '1' -and $Key.KeyChar -le '4'){Open-SeemlessView @('Explorer','Web','Apps','Control')[[int]::Parse([string]$Key.KeyChar)-1]}
        }
        'Start' {
            $st.StartIndex=[Math]::Max(0,[Math]::Min(4,$st.StartIndex+$delta))
            if($Key.Key -eq 'Enter'){if($st.StartIndex -eq 4){[void](Request-SeemlessLeave Exit)}else{Open-SeemlessView @('Explorer','Web','Apps','Control')[$st.StartIndex]}}
        }
        'Explorer' {
            $st.FileIndex=[Math]::Max(0,[Math]::Min([Math]::Max(0,$st.Files.Count-1),$st.FileIndex+$delta))
            switch($Key.Key){
                'L' {Start-SeemlessPrompt Location 'Go to folder' 'Type a full folder path.' $st.Path}
                'H' {Update-SeemlessFiles ([Environment]::GetFolderPath('UserProfile'))}
                'N' {if(-not (Test-Path -LiteralPath $InstallDir)){[void][IO.Directory]::CreateDirectory($InstallDir)};Update-SeemlessFiles $InstallDir}
                'R' {Update-SeemlessFiles}
                'Backspace' {$parent=[IO.Directory]::GetParent($st.Path);if($parent){Update-SeemlessFiles $parent.FullName}}
                'Enter' {if($st.Files.Count){$item=$st.Files[$st.FileIndex];if($item.PSIsContainer){Update-SeemlessFiles $item.FullName}else{Open-SeemlessFile $item.FullName}}}
                'O' {if($st.Files.Count){Start-SeemlessExternal $st.Files[$st.FileIndex].FullName}}
                'T' {Start-SeemlessPrompt NewText 'New note' 'Give your note a filename, for example ideas.txt'}
                'M' {Start-SeemlessPrompt NewFolder 'New folder' 'Choose a name for the folder.'}
            }
        }
        'Apps' {
            $apps=@(Get-SeemlessVisibleApps);$st.AppIndex=[Math]::Max(0,[Math]::Min([Math]::Max(0,$apps.Count-1),$st.AppIndex+$delta))
            if($Key.KeyChar -eq '/') {Start-SeemlessPrompt Search 'Find an app' 'Search app names or categories.' $st.AppQuery}
            if($Key.Key -eq 'R'){$st.AppQuery='';$st.AppIndex=0}
            if($Key.Key -eq 'Enter' -and $apps.Count){$st.Modal=$apps[$st.AppIndex]}
        }
        'Control' {
            $st.ControlIndex=[Math]::Max(0,[Math]::Min($script:SeemlessControls.Count-1,$st.ControlIndex+$delta))
            if($delta){$st.SettingsScroll=0}
            if($Key.Key -eq 'R'){$st.SettingsCache=@{}}
            if($Key.Key -eq 'Enter' -and $script:SeemlessControls[$st.ControlIndex].Name -eq 'Browser proxy'){
                Start-SeemlessPrompt Proxy 'Browser proxy' 'http://host:port, or leave blank for system default.' (Get-SeemlessProxy)
            }
            if($Key.Key -eq 'PageDown'){$st.SettingsScroll += 5};if($Key.Key -eq 'PageUp'){$st.SettingsScroll=[Math]::Max(0,$st.SettingsScroll-5)}
            if($Key.Key -eq 'Enter' -and $script:SeemlessControls[$st.ControlIndex].Name -eq 'Appearance'){
                $themes=@('Ocean','Emerald','Violet');$next=($themes.IndexOf($st.Theme)+1)%3;Set-SeemlessTheme $themes[$next] -Save;$st.SettingsCache=@{}
            }
        }
        'Preview' {
            if($Key.Key -eq 'E'){Start-SeemlessEditor $st.Preview}
            if($Key.Key -eq 'O'){Start-SeemlessExternal $st.Preview.Path}
            $st.Preview.Scroll=[Math]::Max(0,$st.Preview.Scroll+$delta)
            if($Key.Key -eq 'PageDown'){$st.Preview.Scroll += [Math]::Max(1,$st.Height-13)}
            if($Key.Key -eq 'PageUp'){$st.Preview.Scroll=[Math]::Max(0,$st.Preview.Scroll-[Math]::Max(1,$st.Height-13))}
        }

    }
}
function Start-SeemlessOS {
    $st=$script:SeemlessState
    $oldFg=[Console]::ForegroundColor;$oldBg=[Console]::BackgroundColor;$oldTitle=''
    try{$oldTitle=[Console]::Title}catch{}
    $oldCursor=$true;try{$oldCursor=[Console]::CursorVisible}catch{}
    $oldControlC=$false;try{$oldControlC=[Console]::TreatControlCAsInput;[Console]::TreatControlCAsInput=$true}catch{}
    $oldMode=[uint32]0;$changedMode=$false;$handle=[IntPtr]::Zero
    try {
        try{$handle=[SeemlessNative]::GetStdHandle(-11);if([SeemlessNative]::GetConsoleMode($handle,[ref]$oldMode)){$changedMode=[SeemlessNative]::SetConsoleMode($handle,($oldMode -bor 4));$st.Ansi=$changedMode}}catch{$st.Ansi=($env:TERM -match 'xterm|screen|vt100')}
        [Console]::Title='Seemless 0S MODE - Made by N0VALID'
        [Console]::CursorVisible=$false
        [Console]::BackgroundColor='Black';[Console]::ForegroundColor='Cyan'
        $lastSize='';$lastClock='';$lastInstallTick=[DateTime]::MinValue
        while($st.Running){
            [void](Update-SeemlessInstall)
            Show-SeemlessScreen
            $lastSize='{0}:{1}' -f [Console]::WindowWidth,[Console]::WindowHeight
            $lastClock=(Get-Date).ToString('HH:mm')
            while(-not [Console]::KeyAvailable){
                Start-Sleep -Milliseconds 35
                if($st.InstallJob -and ((Get-Date)-$lastInstallTick).TotalMilliseconds -gt 500){$lastInstallTick=Get-Date;break}
                if($lastSize -ne ('{0}:{1}' -f [Console]::WindowWidth,[Console]::WindowHeight) -or $lastClock -ne (Get-Date).ToString('HH:mm')){break}
            }
            if([Console]::KeyAvailable){
                try{Invoke-SeemlessKey ([Console]::ReadKey($true))}catch{Set-SeemlessStatus $_.Exception.Message}
            }
        }
    }finally{
        [Console]::ForegroundColor=$oldFg;[Console]::BackgroundColor=$oldBg
        [Console]::CursorVisible=$oldCursor
        try{[Console]::TreatControlCAsInput=$oldControlC}catch{}
        try{[Console]::Title=$oldTitle}catch{}
        if($changedMode){[void][SeemlessNative]::SetConsoleMode($handle,$oldMode)}
        [Console]::Clear()
    }
}
if(-not $NoRun){Start-SeemlessOS}
