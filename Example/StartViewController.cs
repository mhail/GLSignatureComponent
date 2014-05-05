// This file has been autogenerated from a class added in the UI designer.

using System;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace Example
{
	public partial class StartViewController : UIViewController
	{
		SignatureCaptureController _capture;
		public StartViewController (IntPtr handle) : base (handle)
		{
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();

			CapturedImage.Layer.BorderColor = UIColor.Black.CGColor;
			CapturedImage.Layer.BorderWidth = 2;
		}

		public override void DidRotate (MonoTouch.UIKit.UIInterfaceOrientation fromInterfaceOrientation)
		{
			base.DidRotate (fromInterfaceOrientation);

			if (IsInLandscapeOrentation()) {
				if (null == _capture) {
					_capture = UIStoryboard.FromName ("SignatureCapture", null).InstantiateViewController("SignatureCaptureController") as SignatureCaptureController;
					_capture.SignatureCaptured += this.SignatureCaptured;
				}
				this.PresentViewController (_capture, true, null);
			} 
		}

		public override UIInterfaceOrientationMask GetSupportedInterfaceOrientations ()
		{
			return UIInterfaceOrientationMask.Portrait | UIInterfaceOrientationMask.LandscapeLeft;
		}

		protected bool IsInLandscapeOrentation()
		{
			return this.InterfaceOrientation == UIInterfaceOrientation.LandscapeLeft 
				|| this.InterfaceOrientation == UIInterfaceOrientation.LandscapeRight;
		}

		protected void SignatureCaptured(object sender, SignatureCaptureEventArgs e)
		{
			if (e.HasSignature) {
				this.CapturedImage.Image = e.Signature;
			}
		}
	}
}
