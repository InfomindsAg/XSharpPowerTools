# Change log

## 2.0

- Created first release based on XSharpPowerTools2019

## 2.1

- BugFix for "Failed to open file" error message

## 2.2

- Fixed Link to Changelog

## 2.3

- Completely reworked Code Browser, now including filtering, user sorting, additional kinds of types and members
- New inserting functionalities for Find Namespace

## 2.11
- Minimum requirement VS 2022 17.12
- Fixed search with ".." for members of partial classes

## 2.17
- Fixed Find Namespace: inserting a using did nothing at all, without any error message
- Existing usings are now also found below a file header (comments, preprocessor directives), and a new using is inserted after that header instead of always at the very top of the file
- Access to the X# database now uses the public XDatabase.Connection property instead of an internal field, which makes the extension less dependent on X# internals

## 2.18
- Fixed Code Browser and Find Namespace doing nothing at all with X# 3.0 and later: X# renamed its model assembly from XSharpModel.dll to XSharp.CodeModel.dll, and the database lookup now resolves the assembly by name as well instead of only searching the already loaded ones
- A missing X# database no longer fails silently with an ArgumentNullException, but reports whether the X# code model is not loaded or has no database open for the current solution

## 2.19
- Unexpected errors are now written to the activity log and to the Extensions pane of the output window. Until now every asynchronous handler used FileAndForget, which reports faults to telemetry only and leaves nothing a developer or a user can look at

## 2.20
- Fixed "The 'XSharpPowerToolsPackage' package did not load correctly" with Visual Studio 18, which left the menu entries in place but without a command behind them: package initialization set a wait cursor before switching to the UI thread, and WPF rejects that on the background thread Visual Studio now loads the package on
- A wait cursor is no longer set at all outside the UI thread, and a failure during package initialization is logged instead of taking down the whole package
- The extension can now be installed on Visual Studio 18

see README.md for further details