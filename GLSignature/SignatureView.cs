using System;
using MonoTouch.UIKit;
using OpenTK;
using MonoTouch.OpenGLES;
using MonoTouch.GLKit;
using MonoTouch;
using OpenTK.Graphics.ES20;
using MonoTouch.CoreGraphics;
using System.Drawing;
using MonoTouch.Foundation;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GLSignature
{
	// https://github.com/jharwig/PPSSignatureView/blob/master/Source/PPSSignatureView.m
	[Register("GLSignatureView")]
	public class GLSignatureView : GLKView
	{

		const float STROKE_WIDTH_MIN = 0.002f; // Stroke width determined by touch velocity
		const float STROKE_WIDTH_MAX = 0.010f;
		const float STROKE_WIDTH_SMOOTHING = 0.5f;   // Low pass filter alpha
		const float VELOCITY_CLAMP_MIN = 20;
		const float VELOCITY_CLAMP_MAX = 5000;
		const float QUADRATIC_DISTANCE_TOLERANCE = 3.0f;   // Minimum distance to make a curve
		const int MAXIMUM_VERTECES = 100000;
		//
		static Vector3 StrokeColor = new Vector3( 0, 0, 0 );

		[StructLayout(LayoutKind.Sequential)]
		struct PPSSignaturePoint
		{
			public Vector3		Vertex;
			public Vector3		Color;
		};

		static int maxLength = MAXIMUM_VERTECES;

		EAGLContext context;
		GLKBaseEffect effect;

		// GL Vertex Array Object Handles and Buffer Handles, Note: need to be destroyed
		uint vertexArray;
		uint vertexBuffer;
		uint dotsArray;
		uint dotsBuffer;

		private PPSSignaturePoint[] SignatureVertexData = new PPSSignaturePoint[maxLength];
		int length;

		private PPSSignaturePoint[] SignatureDotsData = new PPSSignaturePoint[maxLength];
		int dotsLength;

		// Width of line at current and previous vertex
		float penThickness;
		float previousThickness;


		// Previous points for quadratic bezier computations
		PointF previousPoint;
		PointF previousMidPoint;
		PPSSignaturePoint previousVertex;
		PPSSignaturePoint currentVelocity;

		private void CommonInit()
		{
			context = new EAGLContext (EAGLRenderingAPI.OpenGLES2);
			if (null != context) {
				this.Context = context;
				this.DrawableDepthFormat = GLKViewDrawableDepthFormat.Format24;
				this.EnableSetNeedsDisplay = true;

				// Turn on antialiasing
				this.DrawableMultisample = GLKViewDrawableMultisample.Sample4x;
				this.SetupGL ();

				// Capture touches
				AddGestureRecognizer (_pan = new UIPanGestureRecognizer (Pan) {
					MaximumNumberOfTouches = 1,
					MinimumNumberOfTouches = 1,
				});

				// For dotting your i's
				AddGestureRecognizer (_tap = new UITapGestureRecognizer (Tap));

				// Erase with long press
				AddGestureRecognizer (_press = new UILongPressGestureRecognizer (Press));
			} else {
				throw new Exception ("Failed to create OpenGL ES2 context");
			}
		}


		static PPSSignaturePoint ViewPointToGL(PointF viewPoint, RectangleF bounds, Vector3 color) {

			return new PPSSignaturePoint() {
				Vertex = new Vector3(
					(viewPoint.X / bounds.Size.Width * 2.0f - 1),
					((viewPoint.Y / bounds.Size.Height) * 2.0f - 1) * -1,
					0
				),
				Color = color
			};
		}

		static PointF QuadraticPointInCurve(PointF start, PointF end, PointF controlPoint, float percent) {
			float a = (float)Math.Pow((1.0f - percent), 2.0f);
			float b = 2.0f * percent * (1.0f - percent);
			float c = (float)Math.Pow(percent, 2.0f);

			return new PointF(
				a * start.X + b * controlPoint.X + c * end.X,
				a * start.X + b * controlPoint.Y + c * end.Y
			);
		}

		void AddVertex(ref int length, PPSSignaturePoint v)
		{
			if (length >= maxLength) {
				return;
			}
			try {
				var data = GL.Oes.MapBuffer (All.ArrayBuffer, All.WriteOnlyOes);
				if (data != IntPtr.Zero) {
					var offset = Marshal.SizeOf<PPSSignaturePoint> () * length;
					Marshal.StructureToPtr (v, IntPtr.Add( data, offset) , false);
					length++;
				}
			} finally{
				GL.Oes.UnmapBuffer (All.ArrayBuffer);
			}

		}

		private UIPanGestureRecognizer _pan;
		private UITapGestureRecognizer _tap;
		private UILongPressGestureRecognizer _press;

		private GLKBaseEffect _effect;
		private List<Vector3> _points;

		public GLSignatureView () : base ()
		{
			CommonInit ();
		}

		public GLSignatureView(IntPtr handle) : base (handle)
		{
			CommonInit ();
		}

		public GLSignatureView(NSCoder coder) : base (coder)
		{
			CommonInit ();
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				this.TearDownGL ();
				if (EAGLContext.CurrentContext == context) {
					EAGLContext.SetCurrentContext (null);
				}
			}
			base.Dispose (disposing);
		}



		private void BindShaderAttributes()
		{
			GL.EnableVertexAttribArray ((int)GLKVertexAttrib.Position); //GLKVertexAttribPosition
			GL.VertexAttribPointer (0, 3, VertexAttribPointerType.Float, false, Marshal.SizeOf<PPSSignaturePoint>(), 0);
			GL.EnableVertexAttribArray ((int)GLKVertexAttrib.Color); //GLKVertexAttribColor
			GL.VertexAttribPointer (2, 3, VertexAttribPointerType.Float, false, 6 * Marshal.SizeOf<float>(), 12);
			/*
			glEnableVertexAttribArray(GLKVertexAttribPosition);
    		glVertexAttribPointer(GLKVertexAttribPosition, 3, GL_FLOAT, GL_FALSE, sizeof(PPSSignaturePoint), 0);
    		glEnableVertexAttribArray(GLKVertexAttribColor);
    		glVertexAttribPointer(GLKVertexAttribColor, 3, GL_FLOAT, GL_FALSE,  6 * sizeof(GLfloat), (char *)12);
			 */ 
		}

		private void SetupGL()
		{
			EAGLContext.SetCurrentContext (context);
			_effect = new GLKBaseEffect (); // Create a default effect
			GL.Disable (EnableCap.DepthTest); // Disable Depth testing

			//GCHandle handle = GCHandle.Alloc( rawdatas, GCHandleType.Pinned );
			// Signature Lines, //http://www.opentk.com/book/export/html/1198
			GL.Oes.GenVertexArrays (1, out vertexArray); // Create the Vertex Array Object, returning out the handle to it
			GL.Oes.BindVertexArray (vertexArray);

			GL.GenBuffers (1, out vertexBuffer); // Create an vertext buffer
			GL.BindBuffer (BufferTarget.ArrayBuffer, vertexBuffer); 

			// Bind the buffer data to the vertex buffer
			GL.BufferData<PPSSignaturePoint> (BufferTarget.ArrayBuffer, (IntPtr) (Marshal.SizeOf<PPSSignaturePoint>() * SignatureVertexData.Length), SignatureVertexData, BufferUsage.DynamicDraw);
			BindShaderAttributes ();
			GL.Oes.BindVertexArray (0);
			//GL.GetError()

			/*
			glGenVertexArraysOES(1, &vertexArray);
			glBindVertexArrayOES(vertexArray);

			glGenBuffers(1, &vertexBuffer);
			glBindBuffer(GL_ARRAY_BUFFER, vertexBuffer);
			glBufferData(GL_ARRAY_BUFFER, sizeof(SignatureVertexData), SignatureVertexData, GL_DYNAMIC_DRAW);
			[self bindShaderAttributes];
			*/

			GL.Oes.GenVertexArrays (1, out dotsArray); // Create the Vertex Array Object, returning out the handle to it
			GL.Oes.BindVertexArray (dotsBuffer); // Bind the buffer to the array

			GL.GenBuffers (1, out dotsArray); // Create an vertext buffer
			GL.BindBuffer (BufferTarget.ArrayBuffer, dotsBuffer);

			GL.BufferData<PPSSignaturePoint> (BufferTarget.ArrayBuffer, (IntPtr) (Marshal.SizeOf<PPSSignaturePoint>() * SignatureDotsData.Length), SignatureDotsData, BufferUsage.DynamicDraw);

			BindShaderAttributes ();
			GL.Oes.BindVertexArray (0); // complete
			/*
			// Signature Dots
			glGenVertexArraysOES(1, &dotsArray);
			glBindVertexArrayOES(dotsArray);

			glGenBuffers(1, &dotsBuffer);
			glBindBuffer(GL_ARRAY_BUFFER, dotsBuffer);
			glBufferData(GL_ARRAY_BUFFER, sizeof(SignatureDotsData), SignatureDotsData, GL_DYNAMIC_DRAW);
			*/



			// Perspective
			//_effect.Transform.ProjectionMatrix = Matrix4.CreateOrthographic(-1, 1, -1, 1);
			//_effect.Transform.ModelViewMatrix = Matrix4.CreateTranslation(0.0f, 0.0f, -1.0f);

			//length = 0;
			//penThickness = 0.003;
			//previousPoint = CGPointMake(-100, -100);
		}

		void TearDownGL ()
		{
			EAGLContext.SetCurrentContext (context);
			GL.DeleteBuffers (1, ref vertexBuffer);
			GL.DeleteBuffers (1, ref dotsBuffer);
			effect = null;
		}

		private void Pan(UIPanGestureRecognizer gesture)
		{
		}

		private void Tap(UITapGestureRecognizer gesture)
		{
			var l = gesture.LocationInView (this);

			if (UIGestureRecognizerState.Recognized == gesture.State) {
				// Start using the dots buffer as the array buffer
				GL.BindBuffer (BufferTarget.ArrayBuffer, dotsBuffer);

				var touchpoint = ViewPointToGL (l, this.Bounds, new Vector3 (1, 1, 1));
				AddVertex (ref dotsLength, touchpoint);

			}
		}

		private void Press(UILongPressGestureRecognizer gesture)
		{
		}

		public bool HasSignature { get; private set; }
		public UIImage SignatureImage { get; private set; }

		public void Erase()
		{
		}

		public override void Draw (RectangleF rect)
		{
			base.Draw (rect);
			// This diagram makes it so clear!
			//http://www.opentk.com/node/1342

			GL.ClearColor (1.0f, 1.0f, 1.0f, 1.0f);
			GL.Clear (ClearBufferMask.ColorBufferBit);

			_effect.PrepareToDraw ();

			if (length > 2) {
				GL.Oes.BindVertexArray (vertexArray);
				GL.DrawArrays (BeginMode.TriangleStrip, 0, length);
			}

			if (dotsLength > 0) {
				GL.Oes.BindVertexArray (dotsArray);
				GL.DrawArrays (BeginMode.TriangleStrip, 0, dotsLength);
			}
		}
	}
}

