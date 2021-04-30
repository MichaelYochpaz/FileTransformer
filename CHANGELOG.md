# Changelog
All notable changes to the script will be documented here.
<br>

## 2.0.0 - [2021-04-12]
* Migrated code to .NET 5.0
* Design overhaul.
* Added a top menu bar with an option to check for updates and an "About" section.
* Added an option to select multiple files at once.
* Added an option to choose a custom extension for transformed files and removed previous random extension.
* Switched encryption method from plain XOR to the far more secure AES-GCM algorithm.
* Encrypting transformed files is no longer optional (using a default encryption key in case no key is chosen).
* File hash verification (and with that the file footer) were removed, now using AES-GCM's authentication instead.
* Added a file signature for transformed files to easily identify whether chosen files are valid.
* Code optimization and better error handling.

### BUGFIXES:
* Trying to restore an invalid file / using a wrong decryption key causes the program to crash.
* Trying to read a file that's already open by a different program / service causes the program to crash.
<br>

## 1.0.0 - [2021-03-13]
* Initial release.
