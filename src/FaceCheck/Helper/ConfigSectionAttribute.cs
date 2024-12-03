using System;

namespace FaceCheck.Server.Helper
{
    /// <summary>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigSectionAttribute : Attribute
    {
        /// <summary>
        /// </summary>
        public string Section { get; set; }
    }
}
