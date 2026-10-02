Unicode true
SetCompressor /SOLID /FINAL lzma

!ifndef APP_VERSION
  !define APP_VERSION "0.1.0"
!endif
!define PUBLISH_PATH "..\publish"

!include "MUI2.nsh"
!include "x64.nsh"

Name "UOTinker ${APP_VERSION}"
OutFile "UOTinker_Setup_${APP_VERSION}.exe"
InstallDir "$LOCALAPPDATA\Programs\UOTinker"
InstallDirRegKey HKCU "Software\UOTinker" "InstallDir"
RequestExecutionLevel user
BrandingText "UOTinker"
CRCCheck on
ShowInstDetails show
ShowUninstDetails show

!define MUI_ICON "Icons\UOTinker.ico"
!define MUI_UNICON "Icons\UOTinker.ico"
!define MUI_ABORTWARNING
!define MUI_LICENSEPAGE_TEXT_TOP "You must agree to this license before installing."
!define MUI_STARTMENUPAGE_REGISTRY_ROOT HKCU
!define MUI_STARTMENUPAGE_REGISTRY_KEY "Software\UOTinker"
!define MUI_STARTMENUPAGE_REGISTRY_VALUENAME "Start Menu"
!define MUI_STARTMENUPAGE_TEXT_CHECKBOX "Do not add Start Menu Folder"
!define MUI_LANGDLL_WINDOWTITLE "UOTinker - Language / Sprache"
!define MUI_FINISHPAGE_RUN "$INSTDIR\UOTinker.exe"

VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey /LANG=1033-English "ProductName" "UOTinker"
VIAddVersionKey /LANG=1033-English "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033-English "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033-English "FileDescription" "UOTinker Setup"
VIAddVersionKey /LANG=1033-English "LegalCopyright" "GPL v3 or later - Daniel Kowarek"

Var StartMenuFolder

!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_STARTMENU Application $StartMenuFolder
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "German"
!insertmacro MUI_RESERVEFILE_LANGDLL

LangString REMOVE_SETTINGS ${LANG_ENGLISH} "Do you also want to remove your UOTinker settings and backups?"
LangString REMOVE_SETTINGS ${LANG_GERMAN} "Sollen auch die Einstellungen und Sicherungen von UOTinker entfernt werden?"

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_OK|MB_ICONSTOP "UOTinker needs 64-bit Windows.$\r$\nUOTinker braucht ein 64-Bit-Windows."
    Abort
  ${EndIf}
  SetRegView 64
  !insertmacro MUI_LANGDLL_DISPLAY
FunctionEnd

Function un.onInit
  SetRegView 64
  !insertmacro MUI_UNGETLANGUAGE
FunctionEnd

Section "UOTinker" SecMain
  SetOutPath "$INSTDIR"
  File "${PUBLISH_PATH}\UOTinker.exe"
  File "..\LICENSE"
  File "..\THIRD-PARTY-NOTICES.md"
  File "..\README.md"

  WriteRegStr HKCU "Software\UOTinker" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\UOTinker" "DisplayName" "UOTinker"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\UOTinker" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\UOTinker" "Publisher" "Daniel Kowarek"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\UOTinker" "DisplayIcon" "$INSTDIR\UOTinker.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\UOTinker" "UninstallString" '"$INSTDIR\UOTinker_uninst.exe"'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\UOTinker" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\UOTinker" "NoRepair" 1

  !insertmacro MUI_STARTMENU_WRITE_BEGIN Application
    CreateDirectory "$SMPROGRAMS\$StartMenuFolder"
    CreateShortCut "$SMPROGRAMS\$StartMenuFolder\UOTinker.lnk" "$INSTDIR\UOTinker.exe"
    CreateShortCut "$SMPROGRAMS\$StartMenuFolder\Uninstall.lnk" "$INSTDIR\UOTinker_uninst.exe"
  !insertmacro MUI_STARTMENU_WRITE_END

  CreateShortCut "$DESKTOP\UOTinker.lnk" "$INSTDIR\UOTinker.exe"
  WriteUninstaller "$INSTDIR\UOTinker_uninst.exe"
SectionEnd

Section "Uninstall"
  Delete "$INSTDIR\UOTinker.exe"
  Delete "$INSTDIR\LICENSE"
  Delete "$INSTDIR\THIRD-PARTY-NOTICES.md"
  Delete "$INSTDIR\README.md"
  Delete "$INSTDIR\UOTinker_uninst.exe"
  RMDir "$INSTDIR"
  Delete "$DESKTOP\UOTinker.lnk"

  !insertmacro MUI_STARTMENU_GETFOLDER Application $StartMenuFolder
  StrCmp $StartMenuFolder "" NO_SHORTCUTS
  Delete "$SMPROGRAMS\$StartMenuFolder\UOTinker.lnk"
  Delete "$SMPROGRAMS\$StartMenuFolder\Uninstall.lnk"
  RMDir "$SMPROGRAMS\$StartMenuFolder"
  NO_SHORTCUTS:

  MessageBox MB_YESNO|MB_ICONQUESTION "$(REMOVE_SETTINGS)" /SD IDNO IDNO SKIP_SETTINGS
    RMDir /r "$APPDATA\UOTinker"
  SKIP_SETTINGS:

  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\UOTinker"
  DeleteRegKey HKCU "Software\UOTinker"
SectionEnd
