using System;

[Flags]
public enum SamplePlatforms
{
	iOS = 1 << 0,
	Android = 1 << 1,
	OSX = 1 << 2,
	WindowsDesktop = 1 << 3,
	Linux = 1 << 4,

	All = iOS | Android | OSX | WindowsDesktop | Linux ,

	AllWindows = WindowsDesktop | Linux,
	AllAndroid = Android,
	AlliOS = iOS ,
	AllApple = iOS  | OSX,
	AllMobile = iOS  | Android,
	AllDesktop = WindowsDesktop | OSX | Linux,
}

