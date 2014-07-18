Deepfreeze for Windows. 
=======================

This is the official Deepfreeze.io Windows Desktop client application for uploading files to the Deepfreeze.io service.

This repository includes a ```Visual Studio 2013``` solution with three (3) projects:

- ```DeepfreezeApp```: ```WPF``` application.
- ```DeepfreezeSDK```: Deepfreeze API consumer for ```.NET 4.5```.
- ```DeepfreezeModel```: Models for Deepfreeze API objects and for DeepfreezeSDK.

Third party project dependencies
--------------------------------
- ```Caliburn.Micro```
- ```MahApps.Metro```
- ```Json.NET```
- ```Amazon AWS SDK for .NET```
- ```log4net```

Build
-----
The solution has nuget automatic restore on build enabled, so installing the dependencies shouldn't be a problem.
If you want to disable it, then check the ```packages.config``` file for exact versions to install.
