using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace Example
{
	// The UIApplicationDelegate for the application. This class is responsible for launching the
	// User Interface of the application, as well as listening (and optionally responding) to
	// application events from iOS.
	[Register ("AppDelegate")]
	public partial class AppDelegate : UIApplicationDelegate
	{
		// class-level declarations
		UIWindow window;
		//MonoTouch.GLKit.GLKViewController vc;
		//GLSignature.GLSignatureCaptureController controller;
		//
		// This method is invoked when the application has loaded and is ready to run. In this
		// method you should instantiate the window, load the UI into it and then make the window
		// visible.
		//
		// You have 17 seconds to return from this method, or iOS will terminate your application.
		//
		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			// create a new window instance based on the screen size
			window = new UIWindow (UIScreen.MainScreen.Bounds);

			//vc = new MonoTouch.GLKit.GLKViewController ();
			//vc.View = new GLSignature.GLSignatureView ();
			//controller = ;
			// If you have defined a root view controller, set it here:
			window.RootViewController = UIStoryboard.FromName ("SignatureCapture", null).InstantiateInitialViewController () as UIViewController;
			
			// make the window visible
			window.MakeKeyAndVisible ();
			
			return true;
		}

		public override UIInterfaceOrientationMask GetSupportedInterfaceOrientations (UIApplication application, UIWindow forWindow)
		{
			return UIInterfaceOrientationMask.Portrait | UIInterfaceOrientationMask.LandscapeLeft;
		}
	}
}

