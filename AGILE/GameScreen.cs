using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows.Forms;

using Marshal = System.Runtime.InteropServices.Marshal;

namespace AGILE
{
    /// <summary>
    /// The GameScreen class is a type of PictureBox containing only a Bitmap, the pixel
    /// contents of which are painted in the NearestNeighbor InterpolationMode, and 
    /// whose pixels are copied into from a Pixels int array on each call to the Render
    /// method.
    /// </summary>
    class GameScreen : PictureBox
    {
        /// <summary>
        /// The pixels array for the AGI screen. Any change made to this array will be copied 
        /// to the Bitmap on every frame.
        /// </summary>
        public int[] Pixels;

        /// <summary>
        /// The Bitmap that holds the pixels for the AGI screen. Fills up the whole PictureBox.
        /// </summary>
        private Bitmap screenBitmap;

        /// <summary>
        /// Constructor for GameScreen.
        /// </summary>
        public GameScreen()
        {
            this.screenBitmap = new Bitmap(320, 200, PixelFormat.Format32bppPArgb);
            this.Pixels = new int[this.screenBitmap.Width * this.screenBitmap.Height];
            this.SizeMode = PictureBoxSizeMode.StretchImage;
            this.Image = this.screenBitmap;
            this.Dock = DockStyle.Fill;
        }

        /// <summary>
        /// Overrides the PictureBox OnPaint method so that the NearestNeighor InterpolationMode
        /// can be applied.
        /// </summary>
        /// <param name="pe">The PaintEventArgs for the Paint event, simply passed to the base class.</param>
        protected override void OnPaint(PaintEventArgs pe)
        {
            if (Monitor.TryEnter(screenBitmap))
            {
                try
                {
                    // Makes the pixels crisp and clear as we'd have seen them in the old low res screens.
                    pe.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                    base.OnPaint(pe);
                }
                finally
                {
                    Monitor.Exit(screenBitmap);
                }
            }
        }

        /// <summary>
        /// Invoked when the GameScreen should be rendered. When called, the AGI screen data in 
        /// the Pixels is rendered within the PictureBox.
        /// </summary>
        public void Render()
        {
            if (Monitor.TryEnter(screenBitmap))
            {
                try
                {
                    // Copy the pixel data in to the screen Bitmap.
                    var bitmapData = screenBitmap.LockBits(new Rectangle(0, 0, screenBitmap.Width, screenBitmap.Height), ImageLockMode.ReadWrite, screenBitmap.PixelFormat);
                    Marshal.Copy(Pixels, 0, bitmapData.Scan0, Pixels.Length);
                    screenBitmap.UnlockBits(bitmapData);
                }
                finally
                {
                    Monitor.Exit(screenBitmap);
                }
            }

            // Request the PictureBox to be redrawn.
            this.Invalidate();
        }
    }
}
