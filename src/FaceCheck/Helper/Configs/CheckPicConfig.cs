namespace FaceCheck.Server.Helper.Configs
{
    /// <summary>
    /// </summary>
    [ConfigSection(Section = Section)]
    public class CheckPicConfig : IConfig
    {
        /// <summary>
        /// </summary>
        public const string Section = "CheckPic";

        /// <summary>
        /// 引擎个数
        /// </summary>
        public int EngineNum { set; get; }

        /// <summary>
        /// </summary>
        public bool NeedAge { set; get; }

        /// <summary>
        /// </summary>
        public bool NeedGender { set; get; }

        /// <summary>
        /// </summary>
        public double OpenEye { set; get; }

        /// <summary>
        /// 相似度 (0.0~1.0) 默认0.8
        /// </summary>
        public double Similarity { get; set; } = 0.8;

        /// <summary>
        /// 照片质量 (0.0~1.0) (-1表示不检测)
        /// </summary>
        public float ImageQuality { get; set; } = -1;
    }
}