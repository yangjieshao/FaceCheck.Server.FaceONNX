namespace FaceCheck.Server.Model
{
    /// <summary>
    /// </summary>
    public class HeadLocationInfoBase
    {
        /// <summary>
        /// </summary>
        public int X { set; get; }

        /// <summary>
        /// </summary>
        public int Y { set; get; }

        /// <summary>
        /// </summary>
        public int Width { set; get; }

        /// <summary>
        /// </summary>
        public int Height { set; get; }

        /// <summary>
        /// 图像质量 -1无效
        /// </summary>
        public float ImageQuality { set; get; } = -1;

        /// <summary>
        /// </summary>
        public byte[] SnapBuffer { set; get; }

        /// <summary>
        /// </summary>
        public FaceInfo FaceInfo { set; get; }
    }
}