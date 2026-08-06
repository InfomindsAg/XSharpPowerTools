# Change log

## 1.0

- Code Browser
- Find Namespaces

## 1.1

- Bug fixes and minor adjustments

## 1.2

- Improved Visual Studio integration
- UI tweaks and bug fixes

## 1.3

- Find Namespace tool searching in local sourcecode
- User feedback when no results are found
- Bug fixes

## 1.4

- Find Namespace tool bug fix

## 1.5

- BugFix for crash when searching in current document (.. prefix)

## 1.6 

- Completely reworked Code Browser, now including filtering, user sorting, additional kinds of types and members
- New inserting functionalities for Find Namespace

## 1.14
- Fixed search with ".." for members of partial classes

## 1.17
- Fixed Find Namespace: inserting a using did nothing at all, without any error message
- Existing usings are now also found below a file header (comments, preprocessor directives), and a new using is inserted after that header instead of always at the very top of the file
- Access to the X# database now uses the public XDatabase.Connection property instead of an internal field, which makes the extension less dependent on X# internals

see README.md for further details