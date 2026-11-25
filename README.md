# CornerGrab for Windows

[**Download CornerGrab.exe**](https://github.com/perlytiara/cornergrab/releases/latest/download/CornerGrab.exe) · [Release notes and SHA-256](https://github.com/perlytiara/cornergrab/releases/latest)

1. Download **CornerGrab.exe** to a folder you want to keep. Double-click it. Your first selection opens immediately.
2. Drag a rectangle and release. CornerGrab saves the PNG and copies the image. Press **Esc** to cancel.
3. **Drag the bottom-right thumbnail into a Discord chat** to attach the file, or press **Ctrl+V** in an app that accepts images. Press **Ctrl+Shift+4** to grab another.

### Where screenshots go

The default is **Pictures → CornerGrab**. Click the thumbnail for **Save as…** or **Copy image**. Right-click the CornerGrab icon near the Windows clock for **Choose default folder…**, **Show latest thumbnail**, or **Start at sign-in**. Hover over the thumbnail to keep it on screen. If it disappears, the PNG is still in your folder.

### Print Screen

**Print Screen** also starts region selection when the shortcut is available. If the Windows Snipping Tool opens, go to **Settings → Accessibility → Keyboard**, turn off **Use the Print Screen key to open screen capture**, and reopen CornerGrab’s tray menu. You can also start a capture by clicking the tray icon. [Microsoft’s keyboard guide](https://support.microsoft.com/en-gb/accessibility/windows/keyboard-shortcuts-in-windows)

### Discord Whiteboard

To put the image on the **board**, click its **Image +** toolbar button. For a fast file-picker shortcut, first click the CornerGrab thumbnail and choose **Copy file path for Whiteboard**. Paste into **File name** in the picker and click **Open**. [Discord’s Whiteboard guide](https://support-apps.discord.com/hc/en-us/articles/26502097657879-Jamspace-Whiteboard-FAQ)

### Requirements and build

A Windows 10 or 11 desktop with .NET Framework 4.8 or later. The download is one portable, unsigned executable. Windows may display an unknown-publisher prompt. CornerGrab runs as your user and stores captures locally.

Developers can build with Windows PowerShell using `./scripts/build-windows.ps1`. On an unlocked Windows desktop, quit CornerGrab and run `./scripts/test-windows.ps1` to exercise region selection, exact image pixels, clipboard formats, and dragging a real file between processes. The test temporarily uses a private capture folder and replaces the current clipboard image.
