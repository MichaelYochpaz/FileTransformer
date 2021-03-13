<p align="center">
  <a href="#"><img src="Resources/logo.svg" width="192" a="https://github.com/MichaelYochpaz/FileTransformer"></a>
</p>

# FileTransformer

A tool for converting files to text based files with an option for encrypting, and an option to restore the original file using the converted file.
The tool uses Base64 to store file data as text, and Hex for file storing header and footer data.

Latest version: [1.0.0](https://github.com/MichaelYochpaz/FileTransformer/releases/tag/v1.0.0)



## Features
* Generate a text based (Base64) file from any type of file
* Encrypt the data using an encryption key (encryption is done using XOR)
* Embed original file's name within transformed file and give the transformed file a random name (can be changed to any name)
* Embed original file's SHA256 hash in the transformed file and use it to check file integrity when restoring the original file
* Delete source file after conversion

##
</br>
</br>
<p align="center">
  <a href="#"><img src="https://user-images.githubusercontent.com/8832013/111038622-ae27b080-8432-11eb-829f-78306f51cc27.gif" height="400" a="#"></a>
</p>
