using System;

namespace FaceCheck.Server.Helper
{
    /// <summary>
    /// 图片校验失败
    /// </summary>
    public class PhotoException : Exception
    {
        /// <summary>
        /// </summary>
        public PhotoException(string message) : base(message) { }
    }
    /// <summary>
    /// </summary>
    public class EngineException : Exception
    {
        /// <summary>
        /// </summary>
        public EngineException(string message) : base(message) { }
    }
}
