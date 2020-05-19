using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CanonCaptureFilter
{
    static class Utility
    {
        /// <summary>
        /// Get the bytes from the bitmap
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        public static byte[] GetBytes(this Bitmap bitmap)
        {
            BitmapData bmpData = null;
            try
            {
                Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);

                IntPtr ptr = bmpData.Scan0;
                int bytes = Math.Abs(bmpData.Stride) * bitmap.Height;
                byte[] rgbValues = new byte[bytes];
                Marshal.Copy(ptr, rgbValues, 0, bytes);

                // copy the bytes, row by row, reversed
                byte[] reversed = new byte[bytes];

                for (int i = 0; i < bmpData.Height; i++)
                    Array.Copy(rgbValues, i * bmpData.Stride, reversed, bmpData.Height - i, bmpData.Stride);

                return reversed;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error\r\n{0}0", ex);
                return null;
            }
            finally
            {
                if (bmpData != null)
                {
                    bitmap.UnlockBits(bmpData);
                    bmpData = null;
                }
            }
        }

        public static IntPtr CopyToUnmanaged(this byte[] data)
        {
            IntPtr unmanagedPointer = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, unmanagedPointer, data.Length);
            return unmanagedPointer;
        }

        // Copy the bitmap, rotate it, and return the result.
        public static Bitmap ModifiedBitmap(this Image original, RotateFlipType rotate_flip_type)
        {
            // Copy the Bitmap.
            Bitmap new_bitmap = new Bitmap(original);

            // Rotate and flip.
            new_bitmap.RotateFlip(rotate_flip_type);

            // Return the result.
            return new_bitmap;
        }
    }
}