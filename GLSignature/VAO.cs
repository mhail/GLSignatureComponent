using System;
using OpenTK.Graphics.ES20;
using System.Runtime.InteropServices;
using System.Linq;
using MonoTouch.GLKit;
using MonoTouch;

namespace GLSignature.Util
{
	public class VAO<T> : IDisposable where T : struct
	{
		protected uint VertexArray;
		protected uint vertexBuffer;

		public readonly int Size;
		int index = 0;

		public int Length
		{
			get { return Math.Min(index, Size); }
		}

		public VAO(int size)
		{
			Size = size;

			GL.Oes.GenVertexArrays (1, out VertexArray);
			GL.Oes.BindVertexArray (VertexArray);

			GL.GenBuffers (1, out vertexBuffer);
			GL.BindBuffer (BufferTarget.ArrayBuffer, vertexBuffer);
			GL.BufferData (BufferTarget.ArrayBuffer, (IntPtr)(size * Marshal.SizeOf<T>()), IntPtr.Zero, BufferUsage.StaticDraw);

			var fields = typeof(T).GetFields ();

			foreach (var field in fields) {
				foreach (var attribute in field.GetCustomAttributes (typeof(VertexAttribute), false).OfType<VertexAttribute> ())
				{
					var fieldSize = Marshal.SizeOf (field.FieldType);
					var offset = Marshal.OffsetOf<T> (field.Name);

					int pointersPerField = 4;

					switch (attribute.PointerType) {
					case VertexAttribPointerType.Byte:
						pointersPerField = fieldSize;
						break;
					case VertexAttribPointerType.Float:
						pointersPerField = fieldSize / sizeof(float);
						break;
					default:
						throw new NotImplementedException ();
					}

					GL.EnableVertexAttribArray ((int) attribute.AttributeType);
					GL.VertexAttribPointer ((int) attribute.AttributeType, pointersPerField, attribute.PointerType,
						false, Marshal.SizeOf<T>(), offset);
				}
			}

			GL.Oes.BindVertexArray (0);
		}

		#region IDisposable implementation
		public void Dispose(){
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		#endregion

		protected virtual void Dispose(bool disposing){
			if (disposing){
				if (0 != vertexBuffer) {
					GL.DeleteBuffers (1, ref vertexBuffer);
				}
				if (0 != this.VertexArray) {
					GL.Oes.DeleteVertexArrays (1, ref VertexArray);
				}
			}
		}

		public void Bind ()
		{
			GL.Oes.BindVertexArray (VertexArray);
		}

		public void Update(int index, params T[] data)
		{
			// Bind the array buffer to our vertex buffer handle
			GL.BindBuffer (BufferTarget.ArrayBuffer, vertexBuffer);

			// Calc the size and offset
			var offset = new IntPtr(Marshal.SizeOf<T> () * index);
			var size = new IntPtr (Marshal.SizeOf<T> () * data.Length);

			// Copy the data to the array buffer
			GL.BufferSubData (BufferTarget.ArrayBuffer, offset, size, data);

			// Unbind
			GL.BindBuffer (BufferTarget.ArrayBuffer, 0);
		}

		public void Add(params T[] data)
		{
			Update (this.index, data);
			this.index += data.Length;
		}

		public void Clear()
		{
			this.index = 0;
		}
	}

	[AttributeUsage(AttributeTargets.Field)]
	public class VertexAttribute : Attribute
	{
		public readonly GLKVertexAttrib AttributeType;
		public readonly VertexAttribPointerType PointerType;

		public VertexAttribute(GLKVertexAttrib attributeType, VertexAttribPointerType type)
		{
			this.AttributeType = attributeType;
			this.PointerType = type;
		}
	}
}

