using FaceCheck.Server.Configs;
using FaceCheck.Server.Helper.Configs;
using FaceONNX;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UMapx.Core;
using UMapx.Imaging;

namespace FaceCheck.Server.Util
{
    internal class EngineGroup
    {
        public EngineGroup(bool needAge, bool needGender, bool needEye)
        {
            if (needAge)
            {
                GenderClassifier = new();
            }
            if (needGender)
            {
                AgeEstimator = new();
            }
            if (needEye)
            {
                EyeBlinkClassifier = new();
            }
        }

        public FaceDetector Detector { get; } = new();
        public Face68LandmarksExtractor LandmarksExtractor { get; } = new();
        public FaceEmbedder Embedder { get; } = new();
        public FaceGenderClassifier GenderClassifier { get; }
        public FaceAgeEstimator AgeEstimator { get; }
        public EyeBlinkClassifier EyeBlinkClassifier { get; }
    }

    /// <summary>
    /// </summary>
    public class FaceONNXUtil : IDisposable
    {
        private ILogger<FaceONNXUtil> Logger { get; }

        private SystemConfig SystemConfig { get; } = new();

        private CheckPicConfig CheckPicConfig { get; } = new();

        /// <summary>
        /// 初始化多个引擎时使用 (假设引擎支持不多线程并发使用)
        /// </summary>
        private ConcurrentBag<EngineGroup> Engines { get; } = [];

        /// <summary>
        /// 仅初始化一个引擎时使用 (假设引擎支持多线程并发使用)
        /// </summary>
        private EngineGroup SingleEngine { set; get; }

        private bool _isActivationed = false;

        /// <summary>
        /// </summary>
        public FaceONNXUtil(ILogger<FaceONNXUtil> logger, IConfiguration configuration)
        {
            Logger = logger;
            configuration.Bind(CheckPicConfig.Section, CheckPicConfig);
            configuration.Bind(SystemConfig.Section, SystemConfig);

            if (!SystemConfig.MicrosoftOnnxTelemetry)
            {
                OrtEnv.Instance().DisableTelemetryEvents();
            }

            Logger.LogInformation("Microsoft.ML.OnnxRuntime Version:{v}", OrtEnv.Instance().GetVersionString());
            var availableProviders = OrtEnv.Instance().GetAvailableProviders();
            if(availableProviders != null)
            {
                foreach( var availableProvider in availableProviders )
                {
                    Logger.LogInformation("Microsoft.ML.OnnxRuntime AvailableProvider:{v}", availableProvider);
                }
            }
        }

        /// <summary>
        /// 激活
        /// </summary>
        public bool Activation()
        {
            if (_isActivationed)
            {
                Logger.LogInformation("已激活过算法");
                return true;
            }
            _isActivationed = true;
            if (!SystemConfig.IsReal)
            {
                Logger.LogInformation("不真实使用算法，跳过");
                return true;
            }

            if (CheckPicConfig.EngineNum <= 1)
            {
                SingleEngine = new EngineGroup(CheckPicConfig.NeedAge, CheckPicConfig.NeedGender, CheckPicConfig.OpenEye > 0.0f);
                Logger.LogInformation("初始化引擎组成功");
            }
            else
            {
                for (int i = 0; i < CheckPicConfig.EngineNum; i++)
                {
                    var pEngine = new EngineGroup(CheckPicConfig.NeedAge, CheckPicConfig.NeedGender, CheckPicConfig.OpenEye > 0.0f);
                    Logger.LogInformation("初始化第{num}组引擎组成功", i + 1);
                    Engines.Add(pEngine);
                }
            }
            Logger.LogInformation("共初始化了{num}组引擎", Engines.Count);
            return true;
        }

