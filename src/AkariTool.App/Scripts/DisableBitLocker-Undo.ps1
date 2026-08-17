# BitLocker - Re-enable
# Undo script for DisableBitLocker.ps1
# Note: requires a TPM chip or a recovery key to enable BitLocker.

Enable-BitLocker -MountPoint $Env:SystemDrive -ErrorAction SilentlyContinue
Write-Host "BitLocker re-enabled on $Env:SystemDrive."