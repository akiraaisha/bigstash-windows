In order to release a new squirrel deployed version of the application, one can follow the below instructions:

- Update the ```AssemblyVersion string``` in ```Properties\AssemblyInfo.cs``` to the release version number.
- Build the project and use ```BigStashWindows<version>.nuspec``` file to create a ```nuget``` package.
- Before saving the package, make sure to update it's information by changing the nuspec properties, either by editing the file or by using ```NuGet Package Explorer```.
- Make sure to non include any points ```.``` in the package's name. This will be supported in a future version of ```Squirrel```.
- Using the ```Package Manager Console``` inside ```Visual Studio``` run the below command to create a release:
	- ```Squirrel --releasify <path_to_nuget_package> -i <path_to_application_icon> -r <path_to_release_folder> -n "/a /f our_company_certificate.pfx /p our_certificate_password /t http://timestamp.verisign.com/scripts/timstamp.dll"```
	- This command creates a release from the package placed in ```<path_to_nuget_package>```. With ```releasify``` option ```Squirrel``` creates 3 files, ```Setup.exe```, ```RELEASES``` and ```BigStashWindows.<version>.nupkg``` and saves them in ```<path_to_release_folder>``` utilizing the ```-r``` option.
	- ```-i``` sets the icon of the ```Setup.exe``` file from the path ```<path_to_application_icon>``` given as an argument.
	- ```-n``` option signs the executables using ```signtool``` and the given arguments.


**Important**

Before creating a new release, make sure to create a backup of the releases folder, because the hash in ```RELEASES``` file will change. You don't want this messed up by subsequent releases with the same version number when developing for testing.