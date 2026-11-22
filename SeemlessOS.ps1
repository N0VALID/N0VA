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
$script:SeemlessControls = @(
    @{Name='Windows Settings';Target='ms-settings:';Detail='Open the full Windows Settings app'},
    @{Name='Installed apps';Target='ms-settings:appsfeatures';Detail='Manage or uninstall installed apps'},
    @{Name='Windows Update';Target='ms-settings:windowsupdate';Detail='Check for Windows updates'},
    @{Name='Network & Internet';Target='ms-settings:network';Detail='Wi-Fi, Ethernet and connectivity'},
    @{Name='Display';Target='ms-settings:display';Detail='Resolution, scale and monitors'},
    @{Name='Sound';Target='ms-settings:sound';Detail='Playback and recording devices'},
    @{Name='Storage';Target='ms-settings:storagesense';Detail='Storage usage and cleanup settings'},
    @{Name='Startup apps';Target='ms-settings:startupapps';Detail='Choose what opens when Windows starts'},
    @{Name='System information';Target='msinfo32.exe';Detail='Hardware and Windows information'},
    @{Name='Task Manager';Target='taskmgr.exe';Detail='View running apps and resource use'}
)
$script:SeemlessState = @{
    View='Desktop'; DesktopIndex=0; StartIndex=0; FileIndex=0; AppIndex=0; ControlIndex=0
    Path=[Environment]::GetFolderPath('UserProfile'); Files=@(); AppQuery=''; Status='Ready. Press S for Start, or use F1-F5.'
    Browser=$null; BrowserScroll=0; LinkIndex=0; LinkFocus=$false; History=(New-Object 'System.Collections.Generic.List[object]')
    Modal=$null; Prompt=$null; Running=$true; Width=120; Height=40; Ansi=$false
}
$script:SeemlessBookmarks = @(
    @{Name='N0VA on GitHub';Url='https://github.com/N0VALID/N0VA'},
    @{Name='Python documentation';Url='https://docs.python.org/3/'},
    @{Name='Wikipedia';Url='https://en.wikipedia.org/wiki/Main_Page'},
    @{Name='RustDesk';Url='https://rustdesk.com/'},
    @{Name='Microsoft PowerToys';Url='https://learn.microsoft.com/en-us/windows/powertoys/'}
)
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
    $script:SeemlessState.View=$View
    if($View -eq 'Explorer'){Update-SeemlessFiles}
    Set-SeemlessStatus ('{0}  |  Esc: Desktop  F10: N0VA' -f $View)
}
function Get-SeemlessPage {
    param([string]$Address)
    $Address=$Address.Trim()
    if(-not $Address){throw 'Enter a URL or search words.'}
    if($Address -notmatch '^https?://') {
        if($Address -match '\s' -or $Address -notmatch '\.'){$Address='https://html.duckduckgo.com/html/?q='+[Uri]::EscapeDataString($Address)}
        else{$Address='https://'+$Address}
    }
    $uri=$null
    if(-not [Uri]::TryCreate($Address,[UriKind]::Absolute,[ref]$uri) -or $uri.Scheme -notin @('http','https')){throw 'Only http:// and https:// pages are supported.'}
    [Net.ServicePointManager]::SecurityProtocol=[Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $response=Invoke-WebRequest -UseBasicParsing -Uri $uri.AbsoluteUri -TimeoutSec 15 -MaximumRedirection 5 -UserAgent 'N0VA-SeemlessReader/1.0'
    $html=[string]$response.Content
    if($html.Length -gt 1500000){$html=$html.Substring(0,1500000)}
    $baseUri=$uri
    try{if($response.BaseResponse.ResponseUri){$baseUri=$response.BaseResponse.ResponseUri}elseif($response.BaseResponse.RequestMessage.RequestUri){$baseUri=$response.BaseResponse.RequestMessage.RequestUri}}catch{}
    $titleMatch=[regex]::Match($html,'(?is)<title[^>]*>(.*?)</title>')
    $title=[Net.WebUtility]::HtmlDecode([regex]::Replace($titleMatch.Groups[1].Value,'<[^>]+>',' ')).Trim()
    if(-not $title){$title=$baseUri.Host}
    $clean=[regex]::Replace($html,'(?is)<(script|style|noscript|svg)\b[^>]*>.*?</\1\s*>',' ')
    $links=New-Object 'System.Collections.Generic.List[object]'
    $seen=New-Object 'System.Collections.Generic.HashSet[string]'
    foreach($match in [regex]::Matches($clean,'(?is)<a\b[^>]*href\s*=\s*["'']([^"'']+)["''][^>]*>(.*?)</a>')) {
        if($links.Count -ge 100){break}
        $href=[Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
        if($href.StartsWith('#')){continue}
        $linkUri=$null
        if(-not [Uri]::TryCreate($baseUri,$href,[ref]$linkUri) -or $linkUri.Scheme -notin @('http','https')){continue}
        $label=[Net.WebUtility]::HtmlDecode([regex]::Replace($match.Groups[2].Value,'<[^>]+>',' '))
        $label=(ConvertTo-SeemlessSafeText ([regex]::Replace($label,'\s+',' '))).Trim()
        if($label -and $seen.Add($linkUri.AbsoluteUri)){$links.Add(@{Name=$label;Url=$linkUri.AbsoluteUri})}
    }
    $text=[regex]::Replace($clean,'(?is)</?(p|div|h[1-6]|li|tr|br|section|article|header|footer)\b[^>]*>',"`n")
    $text=[Net.WebUtility]::HtmlDecode([regex]::Replace($text,'<[^>]+>',' '))
    $text=ConvertTo-SeemlessSafeText $text
    $text=[regex]::Replace($text,'[ \t]+',' ')
    $text=[regex]::Replace($text,'(\r?\n\s*){3,}',"`n`n").Trim()
    if(-not $text){$text='This page has no readable text. Press O to open it in your browser.'}
    return @{Url=$baseUri.AbsoluteUri;Title=(ConvertTo-SeemlessSafeText $title);Text=$text;Links=@($links.ToArray())}
}
function Open-SeemlessPage {
    param([string]$Address,[switch]$NoHistory)
    Set-SeemlessStatus 'Loading page... (15 second timeout)'
    Show-SeemlessScreen
    try {
        $page=Get-SeemlessPage $Address
        if($script:SeemlessState.Browser -and -not $NoHistory){
            if($script:SeemlessState.History.Count -ge 20){$script:SeemlessState.History.RemoveAt(0)}
            $script:SeemlessState.History.Add($script:SeemlessState.Browser)
        }
        $script:SeemlessState.Browser=$page;$script:SeemlessState.BrowserScroll=0;$script:SeemlessState.LinkIndex=0;$script:SeemlessState.LinkFocus=$false
        Set-SeemlessStatus ('Loaded '+$page.Title+' | Tab: links  O: full browser')
    }catch{Set-SeemlessStatus ('Web: '+$_.Exception.Message)}
}
function Start-SeemlessExternal {
    param([string]$Target)
    try { Start-Process -FilePath $Target -ErrorAction Stop | Out-Null;Set-SeemlessStatus ('Opened '+$Target) }
    catch { Set-SeemlessStatus ('Could not open: '+$_.Exception.Message) }
}
function Install-SeemlessApp {
    param($App)
    try {
        [Console]::CursorVisible=$true
        [Console]::ResetColor();[Console]::Clear()
        Write-Host ('N0VA App Center - Installing '+$App.Name) -ForegroundColor Cyan
        Write-Host ('Package: '+$App.Id+"`n")
        $winget=Get-Command winget.exe -ErrorAction SilentlyContinue
        if(-not $winget){throw 'WinGet is missing. Install or update App Installer from Microsoft Store.'}
        & $winget.Source install --id $App.Id --exact --source winget --accept-package-agreements --accept-source-agreements
        if($LASTEXITCODE -ne 0){throw ('WinGet did not complete successfully (exit '+$LASTEXITCODE+'). Read its message above.')}
        Set-SeemlessStatus ($App.Name+' installation completed.')
        Write-Host "`nInstallation complete." -ForegroundColor Green
    }catch {Set-SeemlessStatus $_.Exception.Message;Write-Host ("`n"+$_.Exception.Message) -ForegroundColor Yellow}
    finally {Write-Host "`nPress any key to return to App Center...";[void][Console]::ReadKey($true)}
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
        $screen.Fill($offset,$y,[Math]::Max(0,$g.W-$offset),1,$c.Ink,(0x08264B+$y*256))
    }
    $screen.Text(2,0,'N0VA  /  Seemless 0S MODE',$c.Accent,$c.Desktop)
    $screen.Text([Math]::Max(2,$g.W-28),0,('Made by N0VALID  |  v1.0'),$c.Muted,$c.Desktop)
    $screen.Fill(0,$g.H-3,$g.W,1,$c.Muted,$c.Side)
    $screen.Text(2,$g.H-3,(ConvertTo-SeemlessSafeText $st.Status),$c.Muted,$c.Side)
    $screen.Fill(0,$g.H-2,$g.W,2,$c.Ink,0x0A1326)
    $screen.Text(2,$g.H-2,'[S] Start   F1 Desktop  F2 Files  F3 Web  F4 Apps  F5 Control',$c.Ink,0x0A1326)
    $screen.Text(2,$g.H-1,'Arrows: select   Enter: open   Esc: desktop   F10: return to N0VA',$c.Muted,0x0A1326)
    $clock=(Get-Date).ToString('HH:mm  dd MMM')
    if($g.W -gt 95){$screen.Text($g.W-$clock.Length-2,$g.H-2,$clock,$c.Accent,0x0A1326)}
    if($st.View -in @('Desktop','Start')) {
        $screen.Text(5,4,'YOUR WORKSPACE.',$c.Ink,$c.Desktop)
        $screen.Text(5,6,'Everything you need, one terminal.',$c.Muted,$c.Desktop)
        $cards=@('File Explorer','Web Reader','App Center','Control Panel')
        for($i=0;$i -lt 4;$i++){
            $y=9+$i*2;if($y -ge $g.H-5){break}
            $bg=$c.Desktop;$fg=$c.Ink;if($st.DesktopIndex -eq $i){$bg=$c.Select;$fg=$c.Accent}
            $screen.Fill(4,$y,28,1,$fg,$bg);$screen.Text(5,$y,('[ '+($i+1)+' ]  '+$cards[$i]),$fg,$bg)
        }
        if($g.W -ge 95){
            $wx=[int]($g.W*.65);$wy=[int]($g.H*.3)
            $screen.Fill($wx,$wy,10,4,$c.Ink,0x1387F5);$screen.Fill($wx+11,$wy,10,4,$c.Ink,0x37ACF8)
            $screen.Fill($wx,$wy+5,10,4,$c.Ink,0x0F6BD6);$screen.Fill($wx+11,$wy+5,10,4,$c.Ink,0x1686EB)
            $screen.Text($wx,$wy+11,'Seemless 0S MODE',$c.Accent,0x08264B)
        }
        if($st.View -eq 'Start') {
            $sx=3;$sy=[Math]::Max(3,$g.H-19);$sw=[Math]::Min(43,$g.W-6)
            Draw-SeemlessBox $screen $sx $sy $sw 14 'Start'
            $screen.Text($sx+2,$sy+2,'PINNED',$c.Muted,$c.Panel)
            $startItems=@(@{Name='File Explorer'},@{Name='Web Reader'},@{Name='App Center'},@{Name='Control Panel'},@{Name='Return to N0VA'})
            Draw-SeemlessList $screen $startItems $st.StartIndex ($sx+2) ($sy+4) ($sw-4) 6 'Start'
            $screen.Text($sx+2,$sy+11,'N0VALID       Your terminal workspace',$c.Accent,$c.Panel)
        }
    }else{
        $titles=@{Explorer='File Explorer';Web='Web Reader';Apps='App Center';Control='Control Panel'}
        Draw-SeemlessBox $screen $g.X $g.Y $g.WW $g.WH $titles[$st.View]
        switch($st.View){
            'Explorer' {
                $screen.Text($g.BodyX,5,('This PC > '+$st.Path),$c.Accent,$c.Panel)
                $screen.Text($g.BodyX,$g.H-6,'L Location  H Home  N N0VA  Backspace Parent  R Refresh',$c.Muted,$c.Side)
                Draw-SeemlessList $screen $st.Files $st.FileIndex $g.BodyX $g.BodyY $g.BodyW $g.BodyH 'Files'
            }
            'Apps' {
                $apps=@(Get-SeemlessVisibleApps)
                $screen.Text($g.BodyX,5,('Browse apps   |   Search: '+$(if($st.AppQuery){$st.AppQuery}else{'all apps'})),$c.Accent,$c.Panel)
                $listWidth=$g.BodyW
                if($g.W -gt 110){$listWidth=[int]($g.BodyW*.57)}
                Draw-SeemlessList $screen $apps $st.AppIndex $g.BodyX $g.BodyY $listWidth $g.BodyH 'Apps'
                if($g.W -gt 110 -and $apps.Count -gt 0){
                    $app=$apps[$st.AppIndex];$dx=$g.BodyX+$listWidth+3
                    $screen.Text($dx,8,$app.Name,$c.Accent,$c.Panel)
                    $screen.Text($dx,10,$app.Id,$c.Muted,$c.Panel)
                    $detail=@(Get-SeemlessLines $app.Detail ($g.BodyW-$listWidth-4))
                    for($i=0;$i -lt $detail.Count -and $i -lt 4;$i++){$screen.Text($dx,12+$i,$detail[$i],$c.Ink,$c.Panel)}
                    $screen.Text($dx,18,'Enter to install',$c.Green,$c.Panel)
                }
                $screen.Text($g.BodyX,$g.H-6,'/ Search   Enter Install selected app   R Reset search',$c.Muted,$c.Side)
            }
            'Control' {
                $screen.Text($g.BodyX,5,'Settings and tools on this Windows PC',$c.Accent,$c.Panel)
                Draw-SeemlessList $screen $script:SeemlessControls $st.ControlIndex $g.BodyX $g.BodyY $g.BodyW $g.BodyH 'Control'
                $screen.Text($g.BodyX,$g.H-6,'Enter opens the selected Windows setting or utility',$c.Muted,$c.Side)
            }
            'Web' {
                $address='Start page';if($st.Browser){$address=$st.Browser.Url}
                $screen.Text($g.BodyX,5,('Address: '+$address),$c.Accent,$c.Panel)
                $screen.Text($g.BodyX,$g.H-6,'L Address/search  B Back  Tab Links  O Full browser  PgUp/PgDn',$c.Muted,$c.Side)
                if(-not $st.Browser){
                    $screen.Text($g.BodyX,7,'Browse readable pages without leaving the terminal.',$c.Ink,$c.Panel)
                    $screen.Text($g.BodyX,8,'Interactive sites and logins: press O for your full browser.',$c.Muted,$c.Panel)
                    Draw-SeemlessList $screen $script:SeemlessBookmarks $st.LinkIndex $g.BodyX 10 $g.BodyW ([Math]::Max(1,$g.BodyH-3)) 'Links'
                }elseif($st.LinkFocus){
                    $screen.Text($g.BodyX,6,('LINKS - '+$st.Browser.Title),$c.Muted,$c.Panel)
                    Draw-SeemlessList $screen $st.Browser.Links $st.LinkIndex $g.BodyX $g.BodyY $g.BodyW $g.BodyH 'Links'
                }else{
                    $wrapWidth=$g.BodyW-1
                    if($st.Browser.WrappedWidth -ne $wrapWidth){
                        $st.Browser.WrappedLines=@(Get-SeemlessLines $st.Browser.Text $wrapWidth)
                        $st.Browser.WrappedWidth=$wrapWidth
                    }
                    $lines=$st.Browser.WrappedLines
                    $st.BrowserScroll=[Math]::Min($st.BrowserScroll,[Math]::Max(0,$lines.Count-$g.BodyH))
                    for($row=0;$row -lt $g.BodyH -and ($row+$st.BrowserScroll) -lt $lines.Count;$row++){$screen.Text($g.BodyX,$g.BodyY+$row,$lines[$row+$st.BrowserScroll],$c.Ink,$c.Panel)}
                    $screen.Text($g.BodyX,6,('READER - '+$st.Browser.Title),$c.Muted,$c.Panel)
                }
            }
        }
    }
    if($st.Modal -or $st.Prompt){
        $mw=[Math]::Min(66,$g.W-6);$mx=[int](($g.W-$mw)/2);$my=[Math]::Max(2,[int](($g.H-8)/2))
        $screen.Fill(0,0,$g.W,1,$c.Muted,$c.Desktop)
        if($st.Modal){
            Draw-SeemlessBox $screen $mx $my $mw 8 'Install app'
            $screen.Text($mx+2,$my+2,('Install '+$st.Modal.Name+'?'),$c.Ink,$c.Panel)
            $screen.Text($mx+2,$my+3,$st.Modal.Id,$c.Muted,$c.Panel)
            $screen.Text($mx+2,$my+5,'[Y] Install with WinGet     [Esc / N] Cancel',$c.Green,$c.Panel)
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
        'Address' {Open-SeemlessPage $prompt.Value}
    }
}
function Invoke-SeemlessKey {
    param([ConsoleKeyInfo]$Key)
    $st=$script:SeemlessState
    if($Key.Key -eq 'F10'){$st.Running=$false;return}
    if($st.Prompt){
        if($Key.Key -eq 'Escape'){$st.Prompt=$null;Set-SeemlessStatus 'Canceled.';return}
        if($Key.Key -eq 'A' -and ($Key.Modifiers -band [ConsoleModifiers]::Control)){$st.Prompt.Value='';return}
        if($Key.Key -eq 'Enter'){Invoke-SeemlessPrompt;return}
        if($Key.Key -eq 'Backspace'){if($st.Prompt.Value.Length){$st.Prompt.Value=$st.Prompt.Value.Substring(0,$st.Prompt.Value.Length-1)};return}
        if(-not [char]::IsControl($Key.KeyChar) -and $st.Prompt.Value.Length -lt 2048){$st.Prompt.Value += [string]$Key.KeyChar}
        return
    }
    if($st.Modal){
        if($Key.Key -eq 'Y'){$app=$st.Modal;$st.Modal=$null;Install-SeemlessApp $app}
        elseif($Key.Key -in @('Escape','N')){$st.Modal=$null;Set-SeemlessStatus 'Installation canceled.'}
        return
    }
    switch($Key.Key){
        'F1' {Open-SeemlessView Desktop;return}
        'F2' {Open-SeemlessView Explorer;return}
        'F3' {Open-SeemlessView Web;return}
        'F4' {Open-SeemlessView Apps;return}
        'F5' {Open-SeemlessView Control;return}
        'S' {if($st.View -eq 'Start'){$st.View='Desktop'}else{$st.View='Start'};return}
        'Escape' {Open-SeemlessView Desktop;return}
    }
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
            if($Key.Key -eq 'Enter'){if($st.StartIndex -eq 4){$st.Running=$false}else{Open-SeemlessView @('Explorer','Web','Apps','Control')[$st.StartIndex]}}
        }
        'Explorer' {
            $st.FileIndex=[Math]::Max(0,[Math]::Min([Math]::Max(0,$st.Files.Count-1),$st.FileIndex+$delta))
            switch($Key.Key){
                'L' {Start-SeemlessPrompt Location 'Go to folder' 'Type a full folder path.' $st.Path}
                'H' {Update-SeemlessFiles ([Environment]::GetFolderPath('UserProfile'))}
                'N' {if(-not (Test-Path -LiteralPath $InstallDir)){[void][IO.Directory]::CreateDirectory($InstallDir)};Update-SeemlessFiles $InstallDir}
                'R' {Update-SeemlessFiles}
                'Backspace' {$parent=[IO.Directory]::GetParent($st.Path);if($parent){Update-SeemlessFiles $parent.FullName}}
                'Enter' {if($st.Files.Count){$item=$st.Files[$st.FileIndex];if($item.PSIsContainer){Update-SeemlessFiles $item.FullName}else{Start-SeemlessExternal $item.FullName}}}
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
            if($Key.Key -eq 'Enter'){Start-SeemlessExternal $script:SeemlessControls[$st.ControlIndex].Target}
        }
        'Web' {
            switch($Key.Key){
                'L' {Start-SeemlessPrompt Address 'Address or search' 'Enter https://example.com or search words.'}
                'B' {if($st.History.Count){$st.Browser=$st.History[$st.History.Count-1];$st.History.RemoveAt($st.History.Count-1)}else{$st.Browser=$null};$st.BrowserScroll=0;$st.LinkIndex=0;$st.LinkFocus=$false}
                'O' {$url=if($st.Browser){$st.Browser.Url}else{$script:SeemlessBookmarks[$st.LinkIndex].Url};Start-SeemlessExternal $url}
                'Tab' {$st.LinkFocus=-not $st.LinkFocus}
                'Home' {$st.BrowserScroll=0;$st.LinkIndex=0}
                'PageDown' {$st.BrowserScroll += [Math]::Max(1,$st.Height-13)}
                'PageUp' {$st.BrowserScroll=[Math]::Max(0,$st.BrowserScroll-[Math]::Max(1,$st.Height-13))}
            }
            if(-not $st.Browser -or $st.LinkFocus){
                $links=$script:SeemlessBookmarks;if($st.Browser){$links=$st.Browser.Links}
                $st.LinkIndex=[Math]::Max(0,[Math]::Min([Math]::Max(0,$links.Count-1),$st.LinkIndex+$delta))
                if($Key.Key -eq 'Enter' -and $links.Count){Open-SeemlessPage $links[$st.LinkIndex].Url}
            }else{$st.BrowserScroll=[Math]::Max(0,$st.BrowserScroll+$delta)}
        }
    }
}
function Start-SeemlessOS {
    $st=$script:SeemlessState
    $oldFg=[Console]::ForegroundColor;$oldBg=[Console]::BackgroundColor;$oldTitle=''
    try{$oldTitle=[Console]::Title}catch{}
    $oldCursor=$true;try{$oldCursor=[Console]::CursorVisible}catch{}
    $oldMode=[uint32]0;$changedMode=$false;$handle=[IntPtr]::Zero
    try {
        try{$handle=[SeemlessNative]::GetStdHandle(-11);if([SeemlessNative]::GetConsoleMode($handle,[ref]$oldMode)){$changedMode=[SeemlessNative]::SetConsoleMode($handle,($oldMode -bor 4));$st.Ansi=$changedMode}}catch{$st.Ansi=($env:TERM -match 'xterm|screen|vt100')}
        [Console]::Title='Seemless 0S MODE - Made by N0VALID'
        [Console]::CursorVisible=$false
        [Console]::BackgroundColor='Black';[Console]::ForegroundColor='Cyan'
        $lastSize='';$lastClock=''
        while($st.Running){
            Show-SeemlessScreen
            $lastSize='{0}:{1}' -f [Console]::WindowWidth,[Console]::WindowHeight
            $lastClock=(Get-Date).ToString('HH:mm')
            while(-not [Console]::KeyAvailable){
                Start-Sleep -Milliseconds 35
                if($lastSize -ne ('{0}:{1}' -f [Console]::WindowWidth,[Console]::WindowHeight) -or $lastClock -ne (Get-Date).ToString('HH:mm')){break}
            }
            if([Console]::KeyAvailable){
                try{Invoke-SeemlessKey ([Console]::ReadKey($true))}catch{Set-SeemlessStatus $_.Exception.Message}
            }
        }
    }finally{
        [Console]::ForegroundColor=$oldFg;[Console]::BackgroundColor=$oldBg
        [Console]::CursorVisible=$oldCursor
        try{[Console]::Title=$oldTitle}catch{}
        if($changedMode){[void][SeemlessNative]::SetConsoleMode($handle,$oldMode)}
        [Console]::Clear()
    }
}
if(-not $NoRun){Start-SeemlessOS}
