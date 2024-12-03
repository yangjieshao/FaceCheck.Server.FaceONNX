using System.Text.Json;

namespace FaceCheck.Server.Helper
{
    /// <summary>
    /// 最大化命名
    /// </summary>
    public class UpperCaseNameingPolicy : JsonNamingPolicy
    {
        /// <summary>
        /// </summary>
        public override string ConvertName(string name) => name.ToUpper();
    }
}