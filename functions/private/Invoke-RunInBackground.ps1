function Invoke-RunInBackground {
    <#
    .SYNOPSIS
        Runs a scriptblock on a background runspace so the WPF UI stays responsive.
    .PARAMETER ScriptBlock
        The code to run. Has access to $sync (already passed in).
    .PARAMETER StatusStart
        Text shown in the status bar while the job is running.
    .PARAMETER StatusDone
        Text shown when the job completes successfully.
    #>
    param(
        [Parameter(Mandatory)][scriptblock]$ScriptBlock,
        [string]$StatusStart = "Running...",
        [string]$StatusDone  = "Done."
    )

    Set-Status $StatusStart "#AAAAAA"

    $rs = [runspacefactory]::CreateRunspace()
    $rs.ApartmentState = "STA"
    $rs.ThreadOptions  = "ReuseThread"
    $rs.Open()
    $rs.SessionStateProxy.SetVariable("sync", $sync)

    $ps = [powershell]::Create().AddScript($ScriptBlock)
    $ps.Runspace = $rs

    $handle = $ps.BeginInvoke()

    # Track runspace for cleanup
    $sync.runspaces.Add(@{ ps = $ps; handle = $handle; rs = $rs })

    # Completion watcher on a timer
    $timer          = New-Object System.Windows.Threading.DispatcherTimer
    $timer.Interval = [timespan]::FromMilliseconds(300)
    $done           = $StatusDone

    $timer.Add_Tick({
        if ($handle.IsCompleted) {
            $timer.Stop()
            try   { $ps.EndInvoke($handle) } catch {}
            $rs.Close()
            $rs.Dispose()
            Set-Status $done "#66BB6A"
        }
    }.GetNewClosure())

    $timer.Start()
}
