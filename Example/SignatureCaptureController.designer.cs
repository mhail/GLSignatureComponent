// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using MonoTouch.Foundation;
using System.CodeDom.Compiler;

namespace Example
{
	[Register ("SignatureCaptureController")]
	partial class SignatureCaptureController
	{
		[Outlet]
		MonoTouch.UIKit.UIButton EraseButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (EraseButton != null) {
				EraseButton.Dispose ();
				EraseButton = null;
			}
		}
	}
}
