; Generic NSIS Installer Script
; Template by Andrew Branscom (Collector)
; Script by Andrew Branscom
; Set "!define RESOURCE_NAME" to your own path (can be the app's build folder)
; Add GPL.TXT and "README.md" renamed to "README.TXT" to files resource path ( ${RESOURCE_NAME} )

; Header ------------------------------------------------------

; Defines -----------------------------------------------------
; Names
!define PRODUCT_NAME "AGILE" ; Installer Name
!define PRODUCT_VERSION "1.1.25" ; Script version of installer
!define PRODUCT_PUBLISHER "The Sierra Help Pages" ; Installer Publisher
!define PRODUCT_DEVELOPER "Lance Ewing" ;Installer Developer
!define PUBLISHER_ACRONYM "SHP" ; Installer Publisher
!define PRODUCT_WEB_SITE "http://www.sierrahelp.com/"
!define PRODUCT_FILE_NAME "AGILE" ; PRODUCT_NAME without Illegal File Name Characters
!define SHORT_NAME "AGILE" ; Common abbreviated app name
!define DOS_NAME "AGILE" ; Name to Conform to 8.3 Naming Convention
!define RESOURCE_NAME "" ; Path to folder for resource files

; Add uninstaller info
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\App Paths\AGILE.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
!define PRODUCT_UNINST_ROOT_KEY "HKLM"
!define PRODUCT_STARTMENU_REGVAL "NSIS:StartMenuDir"
Unicode true
SetCompressor /solid lzma

; Includes
!include "MUI2.nsh"
!include "AddOptions.nsh"
!include "LogicLib.nsh"
!include "Registry.nsh"

; MUI Settings ------------------------------------------------
!define MUI_ABORTWARNING
!define MUI_ICON "${NSISDIR}\Contrib\Graphics\Icons\orange-install.ico" ; Set installer icon
!define MUI_UNICON "${NSISDIR}\Contrib\Graphics\Icons\orange-uninstall.ico" ; Set uninstaller icon


; Pages -------------------------------------------------------
; Welcome page
!define MUI_WELCOMEFINISHPAGE_BITMAP "${RESOURCE_PATH}\${DOS_NAME}.bmp"
!define MUI_WELCOMEPAGE_TITLE_3LINES!insertmacro MUI_PAGE_WELCOME

; License page
!define MUI_LICENSEPAGE_BGCOLOR /gray
!insertmacro MUI_PAGE_LICENSE "${RESOURCE_PATH}\GPL.TXT" ; "${ReadmeFile}"

; Options page
!define MUI_PAGE_CUSTOMFUNCTION_PRE OptionsPre
LangString PAGE_TITLE ${LANG_ENGLISH} "Choose Options"
LangString PAGE_SUBTITLE ${LANG_ENGLISH} "Add Desktop Shortcut or Folder Context Menu option ''Run with Agile''"
Page Custom OptionsSelection OptionsLeave

; Directory page
!insertmacro MUI_PAGE_DIRECTORY

; Start menu page
var ICONS_GROUP
!define MUI_STARTMENUPAGE_NODISABLE
!define MUI_STARTMENUPAGE_DEFAULTFOLDER "${PRODUCT_FILE_NAME}"
!define MUI_STARTMENUPAGE_REGISTRY_ROOT "${PRODUCT_UNINST_ROOT_KEY}"
!define MUI_STARTMENUPAGE_REGISTRY_KEY "${PRODUCT_UNINST_KEY}"
!define MUI_STARTMENUPAGE_REGISTRY_VALUENAME "${PRODUCT_STARTMENU_REGVAL}"
!insertmacro MUI_PAGE_STARTMENU Application $ICONS_GROUP

; Instfiles page
!insertmacro MUI_PAGE_INSTFILES

; Finish page
!define MUI_FINISHPAGE_TITLE "Completing ${PRODUCT_NAME} Wizard."
!define MUI_FINISHPAGE_TITLE_3LINES
!define MUI_FINISHPAGE_RUN "AGILE.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Run ${SHORT_NAME} now"
!define MUI_FINISHPAGE_SHOWREADME "$INSTDIR\README.TXT"
!define MUI_FINISHPAGE_LINK "Visit ${PRODUCT_PUBLISHER}" ; Adds hyperlink to Finish Page
!define MUI_FINISHPAGE_LINK_LOCATION "${PRODUCT_WEB_SITE}"
!define MUI_FINISHPAGE_LINK_COLOR "0000FF"
!insertmacro MUI_PAGE_FINISH

; Uninstaller pages
!insertmacro MUI_UNPAGE_INSTFILES

; Language files
!insertmacro MUI_LANGUAGE "English"
; MUI end -----------------------------------------------------

;Version Information ------------------------------------------
  !define INSTALLER_VERSION "1.0"
  VIProductVersion "1.0.0.0"
  VIAddVersionKey /LANG=${LANG_ENGLISH} "ProductName" "${PRODUCT_NAME} Installer"
  VIAddVersionKey /LANG=${LANG_ENGLISH} "ProductVersion" "1.0.0.0"
  VIAddVersionKey /LANG=${LANG_ENGLISH} "Comments" "${PRODUCT_NAME} installer"
  VIAddVersionKey /LANG=${LANG_ENGLISH} "CompanyName" "${PRODUCT_PUBLISHER}"
  VIAddVersionKey /LANG=${LANG_ENGLISH} "LegalTrademarks" "${PUBLISHER_ACRONYM} is a trademark of ${PRODUCT_PUBLISHER}"
  VIAddVersionKey /LANG=${LANG_ENGLISH} "LegalCopyright" "© ${PRODUCT_PUBLISHER}"
  VIAddVersionKey /LANG=${LANG_ENGLISH} "FileDescription" "AGILE installer."
  VIAddVersionKey /LANG=${LANG_ENGLISH} "FileVersion" "1.0.0"
;--------------------------------------------------------------

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "${BASE_PATH}\${DOS_NAME}Setup.exe"
InstallDir "$PROGRAMFILES\${PRODUCT_FILE_NAME}"
InstallDirRegKey HKLM "${PRODUCT_DIR_RegKey}" ""
ShowInstDetails show
ShowUnInstDetails show
RequestExecutionLevel admin
BrandingText "${PRODUCT_PUBLISHER}"

; Declare Non-MUI Variables -----------------------------------
;var VERSION
; Header End --------------------------------------------------


; Sections ----------------------------------------------------
Section "${PRODUCT_NAME}" SEC01 ;MainSection
  ;SectionIn RO
  SetOverwrite on
  
  ; Begin Game Installation -----------------------------------
  SetOutPath "$INSTDIR"
  
  ; Add Resource Files ----------------------------------------
  File "${RESOURCE_PATH}\GPL.TXT"
  File "${RESOURCE_PATH}\README.TXT"
  
  ; Copy Game Files -------------------------------------------
  SetOutPath "$INSTDIR"
  File "${RESOURCE_PATH}\AGILibrary.dll"
  File "${RESOURCE_PATH}\NAudio.Asio.dll"
  File "${RESOURCE_PATH}\NAudio.Core.dll"
  File "${RESOURCE_PATH}\NAudio.dll"
  File "${RESOURCE_PATH}\NAudio.Midi.dll"
  File "${RESOURCE_PATH}\NAudio.Wasapi.dll"
  File "${RESOURCE_PATH}\NAudio.WinForms.dll"
  File "${RESOURCE_PATH}\NAudio.WinMM.dll"
  File "${RESOURCE_PATH}\AGILE.exe"
  
  ; Add Folder ShellEx
  ${If} $AddShellEx == "1"
    WriteRegStr HKEY_CLASSES_ROOT "Directory\shell\Agile" "" "Run with Agile"
    WriteRegStr HKEY_CLASSES_ROOT "Directory\shell\Agile" "Icon" "$INSTDIR\Agile.exe"
    WriteRegStr HKEY_CLASSES_ROOT "Directory\shell\Agile\command" "" "\$\"$INSTDIR\Agile.exe\$\" \$\"--working-dir\$\" \$\"%v.\$\""
  ${EndIf}
  
  ; Shortcuts -------------------------------------------------
  SetOverwrite on
  SetShellVarContext all
  !insertmacro MUI_STARTMENU_WRITE_BEGIN Application
  CreateDirectory "$SMPROGRAMS\$ICONS_GROUP"
  SetOutPath "$INSTDIR"
    MessageBox MB_OK `$AddShortcut`
  
  ${If} $AddShortcut == "1"
    CreateShortCut "$DESKTOP\${PRODUCT_FILE_NAME}.lnk" "$INSTDIR\AGILE.exe" "" "" "" "" "" "Run ${PRODUCT_NAME}"
  ${EndIf}
  
  CreateShortCut "$SMPROGRAMS\$ICONS_GROUP\${PRODUCT_FILE_NAME}.lnk" "$INSTDIR\AGILE.exe" "" "" "" "" "" "Run ${PRODUCT_NAME}"
  
  ; Additional Shortcuts --------------------------------------
  SetOutPath "$INSTDIR"
  CreateShortCut "$SMPROGRAMS\$ICONS_GROUP\Uninstall ${SHORT_NAME}.lnk" "$INSTDIR\uninst.exe"
  CreateShortCut "$SMPROGRAMS\$ICONS_GROUP\AGILE Help.lnk" "$INSTDIR\AGILE.chm" "" "" "" "" "" "View the Help File"
  CreateShortCut "$SMPROGRAMS\$ICONS_GROUP\README.lnk" "$INSTDIR\README.TXT" "" "" "" "" "" "View the README"
  !insertmacro MUI_STARTMENU_WRITE_END
SectionEnd


; Post Install Section ----------------------------------------
Section -Post
  ; Write Uninstaller -----------------------------------------
  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\uninst.exe"
  
  ; Write EXE Path Registry Entry -----------------------------
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\uninst.exe"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayIcon" "AGILE.exe"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" ""
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "InstallerVersion" "${INSTALLER_VERSION}"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
SectionEnd


; Install Functions -------------------------------------------

; On Successful Completion Functions --------------------------
Function .OnInstSuccess
  ; Open Start Menu on Completion -----------------------------
  ;SetShellVarContext all
  ;Exec 'explorer "$SMPROGRAMS\$ICONS_GROUP"'
FunctionEnd

; Initialization Functions ------------------------------------
Function .onInit
  ; Set Section Sizes -----------------------------------------
  SectionSetSize ${SEC01} "1751122" ; 1.67 MB
FunctionEnd
; Functions End -----------------------------------------------


; Uninstaller -------------------------------------------------
Section Uninstall
  
  ; Delete Files ----------------------------------------------
  Delete "$INSTDIR\AGILE.chm"
  Delete "$INSTDIR\AGILE.dll"
  Delete "$INSTDIR\AGILERes.dll"
  Delete "$INSTDIR\AGILE.exe"
  Delete "$INSTDIR\uninst.exe"
  Delete "$INSTDIR\default_menu.txt"
  Delete "$INSTDIR\GPL.TXT"
  Delete "$INSTDIR\README.TXT"
  
  ; Delete Shortcuts ------------------------------------------
  SetShellVarContext all
  Delete "$DESKTOP\${PRODUCT_FILE_NAME}.lnk"
  
  !insertmacro MUI_STARTMENU_GETFOLDER "Application" $ICONS_GROUP
  Delete "$SMPROGRAMS\$ICONS_GROUP\${PRODUCT_FILE_NAME}.lnk"
  Delete "$SMPROGRAMS\$ICONS_GROUP\Uninstall ${SHORT_NAME}.lnk"
  Delete "$SMPROGRAMS\$ICONS_GROUP\AGILE Help.lnk"
  Delete "$SMPROGRAMS\$ICONS_GROUP\README.lnk"
  
  ; Delete Folders -------------------------------------------
  RMDir /REBOOTOK "$SMPROGRAMS\$ICONS_GROUP"
  RMDir /REBOOTOK "$INSTDIR"
  
  ; Delete Uninstall Registry Key -----------------------------
  DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"
  DeleteRegKey HKLM "${PRODUCT_DIR_RegKey}"
  
  ; Remove Folder ShellEx Key
  DeleteRegKey HKEY_CLASSES_ROOT "Directory\shell\Agile"
  
  SetAutoClose true
SectionEnd

; Uninstall Functions -----------------------------------------

; On Successful Uninstall Completion Functions ----------------
Function un.onUninstSuccess
  HideWindow
  MessageBox MB_ICONINFORMATION|MB_OK `$(^Name) was successfully removed from your computer.`
FunctionEnd

; Uninstaller Initialization Functions ------------------------
Function un.onInit
  MessageBox MB_ICONQUESTION|MB_YESNO|MB_DEFBUTTON2 `Are you sure you want to completely remove $(^Name) and all of its components?` IDYES +2
  Abort
FunctionEnd
; Functions End -----------------------------------------------
