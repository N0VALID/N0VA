$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
$stage=Join-Path $env:TEMP ('nova-check-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($stage)
try {
    $source=Get-Content -Raw (Join-Path $repo 'N0VA.cmd')
    $body=$source.Substring($source.LastIndexOf('# N0VA_STARTUP_PS')+'# N0VA_STARTUP_PS'.Length)
    [void][scriptblock]::Create($body)
    . (Join-Path $repo 'SeemlessOS.ps1') -InstallDir $stage -NoRun
    foreach($tool in 'eventvwr.msc','services.msc','taskmgr.exe','explorer.exe','cleanmgr.exe'){
        if(!(Test-Path (Join-Path "$env:WINDIR\System32" $tool)) -and !(Get-Command $tool -ErrorAction SilentlyContinue)){throw "Missing $tool"}
    }
    if(!(Test-Path 'Registry::HKEY_CLASSES_ROOT\ms-settings')){throw 'Windows Settings protocol missing'}
    foreach($file in 'exe_compiler.pyw','cmptr_info.pyw','Python_Command_Explorer_Pro.pyw'){
        $proc=Start-Process python -ArgumentList ('"'+(Join-Path $repo $file)+'"') -PassThru
        try {
            $limit=(Get-Date).AddSeconds(18)
            do{Start-Sleep -Milliseconds 250;$proc.Refresh()}while(!$proc.HasExited -and !$proc.MainWindowHandle -and (Get-Date) -lt $limit)
            if($proc.HasExited -or !$proc.MainWindowHandle){throw "$file did not open a window"}
        }finally{if(!$proc.HasExited){$proc.Kill()};$proc.Dispose()}
    }
    $installer=Join-Path $stage 'SeemlessBrowser-Setup.cmd'
    Save-SeemlessInstaller $installer (Join-Path $repo 'SeemlessOS.ps1')
    cmd.exe /d /c "call `"$installer`" <nul"
    if($LASTEXITCODE){throw 'Standalone installer failed'}
    $lnk=Join-Path ([Environment]::GetFolderPath('Programs')) 'Seemless Browser.lnk'
    if(!(Test-Path $lnk)){throw 'Missing Start shortcut'}
    $installed=Join-Path $env:LOCALAPPDATA 'N0VA\Standalone\SeemlessOS.ps1'
    if((Get-FileHash $installed).Hash -ne (Get-FileHash (Join-Path $repo 'SeemlessOS.ps1')).Hash){throw 'Installed source mismatch'}

    Add-Type -AssemblyName System.Windows.Forms
    $script:passed=$false
    $script:loading=$false
    $script:failure=''
    $timer=New-Object Windows.Forms.Timer
    $timer.Interval=250
    $limit=(Get-Date).AddMinutes(3)
    $timer.Add_Tick({
        $form=[Windows.Forms.Application]::OpenForms|Where-Object {$_.GetType().Name -eq 'SeemlessBrowser'}|Select-Object -First 1
        if(!$form){return}
        try{
            if((Get-Date) -gt $limit){throw 'Browser initialization timed out'}
            $tabs=$form.Controls|Where-Object {$_ -is [Windows.Forms.TabControl]}|Select-Object -First 1
            $tab=$tabs.SelectedTab
            if(!$tab){return}
            if($tab.Text -eq 'Unavailable'){throw $tab.Controls[0].Text}
            if(!$tab.Ready){return}
            $core=$tab.View.CoreWebView2
            if(!$script:loading){$script:loading=$true;$core.NavigateToString('<title>N0VA smoke test</title>');return}
            if($core.DocumentTitle -ne 'N0VA smoke test'){return}
            if($core.Profile.PreferredTrackingPreventionLevel -ne 'Strict'){throw 'Strict tracking prevention missing'}
            $buttons=($form.Controls|Where-Object {$_.GetType().Name -eq 'ToolStrip'}).Items
            ($buttons|Where-Object Text -eq 'Settings').PerformClick()
            if($tabs.SelectedTab.Text -ne 'Settings'){throw 'Settings did not open'}
            $script:passed=$true;$timer.Stop();$form.Close()
        }catch{$script:failure=$_.Exception.Message;$timer.Stop();$form.Close()}
    })
    $BrowserOnly=$true
    $timer.Start()
    try {Start-SeemlessBrowser} finally {$timer.Stop();$timer.Dispose()}
    if(!$script:passed){throw ("WebView2 check failed. "+$script:failure)}
    Write-Host 'Windows tools, Python GUIs, installer, WebView2, blocker initialization and Settings passed.'
}finally{if(Test-Path $stage){Remove-Item -LiteralPath $stage -Recurse -Force}}
