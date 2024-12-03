using FaceCheck.Server.Helper;
using System.Collections.Generic;

namespace FaceCheck.Server.Configs
{
    /// <summary>
    /// </summary>
    [ConfigSection(Section = Section)]
    public class SystemConfig : IConfig
    {
        /// <summary>
        /// </summary>
        public const string Section = "System";

        /// <summary>
        /// 是否对照片进行校验
        /// </summary>
        public bool IsReal { set; get; }

        /// <summary>
        /// </summary>
        public bool UseSwagger { set; get; }

        /// <summary>
        /// </summary>
        public bool IsBase64Log { set; get; }

        /// <summary>
        /// 测试时使用 是否缓存照片文件
        /// </summary>
        public bool IsSaveImg { get; set; }

        /// <summary>
        /// json 是否漂亮打印
        /// </summary>
        public bool PrettyPrintingJson { set; get; }

        /// <summary>
        /// 隐藏命令行窗口(只在Windows有效)
        /// </summary>
        public bool Hide { set; get; }
        /// <summary>
        /// 是否启用微软的算法遥测(会被微软收集数据用于优化项目)
        /// </summary>
        public bool MicrosoftOnnxTelemetry { set; get; }

        /// <summary>
        /// 自定义Title
        /// </summary>
        public string Title { set; get; }

        /// <summary>
        /// 启用目录浏览
        /// </summary>
        public bool UseDirectoryBrowser { set; get; }
    }
}