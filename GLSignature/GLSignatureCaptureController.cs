using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using MonoTouch;
using MonoTouch.CoreGraphics;
using MonoTouch.Foundation;
using MonoTouch.GLKit;
using MonoTouch.OpenGLES;
using MonoTouch.UIKit;
using OpenTK;
using OpenTK.Graphics.ES20;
using GLSignature.Util;

namespace GLSignature
{
	[Register("GLSignatureCaptureController")]
	public class GLSignatureCaptureController: GLKViewController
	{
		const float STROKE_WIDTH_MIN = 0.010f; // Stroke width determined by touch velocity
		const float STROKE_WIDTH_MAX = 0.050f;
		const float STROKE_WIDTH_SMOOTHING = 0.5f;   // Low pass filter alpha
		const float VELOCITY_CLAMP_MIN = 20;
		const float VELOCITY_CLAMP_MAX = 5000;
		const float QUADRATIC_DISTANCE_TOLERANCE = 3.0f;   // Minimum distance to make a curve
		const int MAXIMUM_VERTECES = 100000;

		public GLSignatureCaptureController () : base()
		{
		}

		public GLSignatureCaptureController (IntPtr handle) : base(handle)
		{
		}

		EAGLContext context;
		GLKBaseEffect effect;

		//VAO<SignaturePointVector> pointData;
		VAO<SignaturePointVector> lineData;

		// Width of line at current and previous vertex
		float penThickness = 0.010f;
		float previousThickness;

		// Previous points for quadratic bezier computations
		PointF previousPoint;
		PointF previousMidPoint;
		SignaturePointVector previousVertex;

		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				EAGLContext.SetCurrentContext (context);

				if (null != this.lineData) {
					lineData.Dispose ();
					lineData = null;
				}

				if (null != effect) {
					effect.Dispose ();
					effect = null;
				}

				context.Dispose ();
				context = null;
			}

			base.Dispose (disposing);
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();

			context = new EAGLContext (EAGLRenderingAPI.OpenGLES2);

			if (context == null)
				Console.WriteLine ("Failed to create ES context");

