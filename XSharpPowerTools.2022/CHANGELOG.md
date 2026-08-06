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

see README.md for further details