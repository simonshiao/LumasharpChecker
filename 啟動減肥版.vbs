Option Explicit

Dim fso, shell, rootDir, appDir, appExe
Set fso = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")

rootDir = fso.GetParentFolderName(WScript.ScriptFullName)
appDir = fso.BuildPath(rootDir, "TestArtifacts\_luma_native_location")
appExe = fso.BuildPath(appDir, "LumasharpChecker.exe")

' Prefer the current Luma development build, which includes the native
' InspTools locator runtime. Fall back to the portable Slim release if absent.
If Not fso.FileExists(appExe) Then
    appDir = fso.BuildPath(rootDir, "App-net472-Slim")
    appExe = fso.BuildPath(appDir, "LumasharpChecker.exe")
End If

If Not fso.FileExists(appExe) Then
    MsgBox "LumasharpChecker Slim application EXE not found: " & appExe, vbExclamation, "LumasharpChecker Slim"
    WScript.Quit 1
End If

shell.CurrentDirectory = appDir
shell.Run Chr(34) & appExe & Chr(34), 1, False
