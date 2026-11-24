@echo off
setlocal EnableExtensions
title N0VA - Made by N0VALID
rem Windows Terminal owns fullscreen itself; launch it explicitly in fullscreen.
if /i "%~1"=="--nova-fullscreen" goto nova_started
where wt.exe >nul 2>&1
if errorlevel 1 goto nova_started
start "" wt.exe -w new -F new-tab --title "N0VA - Made by N0VALID" "%ComSpec%" /d /c ""%~f0" --nova-fullscreen"
if not errorlevel 1 exit 0
:nova_started
color 0B

set "N0VA_LAUNCHER=%~f0"
set "N0VA_INTRO=1"
set "BASE_URL=https://raw.githubusercontent.com/N0VALID/N0VA/main"
set "N0VA_BUILD=2.2"
set "DESKTOP_DIR=%USERPROFILE%\Desktop"
for /f "usebackq tokens=1,* delims=|" %%I in (`powershell.exe -NoProfile -Command "([guid]::NewGuid().ToString('N'))+'|'+[Environment]::GetFolderPath('Desktop')" 2^>nul`) do (
    set "N0VA_RUN_ID=%%I"
    if not "%%J"=="" set "DESKTOP_DIR=%%J"
)
set "INSTALL_DIR=%DESKTOP_DIR%\N0VA"
set "N0VA_PAGE=1"

:menu
set "N0VA_VIEW=menu"
powershell.exe -NoProfile -Command "$text = [IO.File]::ReadAllText($env:N0VA_LAUNCHER); $marker = '# N0VA_STARTUP_PS'; try { & ([scriptblock]::Create($text.Substring($text.LastIndexOf($marker) + $marker.Length))) } catch { Write-Host ('N0VA error: ' + $_.Exception.Message) -ForegroundColor Red; exit 250 }"
set "choice=%errorlevel%"
set "N0VA_INTRO="
set "N0VA_VIEW="
if "%choice%"=="122" goto desktop_page_two
if "%choice%"=="120" goto owner_fallback
if "%choice%"=="25" goto sharing_fallback
if "%choice%"=="250" goto menu_fallback
if "%choice%"=="101" goto next_page
if "%choice%"=="100" goto previous_page
goto dispatch

:menu_fallback
echo N0VA - Made by N0VALID - v2.2 - Page %N0VA_PAGE% of 2
if "%N0VA_PAGE%"=="1" (
    echo [1] Python [2] EXE Compiler [3] Computer Info [4] N0VA Folder
    echo [5] WinUtil [6] Update Apps [7] Disk Cleanup [8] Startup Apps
    echo [9] Command Explorer [10] Brave [11] Steam [12] Python packages
    echo [13] RustDesk  [21] Seemless 0S MODE
) else (
    echo [14] 7-Zip [15] Notepad++ [16] VS Code [17] Git
    echo [18] VLC [19] Everything Search [20] PowerToys
    echo [22] Live performance [23] Recent app crashes [24] Connection check
)
echo [25] Run sharing  [A] Previous page  [D] Next page  [0] Exit
powershell.exe -NoProfile -Command "$v=Read-Host 'N0VA'; if($v -eq 'nnnn'){exit 120}; if($v -eq 'seemless'){exit 21}; if($v -eq 'd'){exit 101}; if($v -eq 'a'){exit 100}; if($v -match '^[0-9]{1,2}$'){exit ([int]$v)}; exit 250"
set "choice=%errorlevel%"
if "%choice%"=="120" goto owner_fallback
if "%choice%"=="25" goto sharing_fallback
if "%choice%"=="250" goto menu_fallback
if "%choice%"=="101" goto next_page
if "%choice%"=="100" goto previous_page

:dispatch
rem Remember the page selected inside the persistent PowerShell menu.
if %choice% GEQ 14 if %choice% LEQ 20 set "N0VA_PAGE=2"
if %choice% GEQ 1 if %choice% LEQ 13 set "N0VA_PAGE=1"
if "%choice%"=="1" goto python
if "%choice%"=="2" goto studio
if "%choice%"=="3" goto computer
if "%choice%"=="4" goto folder
if "%choice%"=="5" goto winutil
if "%choice%"=="6" goto updates
if "%choice%"=="7" goto cleanup
if "%choice%"=="8" goto startup
if "%choice%"=="9" goto explorer
if "%choice%"=="10" goto brave
if "%choice%"=="11" goto steam
if "%choice%"=="12" goto dependencies
if "%choice%"=="13" goto rustdesk
if "%choice%"=="14" goto sevenzip
if "%choice%"=="15" goto notepad
if "%choice%"=="16" goto vscode
if "%choice%"=="17" goto git_install
if "%choice%"=="18" goto vlc
if "%choice%"=="19" goto everything
if "%choice%"=="20" goto powertoys
if "%choice%"=="21" goto seamless_os
if %choice% GEQ 22 if %choice% LEQ 24 goto diagnostic
if "%choice%"=="0" goto done
echo.
echo Invalid selection.
pause
goto menu

:next_page
set "N0VA_PAGE=2"
goto menu

:previous_page
set "N0VA_PAGE=1"
goto menu

:owner_fallback
set "N0VA_VIEW=owner"
goto fallback_action

:sharing_fallback
set "N0VA_VIEW=sharing"
:fallback_action
powershell.exe -NoProfile -Command "$text=[IO.File]::ReadAllText($env:N0VA_LAUNCHER); $marker='# N0VA_STARTUP_PS'; try { & ([scriptblock]::Create($text.Substring($text.LastIndexOf($marker)+$marker.Length))) } catch { Write-Host $_.Exception.Message; exit 1 }"
if errorlevel 1 pause
goto menu

:diagnostic
set "N0VA_PAGE=2"
set "N0VA_VIEW=diagnostic"
powershell.exe -NoProfile -Command "$text=[IO.File]::ReadAllText($env:N0VA_LAUNCHER); $marker='# N0VA_STARTUP_PS'; & ([scriptblock]::Create($text.Substring($text.LastIndexOf($marker)+$marker.Length)))"
pause
goto menu

:desktop_page_two
set "N0VA_PAGE=2"
goto open_desktop

