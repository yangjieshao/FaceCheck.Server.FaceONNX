using System;
using System.IO;
using System.Threading.Tasks;

namespace FaceCheck.Server.Helper
{
    /// <summary>
    /// </summary>
    public static class Util
    {
        /// <summary>
        /// </summary>
        public static readonly string WWWDir = "wwwroot";

        private static readonly string RootDir = "Photo";

        /// <summary>
        /// </summary>
        public static readonly string TempRootDir = "PhotoTemp";

        /// <summary>
        /// </summary>
        public static async Task<string> SavePic(byte[] buffer, bool isPng = false)
        {
            if (!Directory.Exists(Path.Combine(WWWDir, RootDir)))
            {
                Directory.CreateDirectory(Path.Combine(WWWDir, RootDir));
            }

            string picPath = Path.Combine(Path.Combine(WWWDir, RootDir), DateTime.Now.ToString("yyyy")
                , DateTime.Now.ToString("MM"), DateTime.Now.ToString("dd"));
            return await Save(buffer, isPng,picPath);
        }

        private static async Task<string> Save(byte[] buffer, bool isPng,string picPath)
        {
            if (buffer != null
                && buffer.Length > 0)
            {
                if (!Directory.Exists(picPath))
                {
                    Directory.CreateDirectory(picPath);
                }
                string ext;
                if (isPng)
                {
                    ext = ".png";
                }
                else
                {
                    ext = buffer.GetPicExtention();
                }

                picPath = Path.Combine(picPath, Guid.NewGuid().ToString("N") + ext);

                await  File.WriteAllBytesAsync(picPath, buffer);
                return picPath;
            }
            return string.Empty;
        }

        /// <summary>
        /// </summary>
        public static async Task<string> SaveTempPic(byte[] buffer)
        {
            if (!Directory.Exists(Path.Combine(WWWDir, TempRootDir)))
            {
                Directory.CreateDirectory(Path.Combine(WWWDir, TempRootDir));
            }

            string picPath = Path.Combine(Path.Combine(WWWDir, TempRootDir), DateTime.Now.ToString("yyyy")
                , DateTime.Now.ToString("MM"), DateTime.Now.ToString("dd"));
            return await Save(buffer, false,picPath);
        }

        /// <summary>
        /// 文件夹路径转为url
        /// </summary>
        public static string Path2Url(string path)
        {
            return path.Replace('\\', '/').TrimStart('/');
        }
    }
}