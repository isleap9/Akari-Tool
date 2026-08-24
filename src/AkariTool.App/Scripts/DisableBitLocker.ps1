# BitLocker - Disable
# https://winutil.christitus.com/dev/tweaks/essential-tweaks/disablebitlocker/
# Disables BitLocker encryption on the system drive (C:).
# Note: decryption happens in the background and may take a while to complete.

$status = Get-BitLockerVolume -MountPoint $Env:SystemDrive -ErrorAction SilentlyContinue

If ($status -and $status.ProtectionStatus -eq "On") {
    Disable-BitLocker -MountPoint $Env:SystemDrive
    Write-Host "BitLocker is being disabled on $Env:SystemDrive. Decryption may take some time."
} Else {
    Write-Host "BitLocker is not enabled on $Env:SystemDrive. Nothing to do."
}