        /// <summary>
        /// 获取空闲引擎
        /// </summary>
        internal EngineGroup GetFreeEngine()
        {
            if(CheckPicConfig.EngineNum <= 1)
            {
                return SingleEngine;
            }

            EngineGroup result = default;
            int i = 0;
            while (result == null
                && i < 100)
            {
                if (!Engines.IsEmpty
                    && Engines.TryTake(out EngineGroup val))
                {
                    result = val;
                }
                Task.Delay(2).Wait();
                i++;
            }

            return result;
        }

        /// <summary>
        /// </summary>
        internal void ReturnEngine(EngineGroup engine)
        {
            if (CheckPicConfig.EngineNum <= 1)
            {
                return;
            }
            if (engine != default)
            {
                Engines.Add(engine);
            }
        }

        /// <summary>
        /// 销毁引擎
        /// </summary>
        /// <param name="pEngine"> </param>
        private void UninitEngine(EngineGroup pEngine)
        {
            pEngine?.Detector?.Dispose();
            pEngine?.LandmarksExtractor?.Dispose();
            pEngine?.Embedder?.Dispose();
            pEngine?.GenderClassifier?.Dispose();
            pEngine?.AgeEstimator?.Dispose();
            pEngine?.EyeBlinkClassifier?.Dispose();
        }

        /// <summary>
        /// 获取特征值
        /// </summary>
        internal Model.FaceInfo GetFaceInfo(EngineGroup pEngine, byte[] imageBuffer, bool needFaceInfo = true, bool needFeatures = true)
        {
            using var theImage = Image.Load<Rgb24>(imageBuffer);

            var (face, gender, age, isLeftEyeClosed, isRightEyeClosed, embedding) = GetEmbedding(theImage, pEngine, needFaceInfo, needFeatures);

            if (face == null)
            {
                return null;
            }
            var box = face.Box;

            var result = new Model.FaceInfo
            {
                Feature = needFeatures ? Vectors2Feature(embedding) : null,
                ImageQuality = face.Score,
                FaceOrient = face.Points.RotationAngle,
                Sex = gender,
                Age = Convert.ToInt32(MathF.Floor(age)),
                IsLeftEyeClosed = isLeftEyeClosed,
                IsRightEyeClosed = isRightEyeClosed,
                Rectangle = new Model.Rectangle
                {
                    Y = box.Y,
                    X = box.X,
                    Width = box.Width,
                    Height = box.Height
                }
            };

            if (result == null)
            {
                // 照片没有人脸
                return result;
            }

            return result;
        }

        /// <summary>
        /// </summary>
        /// <param name="pFeature1"> </param>
        /// <param name="pFeature2"> </param>
        /// <returns> 相似度 0.0~1.0 </returns>
        internal double FaceFeatureCompare(float[] pFeature1, float[] pFeature2)
        {
            var similarity = pFeature1.Cosine(pFeature2);
            return similarity;
        }