			GLKView view;
			if (null == (view = View as GLKView)) {
				Console.WriteLine ("View is not GLKView");
			} else {

				view.Context = context;
				view.DrawableDepthFormat = GLKViewDrawableDepthFormat.Format24;
				view.EnableSetNeedsDisplay = true;

				// Turn on antialiasing
				view.DrawableMultisample = GLKViewDrawableMultisample.Sample4x;

				view.DrawInRect += Draw;
				view.AddGestureRecognizer (new UITapGestureRecognizer (Tap));
				view.AddGestureRecognizer (new UIPanGestureRecognizer (Pan) { 
					MaximumNumberOfTouches = 1, 
					MinimumNumberOfTouches = 1
				});

				setupGL ();
			}
		}

		public void Erase()
		{
			lineData.Clear ();
			this.View.SetNeedsDisplay ();
		}

		public UIImage Signature
		{
			get { 
				GLKView view;
				if (null != (view = View as GLKView)) {
					return view.Snapshot ();
				}
				return null;
			}
		}

		public bool HasSignature
		{
			get { 
				return this.lineData.Length > 0;
			}
		}

		void setupGL ()
		{
			EAGLContext.SetCurrentContext (context);

			effect = new GLKBaseEffect ();
			// Perspective
			effect.Transform.ProjectionMatrix = Matrix4.CreateOrthographicOffCenter (-1, 1, -1, 1, 0.1f, 2.0f);
			effect.Transform.ModelViewMatrix = Matrix4.CreateTranslation(0.0f, 0.0f, -1.0f);

			GL.Disable (EnableCap.DepthTest); // Disable Depth testing

			//pointData = new VAO<SignaturePointVector> (100000);
			lineData = new VAO<SignaturePointVector> (MAXIMUM_VERTECES);

			penThickness = 0.003f;
			previousPoint = new PointF(-100, -100);

		}
			
		public void Draw (object sender, GLKViewDrawEventArgs args)
		{
			GL.ClearColor (1.0f, 1.0f, 1.0f, 1.0f);
			GL.Clear (ClearBufferMask.ColorBufferBit);

			effect.PrepareToDraw ();

			//pointData.Bind ();
			//GL.DrawArrays (BeginMode.TriangleStrip, 0, pointData.Length);

			lineData.Bind ();
			GL.DrawArrays (BeginMode.TriangleStrip, 0, lineData.Length);
		}

		private static readonly Vector3 Black = new Vector3 (0, 0, 0);
		private static readonly Vector3 White = new Vector3 (1, 1, 1);

		Random random = new Random();
		private float GenerateRandom(float start, float end)
		{
			return (random.Next () % 10000 / 10000.0f) * (start - end) + end;
		}

		private void Tap(UITapGestureRecognizer gesture)
		{
			var cords = gesture.LocationInView (View);

			if (UIGestureRecognizerState.Recognized == gesture.State) {
				// Start using the dots buffer as the array buffer

				var touchpoint = SignaturePointVector.FromViewPoint(cords, View.Bounds, White);

				var vertexes = VectorsForTap(touchpoint, 20).ToArray();

				lineData.Add (vertexes);
			}
		}

		static PointF QuadraticPointInCurve(PointF start, PointF end, PointF controlPoint, float percent) {
			float a = (float)Math.Pow((1.0f - percent), 2.0f);
			float b = 2.0f * percent * (1.0f - percent);
			float c = (float)Math.Pow(percent, 2.0f);

			return new PointF(
				a * start.X + b * controlPoint.X + c * end.X,
				a * start.Y + b * controlPoint.Y + c * end.Y
			);
		}

		// Find perpendicular vector from two other vectors to compute triangle strip around line
		static Vector3 perpendicular(SignaturePointVector p1, SignaturePointVector p2) {
			return new Vector3 (
				p2.Location.Y - p1.Location.Y,
				-1 * (p2.Location.X - p1.Location.X),
				0);
		}


		IEnumerable<SignaturePointVector> AddTriangleStripPointsForPrevious(SignaturePointVector previous, SignaturePointVector next )
		{
			float toTravel = penThickness / 2.0f;

			for (int i = 0; i < 2; i++) {
				Vector3 p = perpendicular(previous, next);
				Vector3 p1 = next.Location;
				Vector3 refr = p1 + p;

				float distance = (p1 - refr).Length; //http://www.opentk.com/node/2071
				float difX = p1.X - refr.X;
				float difY = p1.Y - refr.Y;
				float ratio = -1.0f * (toTravel / distance);

				difX = difX * ratio;
				difY = difY * ratio;

				var stripPoint = new SignaturePointVector()
				{
					Location = new Vector3(p1.X + difX, p1.Y +  difY, 0),
					Color = Black,
				};
				yield return stripPoint;
				toTravel *= -1;
			}
		}


		static float Clamp(float min,float max,float value) { return (float)Math.Max(min, Math.Min(max, value)); }

		private void Pan(UIPanGestureRecognizer gesture)
		{
			var v = gesture.VelocityInView (View);
			var l = gesture.LocationInView (View);


			float distance = 0f;
			if (previousPoint.X > 0) {
				distance = (float)Math.Sqrt((l.X - previousPoint.X) * (l.X - previousPoint.X) + (l.Y - previousPoint.Y) * (l.Y - previousPoint.Y));
			}

			float velocityMagnitude = (float)Math.Sqrt(v.X*v.X + v.Y*v.Y);
			float clampedVelocityMagnitude = Clamp(VELOCITY_CLAMP_MIN, VELOCITY_CLAMP_MAX, velocityMagnitude);
			float normalizedVelocity = (clampedVelocityMagnitude - VELOCITY_CLAMP_MIN) / (VELOCITY_CLAMP_MAX - VELOCITY_CLAMP_MIN);

			float lowPassFilterAlpha = STROKE_WIDTH_SMOOTHING;
			float newThickness = (STROKE_WIDTH_MAX - STROKE_WIDTH_MIN) * normalizedVelocity + STROKE_WIDTH_MIN;
			penThickness = penThickness * lowPassFilterAlpha + newThickness * (1 - lowPassFilterAlpha);

			if (gesture.State == UIGestureRecognizerState.Began) {

				previousPoint = l;
				previousMidPoint = l;

				var startPoint = SignaturePointVector.FromViewPoint (l, View.Bounds, White);
				previousVertex = startPoint;
				previousThickness = penThickness;

				lineData.Add (
					startPoint, 
					previousVertex
				);

			} else if (gesture.State == UIGestureRecognizerState.Changed) {
			
				PointF mid = new PointF ((l.X + previousPoint.X) / 2.0f, (l.Y + previousPoint.Y) / 2.0f);

				if (distance > QUADRATIC_DISTANCE_TOLERANCE) {
					// Plot quadratic bezier instead of line
					uint i;

					int segments = (int)(distance / 1.5);

					float startPenThickness = previousThickness;
					float endPenThickness = penThickness;
					previousThickness = penThickness;

					for (i = 0; i < segments; i++) {
						penThickness = startPenThickness + ((endPenThickness - startPenThickness) / segments) * i;

						PointF quadPoint = QuadraticPointInCurve (previousMidPoint, mid, previousPoint, (float)i / (float)(segments));

						var nextv = SignaturePointVector.FromViewPoint (quadPoint, View.Bounds, Black);
						var quads = AddTriangleStripPointsForPrevious (previousVertex, nextv).ToArray ();
						lineData.Add (quads);

						previousVertex = nextv;
					}
				} else if (distance > 1.0) {

					var nextv = SignaturePointVector.FromViewPoint (l, View.Bounds, Black);
					var quads = AddTriangleStripPointsForPrevious (previousVertex, nextv).ToArray ();
					lineData.Add (quads);

					previousVertex = nextv;
					previousThickness = penThickness;
				}

				previousPoint = l;
				previousMidPoint = mid;

			} else if (gesture.State == UIGestureRecognizerState.Ended || gesture.State == UIGestureRecognizerState.Cancelled) {
				var nextv = SignaturePointVector.FromViewPoint (l, View.Bounds, White);
				lineData.Add (nextv);
				previousVertex = nextv;
				lineData.Add (previousVertex);
			}
		}

		private IEnumerable<SignaturePointVector> VectorsForTap(SignaturePointVector touchPoint, int segments)
		{
			// Disconnects from the last point
			yield return touchPoint;

			var centerpoint= new SignaturePointVector()
			{
				Location = touchPoint.Location,
				Color = Black
			};
			var radius = new Vector2( 
				penThickness * 2.0f * GenerateRandom(0.5f, 1.5f), 
				penThickness * 2.0f * GenerateRandom(0.5f, 1.5f));

			yield return centerpoint;

			foreach (var segment in Enumerable.Range(0, segments)) {


				var angle = segment * ((Math.PI * 2) / (segments -1)); // note overdrawing one of the angles

				var edgeVertex = new SignaturePointVector(){
					Color = centerpoint.Color,
					Location = new Vector3(
						centerpoint.Location.X + (radius.X * (float)Math.Cos(angle)), 
						centerpoint.Location.Y + (radius.Y * (float)Math.Sin(angle)),
						0),
				};
				yield return edgeVertex;
				yield return centerpoint;
			}
			yield return touchPoint;
		}
	}
}

