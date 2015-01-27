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
- ```MahApps.Metro.Resources```
- ```Json.NET```
- ```Amazon AWS SDK for .NET```
- ```log4net```
- ```WPF instance aware application```
- ```Hardcodet.Wpf.TaskbarNotification```

Build
-----
The solution has nuget automatic restore on build enabled, so installing the dependencies shouldn't be a problem.
If you want to disable it, then check the ```packages.config``` file for exact versions to install.

~~Important information about mandatory updates~~
---------------------------------------------
~~Always update the minimum version in the updates settings page (in project ```Properties```). Not only because all clients need to receive the update and disable the users to bypass it, but also because if not, when a user tries to uninstall the app from the Programs and Features window, then a choice is given to restore to the previous version. That is generally not desirable, especially if there are changes in the underlying structure of the client (for example, with the ```BigStash``` update (version ```1.2.0.0```), the old ```Deepfreeze.io``` application data folder is removed after the migration completes. If a user could restore to the previous version, that is to downgrade ```BigStash``` to ```Deepfreeze.io```, then she would have lost all existing uploads).~~

Deployment and releases
-----------------------
The application is now released (beginning with version [1.4.0.0](https://github.com/longaccess/deepfreeze-windows-app/tree/1.4.0.0)) using [Squirrel](https://github.com/Squirrel/Squirrel.Windows). See [instructions](https://github.com/longaccess/deepfreeze-windows-app/blob/master/docs/Squirrel%20Instructions.MD) for more information.