:seamless_os
set "N0VA_PAGE=1"
:open_desktop
echo Starting Seemless 0S MODE...
call :download "SeemlessOS.ps1"
if errorlevel 1 (
    echo Could not download Seemless 0S MODE. Check your connection and retry.
    pause
    goto menu
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%INSTALL_DIR%\SeemlessOS.ps1" -InstallDir "%INSTALL_DIR%"
if errorlevel 1 (
    echo Seemless 0S MODE stopped with an error. See the details above.
    pause
)
goto menu

:python
where winget >nul 2>&1
if errorlevel 1 goto failed
winget install --id Python.Python.3.14 --exact --source winget --silent --accept-package-agreements --accept-source-agreements --disable-interactivity >nul 2>&1
if errorlevel 1 goto failed
goto complete

:studio
call :download "exe_compiler.pyw"
if errorlevel 1 goto failed
goto complete_download

:computer
call :download "cmptr_info.pyw"
if errorlevel 1 goto failed
call :run_python_gui "cmptr_info.pyw"
if errorlevel 1 goto failed
goto complete_download

:explorer
call :download "Python_Command_Explorer_Pro.pyw"
if errorlevel 1 goto failed
call :run_python_gui "Python_Command_Explorer_Pro.pyw"
if errorlevel 1 goto failed
goto complete_download

:run_python_gui
where pyw.exe >nul 2>&1
if not errorlevel 1 (
    start "" pyw.exe -3 "%INSTALL_DIR%\%~1"
    exit /b
)
where pythonw.exe >nul 2>&1
if not errorlevel 1 (
    start "" pythonw.exe "%INSTALL_DIR%\%~1"
    exit /b
)
echo Python is required. Choose option 1 to install it.
pause
exit /b 1

:winutil
cls
echo Downloading Chris Titus WinUtil. Accept the Windows administrator prompt.
set "N0VA_VIEW=winutil"
powershell.exe -NoProfile -Command "$text = [IO.File]::ReadAllText($env:N0VA_LAUNCHER); $marker = '# N0VA_STARTUP_PS'; try { & ([scriptblock]::Create($text.Substring($text.LastIndexOf($marker) + $marker.Length))) } catch { Write-Host ('N0VA error: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }"
set "N0VA_VIEW="
rem Keep download errors and UAC cancellation visible before returning.
pause
goto menu

:brave
cls
call :install_app "Brave.Brave"
pause
goto menu

:steam
cls
call :install_app "Valve.Steam"
pause
goto menu

:rustdesk
cls
call :install_app "RustDesk.RustDesk"
pause
goto menu

:sevenzip
cls
call :install_app "7zip.7zip"
pause
goto menu

:notepad
cls
call :install_app "Notepad++.Notepad++"
pause
goto menu

:vscode
cls
call :install_app "Microsoft.VisualStudioCode"
pause
goto menu

:git_install
cls
call :install_app "Git.Git"
pause
goto menu

:vlc
cls
call :install_app "VideoLAN.VLC"
pause
goto menu

:everything
cls
call :install_app "voidtools.Everything"
pause
goto menu

:powertoys
cls
call :install_app "Microsoft.PowerToys"
pause
goto menu

:install_app
where winget.exe >nul 2>&1
if errorlevel 1 (
    echo WinGet is missing. Install or update App Installer from Microsoft Store.
    exit /b 1
)
winget.exe install --id "%~1" --exact --source winget --accept-package-agreements --accept-source-agreements
if errorlevel 1 (
    echo Installation did not finish successfully. See the WinGet message above.
    exit /b 1
)
echo Installation complete.
exit /b 0

:dependencies
cls
echo Installing Python dependencies for all N0VA tools...
set "N0VA_VIEW=dependencies"
powershell.exe -NoProfile -Command "$text = [IO.File]::ReadAllText($env:N0VA_LAUNCHER); $marker = '# N0VA_STARTUP_PS'; try { & ([scriptblock]::Create($text.Substring($text.LastIndexOf($marker) + $marker.Length))) } catch { Write-Host ('N0VA error: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }"
set "N0VA_VIEW="
pause
goto menu


:updates
cls
where winget >nul 2>&1
if errorlevel 1 goto failed
echo Updating installed apps...
echo.
winget upgrade --all --accept-package-agreements --accept-source-agreements
if errorlevel 1 goto failed
goto complete

:cleanup
start "" cleanmgr.exe >nul 2>&1
if errorlevel 1 goto failed
goto complete

:startup
start "" "ms-settings:startupapps" >nul 2>&1
if errorlevel 1 goto failed
goto complete

:folder
if not exist "%INSTALL_DIR%\." mkdir "%INSTALL_DIR%" >nul 2>&1
if not exist "%INSTALL_DIR%\." goto failed
rem Explicitly launch Explorer so N0VA.cmd cannot be selected by START.
start "" "%SystemRoot%\explorer.exe" "%INSTALL_DIR%"
if errorlevel 1 goto failed
goto menu

:download
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%" >nul 2>&1
if not exist "%INSTALL_DIR%" exit /b 1
where curl.exe >nul 2>&1
if errorlevel 1 exit /b 1
set "TEMP_FILE=%INSTALL_DIR%\%~1.partial"
if exist "%TEMP_FILE%" del /q "%TEMP_FILE%" >nul 2>&1
curl.exe --fail --location --silent --retry 2 "%BASE_URL%/%~1?build=%N0VA_BUILD%" --output "%TEMP_FILE%" >nul 2>&1
if errorlevel 1 goto download_failed
for %%F in ("%TEMP_FILE%") do if %%~zF EQU 0 goto download_failed
move /y "%TEMP_FILE%" "%INSTALL_DIR%\%~1" >nul 2>&1
if errorlevel 1 goto download_failed
exit /b 0

:download_failed
if exist "%TEMP_FILE%" del /q "%TEMP_FILE%" >nul 2>&1
exit /b 1

:complete_download
cls
echo.
echo Done.
echo Saved in Desktop.
echo.
set "continue="
set /p "continue=Press Enter to continue..."
goto menu

:complete
cls
echo.
echo Done.
echo.
set "continue="
set /p "continue=Press Enter to continue..."
goto menu

:failed
cls
echo.
echo Failed.
echo.
set "continue="
set /p "continue=Press Enter to continue..."
goto menu

:done
endlocal
exit 0

# N0VA_STARTUP_PS
# This section is read by PowerShell; cmd exits above before reaching it.
$ErrorActionPreference = 'Stop'
if ($env:N0VA_VIEW -eq 'winutil') {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    [void][IO.Directory]::CreateDirectory($env:INSTALL_DIR)
    $cttPath = Join-Path $env:INSTALL_DIR 'WinUtil.ps1'
    Invoke-WebRequest -UseBasicParsing -Uri 'https://christitus.com/win' -OutFile $cttPath
    if ((Get-Item -LiteralPath $cttPath).Length -eq 0) { throw 'WinUtil download was empty. Try again.' }
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-NoExit', '-File', ('"{0}"' -f $cttPath))
    $process = Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $arguments -PassThru -ErrorAction Stop
    Write-Host ('WinUtil opened in an administrator PowerShell window (process {0}).' -f $process.Id) -ForegroundColor Green
    Write-Host 'If WinUtil cannot start, its error will stay visible in that window.'
    return
}
if ($env:N0VA_VIEW -eq 'dependencies') {
    function Find-NovaPython {
        foreach ($name in @('py.exe', 'python.exe')) {
            $command = Get-Command $name -ErrorAction SilentlyContinue
            if (-not $command) { continue }
            if ($command.Source -like '*\Microsoft\WindowsApps\python.exe') { continue }
            $arguments = @('-c', 'import sys; print(sys.executable)')
            if ($name -eq 'py.exe') { $arguments = @('-3') + $arguments }
            try { $result = & $command.Source @arguments 2>$null } catch { continue }
            if ($LASTEXITCODE -eq 0 -and $result -and (Test-Path -LiteralPath ([string]($result | Select-Object -Last 1)))) {
                return [string]($result | Select-Object -Last 1)
            }
        }
        # Also find Python installed in this session, before PATH refreshes.
        $roots = @((Join-Path $env:LOCALAPPDATA 'Programs\Python'), $env:ProgramFiles)
        foreach ($root in $roots) {
            if (-not $root -or -not (Test-Path -LiteralPath $root)) { continue }
            foreach ($folder in (Get-ChildItem -LiteralPath $root -Directory -Filter 'Python3*' -ErrorAction SilentlyContinue | Sort-Object Name -Descending)) {
                $candidate = Join-Path $folder.FullName 'python.exe'
                if (Test-Path -LiteralPath $candidate) { return $candidate }
            }
        }
        return $null
    }
    $python = Find-NovaPython
    if (-not $python) {
        Write-Host 'Python was not found. Installing Python first...'
        $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
        if (-not $winget) { throw 'WinGet is missing. Install App Installer from Microsoft Store, then retry.' }
        & $winget.Source install --id Python.Python.3.14 --exact --source winget --accept-package-agreements --accept-source-agreements
        if ($LASTEXITCODE -ne 0) { throw 'Python installation failed. See the WinGet message above.' }
        $python = Find-NovaPython
        if (-not $python) { throw 'Python was installed but could not be found. Reopen N0VA and select option 12 again.' }
    }
    Write-Host ('Using Python: ' + $python) -ForegroundColor Cyan
    & $python -m ensurepip --upgrade
    if ($LASTEXITCODE -ne 0) { throw 'Could not enable pip for this Python installation.' }
    & $python -m pip install --upgrade pip
    if ($LASTEXITCODE -ne 0) { throw 'Updating pip failed. Check the error above and retry.' }
    & $python -m pip install --upgrade pyinstaller pyautogui pillow opencv-python
    if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed. Check the pip error above and retry.' }
    & $python -m pip check
    if ($LASTEXITCODE -ne 0) { throw 'pip found a package conflict. See the details above.' }
    Write-Host 'All N0VA Python dependencies installed successfully.' -ForegroundColor Green
    return
}
if (-not ('NovaDisplay' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
public static class NovaDisplay {
    [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int kind);
    [DllImport("kernel32.dll")] static extern bool GetConsoleMode(IntPtr h, out uint mode);
    [DllImport("kernel32.dll")] static extern bool SetConsoleMode(IntPtr h, uint mode);
    [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int command);
    [DllImport("kernel32.dll")] static extern bool SetConsoleDisplayMode(IntPtr h, uint flags, out int size);
    public static bool UseColor() {
        try { uint mode; var h=GetStdHandle(-11); return GetConsoleMode(h,out mode) && SetConsoleMode(h,mode|4); }
        catch { return !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("TERM")); }
    }
    public static void Fullscreen() {
        try { int size; if (!SetConsoleDisplayMode(GetStdHandle(-11),1,out size)) ShowWindow(GetConsoleWindow(),3); } catch {}
    }
    static readonly string[] logo = {
        "NN   NN   0000   VV     VV    AAA   ",
        "NNN  NN  00  00   VV   VV    AA AA  ",
        "NN N NN  00  00    VV VV    AA   AA ",
        "NN  NNN  00  00     VVV     AAAAAAA ",
        "NN   NN   0000       V      AA   AA " };
    public static string[] Logo { get { return logo; } }
    public static ConsoleKeyInfo? Intro(bool admin, bool color) {
        int w=Console.WindowWidth-1, h=Console.WindowHeight;
        if(w<15 || h<8)return null;
        var rng=new Random();
        var start=new int[w]; var speed=new int[w]; var length=new int[w];
        for(int x=0;x<w;x++) { start[x]=rng.Next(h+22); speed[x]=rng.Next(12,34); length[x]=rng.Next(5,17); }
        string[] mark=admin ? new[]{"[  owner M3NU  ]"} : (w<40 ? new[]{"N0VA","","Made by N0VALID"} :
            new[]{logo[0],logo[1],logo[2],logo[3],logo[4],"","Made by N0VALID"});
        var palette=admin ? new[]{"0","38;2;64;42;26","38;2;166;95;44","38;2;255;191;99","38;2;255;234;196"} :
            new[]{"0","38;2;16;67;53","38;2;27;132;89","38;2;81;223;155","38;2;187;255;223"};
        string chars="0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ<>{}";
        var timer=Stopwatch.StartNew(); var frame=new StringBuilder();
        int duration=admin?690:3050;
        while(timer.ElapsedMilliseconds<duration && Console.WindowWidth-1==w && Console.WindowHeight==h) {
            if(!admin && Console.KeyAvailable) return Console.ReadKey(true);
            int ms=(int)timer.ElapsedMilliseconds, tick=ms/65;
            double progress=Math.Max(0,Math.Min(1,(ms-(admin?60:1740))/(admin?320.0:610.0)));
            frame.Clear(); int last=-1;
            for(int y=0;y<h;y++) {
                int row=y-(h-mark.Length)/2;
                for(int x=0;x<w;x++) {
                    char ch=' '; int ink=0;
                    if(admin) {
                        int radius=ms*w/780;
                        int dx=Math.Abs(x-w/2), dy=Math.Abs(y-h/2);
                        if(dy==2 && dx<=radius) {ch='-';ink=dx>radius-3?4:2;}
                        else if(dx==radius && dy<2){ch='|';ink=3;}
                    } else if((x&1)==0) {
                        int distance=(start[x]+ms*speed[x]/1000)%(h+22)-y;
                        if(distance>=0 && distance<length[x] && (progress<1 || distance<4)) {
                            ch=chars[(start[x]+x*3+y*7+tick/2)%chars.Length];
                            ink=distance==0?4:(distance<3?3:(distance<7?2:1));
                        }
                    }
                    if(row>=0 && row<mark.Length && progress>0) {
                        int col=x-(w-mark[row].Length)/2;
                        if(col>=-2 && col<mark[row].Length+2 && col<progress*(mark[row].Length+3)) {
                            ch=col>=0 && col<mark[row].Length?mark[row][col]:' ';
                            ink=col>progress*(mark[row].Length+3)-3?4:(row==mark.Length-1?4:3);
                        }
                    }
                    if(color && ch!=' ' && ink!=last) {frame.Append("\x1b[").Append(palette[ink]).Append('m');last=ink;}
                    frame.Append(ch);
                }
                if(y<h-1)frame.Append("\r\n");
            }
            if(color)frame.Append("\x1b[0m");
            Console.SetCursorPosition(0,Console.WindowTop);Console.Write(frame.ToString());Thread.Sleep(32);
        }
        return null;
    }
}
'@
}
# Shared history is off until this Windows profile chooses a name and opts in.
$script:ActivityUrl='https://n0valid-n0va-runs.deftdisc2.chatgpt.site/api'
$script:ActivityRoot=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'N0VA'
$script:SharingFile=Join-Path $ActivityRoot 'sharing.json'
$script:OwnerFile=Join-Path $ActivityRoot 'owner.dpapi'
$script:OwnerSummary=$null
$script:HistoryStatus=''
$script:RunSend=$null
$script:Sharing=$null
try {
    if([IO.File]::Exists($SharingFile)){
        $script:Sharing=[IO.File]::ReadAllText($SharingFile)|ConvertFrom-Json
    }
} catch { $script:HistoryStatus='Sharing settings unreadable' }

function Set-NovaPrivateFile([string]$Path,[string]$Value) {
    [void][IO.Directory]::CreateDirectory($ActivityRoot)
    $tmp=$Path+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
    try {
        [IO.File]::WriteAllText($tmp,$Value)
        if([IO.File]::Exists($Path)){[IO.File]::Replace($tmp,$Path,[System.Management.Automation.Language.NullString]::Value)}
        else{[IO.File]::Move($tmp,$Path)}
    }finally{if([IO.File]::Exists($tmp)){[IO.File]::Delete($tmp)}}
}
function Protect-NovaKey([string]$Value) {
    return (ConvertTo-SecureString $Value -AsPlainText -Force | ConvertFrom-SecureString)
}
function Read-NovaKey([string]$Value) {
    $secure=ConvertTo-SecureString $Value
    try{return (New-Object Net.NetworkCredential('', $secure)).Password}finally{$secure.Dispose()}
}
function Get-NovaOwnerData([string]$View,[int]$Page,[string]$Key) {
    if(!$Key){$Key=Read-NovaKey ([IO.File]::ReadAllText($OwnerFile))}
    [Net.ServicePointManager]::SecurityProtocol=[Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $data=Invoke-RestMethod -UserAgent "N0VA/$env:N0VA_BUILD" -Uri "$ActivityUrl/history?view=$View&page=$Page" -Method Get -Headers @{Authorization="Bearer $Key"} -MaximumRedirection 0 -TimeoutSec 8 -ErrorAction Stop
    if($null -eq $data.summary.runs){throw 'The activity service returned an unexpected response.'}
    $script:OwnerSummary=$data.summary
    $script:HistoryStatus=''
    return $data
}
function Connect-NovaOwner {
    Add-Type -AssemblyName System.Windows.Forms
    $dialog=New-Object Windows.Forms.OpenFileDialog
    try{
        $dialog.Title='Pair this PC with your N0VA owner key'
        $dialog.Filter='N0VA owner key (*.key)|*.key|All files (*.*)|*.*'
        $dialog.InitialDirectory=Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads'
        if($dialog.ShowDialog() -ne 'OK'){return $false}
        $file=Get-Item -LiteralPath $dialog.FileName
        if($file.Length -gt 256){throw 'Choose your N0VA-owner.key file.'}
        $key=[IO.File]::ReadAllText($file.FullName).Trim()
        if($key -cnotmatch '^nova-owner-[0-9a-f]{64}$'){throw 'Choose your N0VA-owner.key file.'}
        [void](Get-NovaOwnerData 'recent' 1 $key)
        Set-NovaPrivateFile $OwnerFile (Protect-NovaKey $key)
        return $true
    }finally{$dialog.Dispose()}
}
function Format-NovaSeen([long]$Milliseconds) {
    return ([datetimeoffset]::FromUnixTimeMilliseconds($Milliseconds).LocalDateTime.ToString('MMM d, h:mm tt',[Globalization.CultureInfo]::InvariantCulture))
}
function Show-NovaHistory([string]$View) {
    if(![IO.File]::Exists($OwnerFile)){if(!(Connect-NovaOwner)){return}}
    $page=1
    while($true){
        [Console]::Clear()
        $title=switch($View){'profiles'{'User directory'} 'builds'{'Builds in use'} default{'Run history'}}
        Write-Host ("owner M3NU  /  "+$title) -ForegroundColor Yellow
        $data=Get-NovaOwnerData $View $page
        Write-Host ("{0} launches   {1} profiles   {2} in 24h   |   Last 90 days" -f $data.summary.runs,$data.summary.profiles,$data.summary.last24h)
        Write-Host ''
        if(!$data.rows.Count){Write-Host 'No shared launches on this page.'}
        elseif($View -eq 'recent'){
            Write-Host ('{0,-24}  {1,-18}  {2}' -f 'Display name','Launch time','Build') -ForegroundColor Cyan
            foreach($row in $data.rows){Write-Host ('{0,-24}  {1,-18}  v{2}' -f $row.name,(Format-NovaSeen $row.seen),$row.build)}
        }elseif($View -eq 'builds'){
            Write-Host ('{0,-12} {1,6} {2,9}   {3}' -f 'Build','Runs','Profiles','Last seen') -ForegroundColor Cyan
            foreach($row in $data.rows){Write-Host ('v{0,-11} {1,6} {2,9}   {3}' -f $row.build,$row.runs,$row.profiles,(Format-NovaSeen $row.lastSeen))}
        }else{
            $wide=[Console]::WindowWidth -ge 76
            if($wide){Write-Host ('{0,-24} {1,5}  {2,-17} {3,-17} {4}' -f 'Display name','Runs','First seen','Last seen','Build') -ForegroundColor Cyan}
            foreach($row in $data.rows){
                $first=Format-NovaSeen $row.firstSeen;$last=Format-NovaSeen $row.lastSeen
                if($wide){Write-Host ('{0,-24} {1,5}  {2,-17} {3,-17} v{4}' -f $row.name,$row.runs,$first,$last,$row.build)}
                else{Write-Host ("{0}   {1} runs   v{2}" -f $row.name,$row.runs,$row.build);Write-Host ("  First {0}  /  Last {1}" -f $first,$last)}
            }
        }
        $actions=@("Page $page",'R Refresh','Esc Back')
        if($page -gt 1){$actions+='A Previous'};if($data.more){$actions+='D Next'}
        Write-Host ("`n"+($actions -join '   ')) -ForegroundColor Yellow
        do{$key=[Console]::ReadKey($true).Key}while($key -notin @('R','Escape') -and !($key -in @('A','LeftArrow') -and $page -gt 1) -and !($key -in @('D','RightArrow') -and $data.more))
        if($key -eq 'Escape'){return}
        if($key -in @('A','LeftArrow')){$page--}
        if($key -in @('D','RightArrow')){$page++}
    }
}
function Send-NovaLaunch {
    if(!$Sharing -or $script:RunSend){return}
    Add-Type -AssemblyName System.Net.Http
    [Net.ServicePointManager]::SecurityProtocol=[Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $handler=New-Object Net.Http.HttpClientHandler
    $handler.AllowAutoRedirect=$false
    $client=New-Object Net.Http.HttpClient($handler)
    $client.Timeout=[timespan]::FromSeconds(6)
    $client.DefaultRequestHeaders.UserAgent.ParseAdd("N0VA/$env:N0VA_BUILD")
    $request=$null
    try {
        $token=Read-NovaKey $Sharing.token
        $id=$env:N0VA_RUN_ID
        if(!$id){$id=[guid]::NewGuid().ToString('N');$env:N0VA_RUN_ID=$id}
        $body=@{name=$Sharing.name;launch=$id;build=$env:N0VA_BUILD}|ConvertTo-Json -Compress
        $request=New-Object Net.Http.HttpRequestMessage([Net.Http.HttpMethod]::Post, "$ActivityUrl/launch")
        $request.Headers.Authorization=New-Object Net.Http.Headers.AuthenticationHeaderValue('Bearer',$token)
        $request.Content=New-Object Net.Http.StringContent($body,[Text.Encoding]::UTF8,'application/json')
        $script:RunSend=@{client=$client;request=$request;task=$client.SendAsync($request)}
    }catch{if($request){$request.Dispose()};$client.Dispose();throw}
}
function Complete-NovaLaunch([switch]$Wait) {
    if(!$RunSend){return}
    $job=$RunSend
    if(!$Wait -and !$job.task.IsCompleted){return}
    try{
        $reply=$job.task.GetAwaiter().GetResult()
        try{if(!$reply.IsSuccessStatusCode){throw 'Check-in rejected.'};$script:HistoryStatus='Launch received'}
        finally{$reply.Dispose()}
    }catch{$script:HistoryStatus='Launch could not be shared'}
    finally{$job.request.Dispose();$job.client.Dispose();$script:RunSend=$null}
}
function Show-NovaSharing {
    [Console]::Clear()
    Write-Host 'Run sharing' -ForegroundColor Cyan
    Write-Host 'Share a display name, launch time and N0VA version with N0VALID.'
    Write-Host 'A random profile ID groups your launches. History covers 90 days.'
    Write-Host ''
    if($Sharing){
        Complete-NovaLaunch -Wait
        Write-Host ('On  /  '+$Sharing.name)
        if($HistoryStatus){Write-Host $HistoryStatus}
        Write-Host 'T  Turn off    D  Erase my history    R  Retry    Esc  Back'
        $key=[Console]::ReadKey($true).Key
        if($key -eq 'T'){[IO.File]::Delete($SharingFile);$script:Sharing=$null;$script:HistoryStatus=''}
        if($key -eq 'D'){
            Write-Host 'Erase this profile''s shared history and turn sharing off?  Y / N'
            if([Console]::ReadKey($true).Key -eq 'Y'){
                $token=Read-NovaKey $Sharing.token
                [void](Invoke-RestMethod -Method Delete -UserAgent "N0VA/$env:N0VA_BUILD" -Uri "$ActivityUrl/profile" -Headers @{Authorization="Bearer $token"} -MaximumRedirection 0 -TimeoutSec 8 -ErrorAction Stop)
                [IO.File]::Delete($SharingFile);$script:Sharing=$null;$script:OwnerSummary=$null;$script:HistoryStatus=''
            }
        }
        if($key -eq 'R'){Send-NovaLaunch;Complete-NovaLaunch -Wait;Write-Host $HistoryStatus;[void][Console]::ReadKey($true)}
        return
    }
    Write-Host 'Enter  Choose a name and turn on    Esc  Back'
    if([Console]::ReadKey($true).Key -ne 'Enter'){return}
    [Console]::CursorVisible=$true
    try{$name=(Read-Host 'Display name').Trim()}finally{[Console]::CursorVisible=$false}
    if(!$name){return}
    if($name -cnotmatch '^[A-Za-z0-9][A-Za-z0-9 _.-]{0,23}$'){throw 'Use 1-24 letters, numbers, spaces, dots, dashes or underscores.'}
    [Net.ServicePointManager]::SecurityProtocol=[Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $enroll=Invoke-RestMethod -Method Post -UserAgent "N0VA/$env:N0VA_BUILD" -Uri "$ActivityUrl/enroll" -Headers @{'X-Nova-Consent'='share-launches'} -MaximumRedirection 0 -TimeoutSec 8 -ErrorAction Stop
    if($enroll.token -cnotmatch '^[0-9a-f]{32}\.[0-9a-f]{64}$'){throw 'Could not enable run sharing.'}
    $settings=@{name=$name;token=(Protect-NovaKey $enroll.token)}
    Set-NovaPrivateFile $SharingFile ($settings|ConvertTo-Json -Compress)
    $script:Sharing=$settings
    Send-NovaLaunch;Complete-NovaLaunch -Wait
    Write-Host $HistoryStatus
    Write-Host 'Press a key to return.';[void][Console]::ReadKey($true)
}

$color = [NovaDisplay]::UseColor()
$access='Standard user'
try {
    $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
    $principal=New-Object Security.Principal.WindowsPrincipal($identity)
    if($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){ $access='Elevated' }
} catch {}
$esc = [string][char]27
$reset = "$esc[0m"
# Numbered entries keep their original IDs on both the wide and compact menus.
$pages = @{
    '1' = @('01  Install Python','02  EXE Compiler','03  Computer info','04  N0VA folder',
        '09  Command Explorer','12  Python packages','21  Seemless 0S MODE',
        '05  WinUtil','06  Update apps','07  Disk cleanup','08  Startup apps',
        '10  Install Brave','11  Install Steam','13  Install RustDesk')
    '2' = @('14  7-Zip','15  Notepad++','16  VS Code','17  Git','18  VLC',
        '19  Everything Search','20  PowerToys','22  Live performance','23  Recent app crashes','24  Connection check')
    'admin' = @('07  Run history','08  User directory','10  Builds in use','09  Pair owner key','01  Live performance',
        '02  Recent app crashes','03  Connection check','04  Event Viewer','05  Services','06  Task Manager')
}
function New-NovaFrame {
    param([string]$Page,[int]$Width,[int]$Height)
    $admin = $Page -eq 'admin'
    $accent = "$esc[38;2;111;237;193m"
    $secondary = "$esc[38;2;120;178;250m"
    $dim = "$esc[38;2;119;141;158m"
    $white = "$esc[38;2;227;237;242m"
    if ($admin) { $accent="$esc[38;2;255;191;102m"; $secondary="$esc[38;2;234;152;97m" }
    $span = [Math]::Max(1,[Math]::Min(61,$Width-2))
    $column = [int][Math]::Floor($span/2)
    $rows = New-Object 'System.Collections.Generic.List[string]'
    function Add-Row([string]$value,[string]$ink) {
        $value=$value.Substring(0,[Math]::Min($value.Length,$span)).PadRight($span)
        if ($color) { $value=$ink+$value+$reset }
        $rows.Add($value)
    }
    if (!$admin -and $Height -ge 23) {
        foreach($line in [NovaDisplay]::Logo){ Add-Row $line $accent }
        Add-Row '' ''
    }
    $title = switch($Page){'1'{'N0VA  /  Tools'} '2'{'N0VA  /  Downloads & diagnostics'} 'admin'{'owner M3NU'}}
    Add-Row $title $accent
    Add-Row ('-' * $span) $dim
    if($admin){
        Add-Row ('N0VALID/N0VA   /   v'+$env:N0VA_BUILD) $secondary
        if($OwnerSummary){Add-Row ('{0} profiles   {1} launches   {2} in 24h' -f $OwnerSummary.profiles,$OwnerSummary.runs,$OwnerSummary.last24h) $white}
        elseif([IO.File]::Exists($OwnerFile)){
            $label=if($HistoryStatus -like 'History unavailable*'){$HistoryStatus}else{'Owner key saved   /   Loading activity'}
            Add-Row $label $white
        }
        else{Add-Row 'Private history   /   09  Pair this PC' $white}
    }
    $items = $pages[$Page]
    $split = switch($Page){'1'{7} '2'{7} 'admin'{5}}
    # Shorten only the narrow layout, keeping both the numbers and labels visible.
    if($Width -lt 50 -and $Height -ge ($items.Count+8)){
        foreach($item in $items){Add-Row $item $white}
    }else{
        $headings = switch($Page){
            '1' { 'Toolbox','Windows & apps' }
            '2' { 'Install apps','Diagnostics' }
            'admin' { 'Owner tools','System tools' }
        }
        if($Width -ge 50 -and $Height -ge 16){Add-Row ($headings[0].PadRight($column)+$headings[1]) $secondary}
        for ($i=0;$i -lt $split;$i++) {
            $first=$items[$i]
            $left=$first.Substring(0,[Math]::Min($first.Length,[Math]::Max(1,$column-1))).PadRight($column)
            $right=if($i+$split -lt $items.Count){$items[$i+$split]}else{''}
            $line=($left+$right).PadRight($span).Substring(0,$span)
            if($color){
                $line=[regex]::Replace($line,'(?<!\S)[0-9]{2}(?=  )',($accent+'$0'+$white))
                $line=$white+$line+$reset
            }
            $rows.Add($line)
        }
    }
    Add-Row ('-' * $span) $dim
    if ($admin){Add-Row 'Esc  Back' $secondary}
    else {
        $sharingLabel=if($Sharing){'On'}else{'Off'}
        Add-Row ("25  Run sharing  /  "+$sharingLabel) $white
        Add-Row ("A / D  Page $Page of 2     0  Exit") $secondary
    }
    $top = [Math]::Max(0,[int][Math]::Floor(($Height-$rows.Count-2)/2))
    $leftPad=' '*[Math]::Max(0,[int][Math]::Floor(($Width-$span)/2))
    $rightPad=' '*[Math]::Max(0,$Width-1-$leftPad.Length-$span)
    $blank=' '*[Math]::Max(0,$Width-1)
    $frame=New-Object Text.StringBuilder
    $promptY=[Math]::Min($Height-1,$top+$rows.Count)
    for($y=0;$y -lt $Height;$y++){
        $i=$y-$top
        if($y -eq $promptY){[void]$frame.Append($leftPad+'> '+(' '*[Math]::Max(0,$Width-3-$leftPad.Length)))}
        elseif($i -ge 0 -and $i -lt $rows.Count){[void]$frame.Append($leftPad+$rows[$i]+$rightPad)}
        else{[void]$frame.Append($blank)}
        if($y -lt $Height-1){[void]$frame.Append("`r`n")}
    }
    return @{Text=$frame.ToString();X=$leftPad.Length+2;Y=$promptY}
}
function Show-NovaDiagnostic {
    param([int]$Id)
    [Console]::Clear()
    switch($Id){
        22 {
            Write-Host 'Live performance' -ForegroundColor Cyan
            Write-Host 'Esc  Back'
            $top=[Console]::CursorTop
            while($true){
                $cpu=Get-CimInstance Win32_PerfFormattedData_PerfOS_Processor -Filter "Name='_Total'"
                $os=Get-CimInstance Win32_OperatingSystem
                $total=$os.TotalVisibleMemorySize / 1MB
                $used=$total - $os.FreePhysicalMemory / 1MB
                $cpuPct=[Math]::Min(100,[Math]::Max(0,[int]$cpu.PercentProcessorTime))
                $cpuBar=[int][Math]::Round($cpuPct/5)
                $ramBar=[Math]::Min(20,[Math]::Max(0,[int][Math]::Round(20*$used/$total)))
                $lines=@('',
                    ('CPU   [{0}{1}]  {2}%' -f ('#'*$cpuBar),('-'*(20-$cpuBar)),$cpuPct),
                    ('RAM   [{0}{1}]  {2:N1} / {3:N1} GB' -f ('#'*$ramBar),('-'*(20-$ramBar)),$used,$total),
                    '','Process                          Memory')
                $lines+=@(Get-Process | Sort-Object WorkingSet64 -Descending | Select-Object -First 6 | ForEach-Object {
                    '{0,-28} {1,8:N0} MB' -f $_.ProcessName.Substring(0,[Math]::Min(28,$_.ProcessName.Length)),($_.WorkingSet64/1MB)
                })
                $w=[Math]::Min(60,[Math]::Max(1,[Console]::WindowWidth-1))
                for($i=0;$i -lt $lines.Count -and $top+$i -lt [Console]::WindowHeight;$i++){
                    [Console]::SetCursorPosition(0,$top+$i)
                    [Console]::Write($lines[$i].PadRight($w).Substring(0,$w))
                }
                for($i=0;$i -lt 20;$i++){if([Console]::KeyAvailable -and [Console]::ReadKey($true).Key -eq 'Escape'){return};Start-Sleep -Milliseconds 50}
            }
        }
        23 {
            Write-Host 'Recent app crashes  /  last 7 days' -ForegroundColor Cyan
            try {
                $events=Get-WinEvent -FilterHashtable @{LogName='Application';ProviderName='Application Error';Id=1000;StartTime=(Get-Date).AddDays(-7)} -MaxEvents 12 -ErrorAction Stop
                foreach($event in $events){
                    $xml=[xml]$event.ToXml()
                    $fields=@{}
                    foreach($node in $xml.Event.EventData.Data){$fields[[string]$node.Name]=[string]$node.'#text'}
                    $app=$fields['AppName'];if(!$app){$app=$event.Properties[0].Value}
                    Write-Host ("{0:MMM d  h:mm tt}   {1}" -f $event.TimeCreated,$app)
                    if($fields['ModuleName']){Write-Host ('    '+$fields['ModuleName']+'    '+$fields['ExceptionCode']) -ForegroundColor DarkGray}
                }
            } catch { if($_.FullyQualifiedErrorId -like 'NoMatchingEventsFound*'){Write-Host 'No app crashes recorded.'}else{throw} }
        }
        24 {
            Write-Host 'Connection check' -ForegroundColor Cyan
            $active=@(Get-NetIPConfiguration | Where-Object {$_.NetAdapter.Status -eq 'Up'})
            if(!$active){Write-Host 'No active adapters.'}
            foreach($adapter in $active){
                Write-Host ($adapter.InterfaceAlias+'   '+($adapter.IPv4Address.IPAddress -join ', '))
                foreach($gateway in $adapter.IPv4DefaultGateway){
                    if(!$gateway){continue}
                    $ping=New-Object Net.NetworkInformation.Ping
                    try{$result=$ping.Send([string]$gateway.NextHop,1200);Write-Host ('Gateway   '+$gateway.NextHop+'   '+$result.Status)}
                    catch{Write-Host ('Gateway   '+$gateway.NextHop+'   No ping reply')}finally{$ping.Dispose()}
                }
            }
            try{$found=Resolve-DnsName example.com -Type A -DnsOnly -QuickTimeout -ErrorAction Stop | Where-Object IPAddress
                if(!$found){throw 'No IPv4 address returned.'};Write-Host ('DNS       example.com   '+(@($found.IPAddress)[0]))}
            catch{Write-Host ('DNS       '+$_.Exception.Message)}
            try{$reply=Invoke-WebRequest -UseBasicParsing -Uri 'https://example.com' -Method Head -TimeoutSec 5 -ErrorAction Stop
                Write-Host ('HTTPS     example.com   '+[int]$reply.StatusCode)}
            catch{Write-Host ('HTTPS     '+$_.Exception.Message)}
        }
    }
}
function Start-NovaMenu {
    $page=if($env:N0VA_PAGE -eq '2'){'2'}else{'1'}
    $pendingKey=$null
    $refreshOwner=$false
    $drawn=''
    $previous=$page
    $cache=@{}
    $typed='';$sequence='';$lastSize=''
    $oldCursor=$true
    try{$oldCursor=[Console]::CursorVisible}catch{}
    try{
        [Console]::CursorVisible=$false
        if($env:N0VA_INTRO -eq '1'){
            try{Send-NovaLaunch}catch{$script:HistoryStatus='Launch could not be shared'}
            [NovaDisplay]::Fullscreen()
            $skipped=[NovaDisplay]::Intro($false,$color)
            if($null -ne $skipped -and $skipped.Key -in @('N','S')){$pendingKey=$skipped}
        }
        while($true){
            $w=[Console]::WindowWidth;$h=[Console]::WindowHeight
            if($w -lt 5 -or $h -lt 2){throw 'Terminal window is too small.'}
            $size="$w/$h"
            if($lastSize -ne $size){
                $cache.Clear()
                foreach($p in '1','2','admin'){$cache[$p]=New-NovaFrame $p $w $h}
                $lastSize=$size
            }
            $frame=$cache[$page]
            $stamp="$size/$page/$typed"
            if($drawn -ne $stamp){
                [Console]::SetCursorPosition(0,[Console]::WindowTop)
                [Console]::Write($frame.Text)
                [Console]::SetCursorPosition($frame.X,[Console]::WindowTop+$frame.Y)
                [Console]::Write($typed)
                $drawn=$stamp
            }
            if($refreshOwner){
                $refreshOwner=$false
                try{[void](Get-NovaOwnerData 'recent' 1)}
                catch{$script:OwnerSummary=$null;$script:HistoryStatus='History unavailable   /   07  Retry'}
                $lastSize='';$drawn='';continue
            }
            while($null -eq $pendingKey -and ![Console]::KeyAvailable){
                if([Console]::WindowWidth -ne $w -or [Console]::WindowHeight -ne $h){break}
                Start-Sleep -Milliseconds 16
            }
            if($null -ne $pendingKey){$key=$pendingKey;$pendingKey=$null}
            elseif([Console]::KeyAvailable){$key=[Console]::ReadKey($true)}
            else{continue}
            $ch=[char]::ToLowerInvariant($key.KeyChar)
            if($page -ne 'admin'){
                $sequence=($sequence+$ch)
                if($sequence.Length -gt 8){$sequence=$sequence.Substring($sequence.Length-8)}
                if($sequence.EndsWith('nnnn')){
                    $pendingKey=$null
    $refreshOwner=$false
    $drawn=''
    $previous=$page;$page='admin';$sequence='';$typed=''
                    $refreshOwner=[IO.File]::Exists($OwnerFile)
                    [void][NovaDisplay]::Intro($true,$color);continue
                }
                if($sequence.EndsWith('seemless')){$number=21}
                elseif($key.Key -in @('A','LeftArrow','D','RightArrow')){
                    $page=if($key.Key -in @('A','LeftArrow')){'1'}else{'2'}
                    $typed='';$sequence='';continue
                }else{$number=-1}
            }else{
                if($key.Key -eq 'Escape'){$page=$previous;$typed='';$sequence='';continue}
                $number=-1
            }
            if($key.Key -eq 'Backspace' -and $typed.Length){$typed=$typed.Substring(0,$typed.Length-1)}
            if($ch -ge '0' -and $ch -le '9' -and $typed.Length -lt 2){$typed+=$ch}
            if($key.Key -eq 'Enter' -and $typed.Length){
                $candidate=[int]$typed
                if($candidate -eq 0 -or ($candidate -eq 25 -and $page -ne 'admin') -or $pages[$page] -match ('^'+$candidate.ToString('00')+' ')){$number=$candidate}
                $typed=''
            }
            if($number -lt 0){continue}
            $drawn='';$typed='';$sequence=''
            if($number -eq 25){
                try{Show-NovaSharing}catch{Write-Host $_.Exception.Message;[void][Console]::ReadKey($true)}
                $lastSize='';continue
            }
            if($page -eq 'admin'){
                if($number -eq 0){$page=$previous;continue}
                if($number -le 3){$number+=21}
                else{
                    try{
                        switch($number){
                            4 {Start-Process eventvwr.msc}
                            5 {Start-Process services.msc}
                            6 {Start-Process taskmgr.exe}
                            7 {Show-NovaHistory 'recent'}
                            8 {Show-NovaHistory 'profiles'}
                            9 {[void](Connect-NovaOwner)}
                            10 {Show-NovaHistory 'builds'}
                        }
                    }catch{Write-Host $_.Exception.Message;[void][Console]::ReadKey($true)}
                    $lastSize='';continue
                }
            }
            if($number -in @(22,23,24)){
                try{Show-NovaDiagnostic $number;if($number -eq 22){continue}}
                catch{Write-Host $_.Exception.Message -ForegroundColor Yellow}
                Write-Host 'Press a key to return.';[void][Console]::ReadKey($true);continue
            }
            if($number -eq 21){
                $desktopFile=Join-Path $env:INSTALL_DIR 'SeemlessOS.ps1'
                if(Test-Path -LiteralPath $desktopFile -PathType Leaf){
                    $source=[IO.File]::ReadAllText($desktopFile)
                    if($source.StartsWith('# N0VA desktop build: 2.2')){
                        try{& ([scriptblock]::Create($source)) -InstallDir $env:INSTALL_DIR}
                        catch{Write-Host $_.Exception.Message;[void][Console]::ReadKey($true)}
                        continue
                    }
                }
                # A distinct return code preserves the second hub page during first-use setup.
                if($page -eq '2'){return 122}
            }
            return $number
        }
    }finally{Complete-NovaLaunch -Wait;[Console]::CursorVisible=$oldCursor}
}
if($env:N0VA_VIEW -eq 'menu'){exit (Start-NovaMenu)}
if($env:N0VA_VIEW -eq 'diagnostic'){Show-NovaDiagnostic ([int]$env:choice)}
if($env:N0VA_VIEW -eq 'sharing'){Show-NovaSharing}
if($env:N0VA_VIEW -eq 'owner'){
    Write-Host 'owner M3NU'
    switch(Read-Host '7 Run history   8 User directory   9 Pair key   10 Builds'){
        '7' {Show-NovaHistory 'recent'} '8' {Show-NovaHistory 'profiles'}
        '9' {[void](Connect-NovaOwner)} '10' {Show-NovaHistory 'builds'}
    }
}
