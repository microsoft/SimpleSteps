
SimpleSteps
==========

SimpleSteps is a sample application which demonstrates the usage of the Step 
Counter API in Windows Phone 8.1. This application displays a graph of user's 
steps during current day. The user is able to see the steps from the last seven 
days by using the application bar buttons.

1. Instructions
--------------------------------------------------------------------------------

Learn about the Lumia SensorCore SDK from the Lumia Developer's Library. The
example requires the Lumia SensorCore SDK's NuGet package but will retrieve it
automatically (if missing) on first build.

To build the application you need to have Windows 8.1 and Windows Phone SDK 8.1
installed.

Using the Windows Phone 8.1 SDK:

1. Open the SLN file: File > Open Project, select the file `SimpleSteps.sln`
2. Remove the "AnyCPU" configuration (not supported by the Lumia SensorCore SDK)
or simply select ARM
3. Select the target 'Device'.
4. Press F5 to build the project and run it on the device.

Please see the official documentation for
deploying and testing applications on Windows Phone devices:
http://msdn.microsoft.com/en-us/library/gg588378%28v=vs.92%29.aspx


2. Implementation
--------------------------------------------------------------------------------

The core functionality is in the MainPage.xaml.cs file, which demonstrates the usage of
Step Counter API that is initialized (if supported).

The API is called through the CallSenseApiAsync() helper function to handle the errors 
when the required features are disabled in the system settings.
Initialize() function contains the compatibility check for devices that have different
sensorCore SDK service.

LoadDaySteps() is a main function that loads steps for the selected day and updates the 
screen with the correct data by calling UpdateScreen() method. 
It will be displayed a graph of user's steps during current day or from the last 
seven days, by using the application bar buttons.
	
3. Version history
--------------------------------------------------------------------------------
* Version 1.1.0.0: The first release.

4. Downloads
---------

| Project | Release | Download |
| ------- | --------| -------- |
| SimpleSteps | v1.1.0.0 | [simplesteps-1.1.0.0.zip](https://github.com/Microsoft/SimpleSteps/archive/v1.1.0.0.zip) |

5. See also
--------------------------------------------------------------------------------

The projects listed below are exemplifying the usage of the SensorCore APIs

* SimpleActivity -  https://github.com/Microsoft/SimpleActivity
* SimplePlaces - https://github.com/Microsoft/SimplePlaces
* SimpleTracks - https://github.com/Microsoft/SimpleTracks
