Option Explicit

Dim fso, shell, appDir, appExe
Set fso = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")

appDir = fso.BuildPath(fso.GetParentFolderName(WScript.ScriptFullName), "App-net472-Slim")
appExe = fso.BuildPath(appDir, "LumasharpChecker.exe")

If Not fso.FileExists(appExe) Then
    MsgBox "Slim application EXE not found: " & appExe, vbExclamation, "LumasharpChecker Slim"
    WScript.Quit 1
End If

shell.CurrentDirectory = appDir
shell.Run Chr(34) & appExe & Chr(34), 1, False
