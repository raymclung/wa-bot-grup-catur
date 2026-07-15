' Jalankan launcher resilience (wa-autostart.ps1) TERSEMBUNYI saat login dev8.
' Ditaruh di Startup folder. 0 = window hidden, False = tak menunggu selesai.
CreateObject("WScript.Shell").Run "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""C:\Users\dev8\Documents\wa-bot\service\wa-autostart.ps1""", 0, False
