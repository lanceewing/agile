; AGILE Installer Options Page
; Script by Andrew Branscom (Collector)
; Add to NSIS' Include folder

; Includes
!include "LogicLib.nsh"
!include "WinMessages.nsh"

; Declare Variables -------------------------------------------
var AddShortcut
var AddShellEx
var Title
var SubTitle
; Header End --------------------------------------------------


; Functions ---------------------------------------------------
Function OptionsSelection
  ; Add CD Selection Page Title & Subtitle --------------------
  !insertmacro MUI_HEADER_TEXT_PAGE $(PAGE_TITLE) $(PAGE_SUBTITLE)
  
  ; Create Options Selection Page -----------------------------
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Settings" "NumFields" "4"
  
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 1" "Type" "Checkbox"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 1" "Text" "Add AGILE Destop Shortcut"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 1" "Left" "8"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 1" "Right" "177"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 1" "Top" "8"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 1" "Bottom" "17"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 1" "State" "$R1"
  
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 2" "Type" "Label"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 2" "Text" "Adds an AGILE shortcut to your Desktop."
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 2" "Left" "20"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 2" "Right" "145"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 2" "Top" "22"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 2" "Bottom" "30"
  
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 3" "Type" "Checkbox"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 3" "Text" "Explorer Shell Integration"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 3" "Left" "8"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 3" "Right" "177"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 3" "Top" "42"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 3" "Bottom" "51"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 3" "State" "$R2"
  
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 4" "Type" "Label"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 4" "Text" "Adds a ''Run with Agile'' folder context menu item."
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 4" "Left" "20"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 4" "Right" "175"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 4" "Top" "56"
  WriteIniStr "$PLUGINSDIR\custom2.ini" "Field 4" "Bottom" "64"
  
  push $0
  InstallOptions::Dialog "$PLUGINSDIR\custom2.ini"
  pop $0
  pop $0
  /*
  push $1
  InstallOptions::Dialog "$PLUGINSDIR\custom2.ini"
  pop $1
  pop $1/**/
FunctionEnd

Function OptionsLeave
  ReadIniStr $0 "$PLUGINSDIR\custom2.ini" "Settings" "State"
  
  ; Get shortcut selection ------------------------------------
  StrCmp $0 "2" 0 next1
  ReadIniStr $0 "$PLUGINSDIR\custom2.ini" "Field 1" "State"
  StrCpy $0 $0 3
  Abort
  next1:
  ReadIniStr $0 "$PLUGINSDIR\custom2.ini" "Field 1" "State"
  
  ; Assign chosen ---------------------------------------------
  StrCpy $AddShortcut $0
  
  ; Get ShellSx selection -------------------------------------
  StrCmp $1 "2" 0 next2
  ReadIniStr $1 "$PLUGINSDIR\custom2.ini" "Field 3" "State"
  StrCpy $1 $1 3
  Abort
  next2:
  ReadIniStr $1 "$PLUGINSDIR\custom2.ini" "Field 3" "State"
  
  ; Assign chosen ---------------------------------------------
  StrCpy $AddShellEx $1
FunctionEnd

Function OptionsPre
  StrCpy $Title "Choose Options"
  StrCpy $SubTitle "Shortcuts & Explorer Shell Integration"
FunctionEnd

