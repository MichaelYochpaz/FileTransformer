<p align="center">
  <a href="#"><img src="Resources/logo.svg" width="128"></a>
</p>

# FileTransformer
A tool for converting files to and from encrypted Base64 text-based files.  
Latest version: [2.0.0](https://github.com/MichaelYochpaz/FileTransformer/releases/latest) ([changelog](https://github.com/MichaelYochpaz/FileTransformer/blob/main/CHANGELOG.md))

<p align="center">
  <a href="https://github.com/MichaelYochpaz/FileTransformer/releases/latest">
    <img alt="Release" src="https://img.shields.io/github/v/release/MichaelYochpaz/FileTransformer">
  </a>
  <a href="https://github.com/MichaelYochpaz/FileTransformer/blob/master/LICENSE.md">
    <img alt="License" src="https://img.shields.io/github/license/MichaelYochpaz/FileTransformer">
  </a>
  <a href="https://github.com/MichaelYochpaz/FileTransformer/issues">
    <img alt="GitHub Issues" src="https://img.shields.io/github/issues/MichaelYochpaz/FileTransformer?style=flat-square&logo=github&logoColor=white">
  </a>
  <a href="https://github.com/MichaelYochpaz/FileTransformer/releases">
    <img alt="Downloads" src="https://img.shields.io/github/downloads/MichaelYochpaz/FileTransformer/total">
  </a>
</p>
</br>

## Features
* Convert any file to and from an encrypted text based (using Base64) file.
* Data is encrypted using AES-GCM before Base64 conversion.
* Data integrity is insured as part of the AES-GCM algorithm. Trying to restore a file that was altered will fail.
* Original filename is embedded within the transformed file, so filename of the transformed file can be altered without affecting the original filename.
* An option to automatically delete source files after conversion.
</br>

## Transformed File Structure
<p align="center">
  <a href="#"><img src="Resources/file-structure.png"></a>
</p>
</br>

## Screenshots
</br>
</br>
<p align="center">
  <a href="#"><img src="Resources/screenshot-1.png" alt="Main window" width=300></a>
  </br>
  <a href="#"><img src="Resources/screenshot-2.png" alt="Transform example"width=300></a>
  <a href="#"><img src="Resources/screenshot-3.png" alt="Restore example"width=300></a>
  </br>
  <a href="#"><img src="Resources/screenshot-HxD.png" alt="HxD hex preview of a transformed file"width=600></a>
</p>
