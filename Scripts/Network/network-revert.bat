@echo off
Echo "Reverting Network Tweaks"
netsh winsock reset
cls
Echo "Network Tweaks are Reverted"
Echo "A reboot is required"
Echo "Press any key to reboot"
pause
shutdown -r -t 01
exit