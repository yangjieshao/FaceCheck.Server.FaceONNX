using System;
using System.Text.Json.Serialization;

namespace FaceCheck.Server.Model
{
    /// <summary>
    /// </summary>
    public class SetPicInfo
    {
        /// <summary>
        /// </summary>
        [JsonPropertyName("pic1")]
        public byte[] Pic1 { set; get; }

        /// <summary>
        /// </summary>
        [JsonPropertyName("pic2")]
        public byte[] Pic2 { set; get; }
    }

    /// <summary>
    /// 日志用
    /// </summary>
    public class SetPicInfo4Log
    {
        /// <summary>
        /// </summary>
        public bool Pic1 { set; get; } = false;

        /// <summary>
        /// </summary>
        public string Pic1Path { set; get; }

        /// <summary>
        /// </summary>
        public bool Pic2 { set; get; }

        /// <summary>
        /// </summary>
        public string Pic2Path { set; get; }
    }
}