        private (FaceDetectionResult face, int gender, float age, bool? isLeftEyeClosed, bool? isRightEyeClosed, float[] embedding)
            GetEmbedding(Image<Rgb24> image, EngineGroup pEngine, bool needFaceInfo = true, bool needFeatures = true)
        {
            var array = GetImageFloatArray(image);
            var rectangles = pEngine.Detector.Forward(array);

            var rectangle = rectangles.FirstOrDefault().Box;

            if (!rectangle.IsEmpty)
            {
                // landmarks
                var points = pEngine.LandmarksExtractor.Forward(array, rectangle);
                var angle = points.RotationAngle;

                // alignment
                var aligned = FaceProcessingExtensions.Align(array, rectangle, angle);

                int gender = -1;
                float age = -1.0f;
                bool? isLeftEyeClosed = default;
                bool? isRightEyeClosed = default;
                if (needFaceInfo)
                {
                    if (pEngine.GenderClassifier != null)
                    {
                        var output = pEngine.GenderClassifier.Forward(aligned);
                        _ = Matrice.Max(output, out gender);
                    }
                    if (pEngine.AgeEstimator != null)
                    {
                        age = pEngine.AgeEstimator.Forward(aligned).FirstOrDefault();
                    }
                    if (pEngine.EyeBlinkClassifier != null)
                    {
                        var left_eye_rect = Face68Landmarks.GetLeftEyeRectangle(points);
                        var right_eye_rect = Face68Landmarks.GetRightEyeRectangle(points);

                        using var left_eye = image.Clone(ctx => ctx.Crop(new Rectangle(left_eye_rect.X, left_eye_rect.Y, left_eye_rect.Width, left_eye_rect.Height)));
                        using var right_eye = image.Clone(ctx => ctx.Crop(new Rectangle(right_eye_rect.X, right_eye_rect.Y, right_eye_rect.Width, right_eye_rect.Height)));

                        var left_eye_value = pEngine.EyeBlinkClassifier.Forward(GetImageFloatArray(left_eye));
                        var right_eye_value = pEngine.EyeBlinkClassifier.Forward(GetImageFloatArray(right_eye));

                        var left_eye11 = Math.Round(left_eye_value[0], 1);
                        var right_eye111 = Math.Round(right_eye_value[0], 1);

                        isLeftEyeClosed = left_eye11 < CheckPicConfig.OpenEye;
                        isRightEyeClosed = right_eye111 < CheckPicConfig.OpenEye;
                    }
                }

                float[] embedding;
                if (needFeatures)
                {
                    embedding = pEngine.Embedder.Forward(aligned);
                }
                else
                {
                    embedding = new float[512];
                }

                return (rectangles.FirstOrDefault(), gender, age, isLeftEyeClosed, isRightEyeClosed, embedding);
            }

            return (default, -1, -1.0f, default, default, new float[512]);
        }

        /// <summary>
        /// </summary>
        public float[] Feature2Vectors(byte[] byteArray)
        {
            int floatCount = byteArray.Length / sizeof(float);
            float[] floatArray = new float[floatCount];

            GCHandle handle = GCHandle.Alloc(byteArray, GCHandleType.Pinned);
            try
            {
                Marshal.Copy(handle.AddrOfPinnedObject(), floatArray, 0, floatCount);
            }
            finally
            {
                handle.Free();
            }

            return floatArray;
        }

        /// <summary>
        /// </summary>
        public byte[] Vectors2Feature(float[] floatArray)
        {
            int size = sizeof(float) * floatArray.Length;
            byte[] byteArray = new byte[size];

            GCHandle handle = GCHandle.Alloc(byteArray, GCHandleType.Pinned);
            try
            {
                Marshal.Copy(floatArray, 0, handle.AddrOfPinnedObject(), floatArray.Length);
            }
            finally
            {
                handle.Free();
            }

            return byteArray;
        }

        private static float[][,] GetImageFloatArray(Image<Rgb24> image)
        {
            var array = new[]
            {
                new float [image.Height,image.Width],
                new float [image.Height,image.Width],
                new float [image.Height,image.Width]
            };

            image.ProcessPixelRows(pixelAccessor =>
            {
                for (var y = 0; y < pixelAccessor.Height; y++)
                {
                    var row = pixelAccessor.GetRowSpan(y);
                    for (var x = 0; x < pixelAccessor.Width; x++)
                    {
                        array[2][y, x] = row[x].R / 255.0F;
                        array[1][y, x] = row[x].G / 255.0F;
                        array[0][y, x] = row[x].B / 255.0F;
                    }
                }
            });

            return array;
        }

        #region IDisposable Support

        private bool disposedValue = false;

        /// <summary>
        /// </summary>
        /// <param name="disposing"> </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (!Engines.IsEmpty)
                    {
                        var engines = new EngineGroup[Engines.Count];

                        Engines.CopyTo(engines, 0);
                        Engines.Clear();
                        for (int i = 0; i < engines.Length; i++)
                        {
                            UninitEngine(engines[i]);
                        }
                    }
                    UninitEngine(SingleEngine);
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// </summary>
        ~FaceONNXUtil()
        {
            Dispose(false);
        }

        /// <summary>
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion IDisposable Support
    }
}