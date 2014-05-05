using System;
using System.Runtime.InteropServices;
using OpenTK;
using GLSignature.Util;
using OpenTK.Graphics.ES20;
using MonoTouch.GLKit;
using System.Drawing;

namespace GLSignature
{
	[StructLayout(LayoutKind.Sequential)]
	struct SignaturePointVector
	{
		[Vertex(GLKVertexAttrib.Position, VertexAttribPointerType.Float)]
		public Vector3		Location;
		[Vertex(GLKVertexAttrib.Color, VertexAttribPointerType.Float)]
		public Vector3		Color;

		public static SignaturePointVector FromPoint(float x, float y, Vector3 color)
		{
			return new SignaturePointVector()
			{
				Location = new Vector3 (x, y, 0),
				Color =  color,
			};
		}

		public static SignaturePointVector FromViewPoint(PointF viewPoint, RectangleF bounds, Vector3 color)
		{
			var x = (viewPoint.X / bounds.Size.Width * 2.0f - 1);
			var y = ((viewPoint.Y / bounds.Size.Height) * 2.0f - 1) * -1;
			return FromPoint(x,y, color);
		}
	};
}

