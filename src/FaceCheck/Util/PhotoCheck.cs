using FaceCheck.Server.Configs;
using FaceCheck.Server.Helper;
using FaceCheck.Server.Helper.Configs;
using FaceCheck.Server.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FaceCheck.Server.Util
{
    /// <summary>
    /// 实际处理照片
    /// </summary>
    public class PhotoCheck
    {
        private ILogger<PhotoCheck> Logger { get; }
        private SystemConfig SystemConfig { get; } = new SystemConfig();
        private CheckPicConfig CheckPicConfig { get; } = new CheckPicConfig();
        private CutPhotoConfig CutPhotoConfig { get; } = new CutPhotoConfig();
        private FaceONNXUtil FaceONNXUtil { get; }

        /// <summary>
        /// </summary>
        public PhotoCheck(ILogger<PhotoCheck> logger, IConfiguration configuration, FaceONNXUtil faceONNXUtil)
        {
            Logger = logger;
            configuration.Bind(SystemConfig.Section, SystemConfig);
            configuration.Bind(CheckPicConfig.Section, CheckPicConfig);
            configuration.Bind(CutPhotoConfig.Section, CutPhotoConfig);
            FaceONNXUtil = faceONNXUtil;
        }

        /// <summary>
        /// </summary>
        public async Task< HttpResult<byte[]>> CheckFace(byte[] picBuffer)
        {
            if (!picBuffer.IsFacePhoto())
            {
                throw new PhotoException("只支持png或jpg或bmp或gif图片");
            }
            var result = new HttpResult<byte[]>();

            if (!SystemConfig.IsReal)
            {
                Logger.LogInformation($"不校验 直接返回原数据");
                result.Message = string.Empty;
                result.Data = picBuffer;
                result.Success = true;
                return result;
            }

            var image = Image.Load<Rgb24>(picBuffer)
                ?? throw new PhotoException("图片解析失败");
            ClearImageOrient(ref image);
            var faceInfo = GetFaceInfo(image);

            if (faceInfo.Rectangle.Width < CutPhotoConfig.MinSize
                || faceInfo.Rectangle.Height < CutPhotoConfig.MinSize)
            {
                image.Dispose();
                throw new PhotoException($"小于最小可用宽度 {faceInfo.Rectangle.Width}*{faceInfo.Rectangle.Height} PhotoMinSize:{CutPhotoConfig.MinSize}");
            }

            GetMaxFaceImage(image, faceInfo);

            result.Data = ImageToBytes(image);
            result.Success = true;
            image.Dispose();

            if (SystemConfig.IsSaveImg)
            {
                await Helper.Util.SaveTempPic(result.Data);
            }
            return result;
        }

        /// <summary>
        /// </summary>
        public async Task< HttpResult<HeadLocationInfoBase>> CheckFace2(byte[] picBuffer)
        {
            if (!picBuffer.IsFacePhoto())
            {
                throw new PhotoException("只支持png或jpg或bmp图片");
            }

            var result = new HttpResult<HeadLocationInfoBase>();

            if (!SystemConfig.IsReal)
            {
                Logger.LogInformation($"不校验 直接返回原数据");
                var headLocationInfo_ = new HeadLocationInfoBase
                {
                    FaceInfo = new FaceInfo
                    {
                        IsLeftEyeClosed = false,
                        IsRightEyeClosed = false,
                        RgbLive = 1,
                        FaceShelter = 0,
                    },
                    SnapBuffer = picBuffer
                };
                result.Data = headLocationInfo_;
                result.Success = true;
                return result;
            }

            var image = Image.Load<Rgb24>(picBuffer)
                 ?? throw new PhotoException("图片解析失败");
            ClearImageOrient(ref image);

            var faceInfo = GetFaceInfo(image);

            if (faceInfo.Rectangle.Width < CutPhotoConfig.MinSize
                || faceInfo.Rectangle.Height < CutPhotoConfig.MinSize)
            {
                image.Dispose();
                throw new PhotoException($"小于最小可用宽度 {faceInfo.Rectangle.Width}*{faceInfo.Rectangle.Height} PhotoMinSize:{CutPhotoConfig.MinSize}");
            }

            byte[] snapBuffer = null;
            if (CutPhotoConfig.Need)
            {
                GetMaxFaceImage(image, faceInfo);
                snapBuffer = ImageToBytes(image);
            }

            var headLocationInfo = new HeadLocationInfoBase
            {
                Height = faceInfo.Rectangle.Height,
                Width = faceInfo.Rectangle.Width,
                X = faceInfo.Rectangle.X,
                Y = faceInfo.Rectangle.Y,
                ImageQuality = faceInfo.ImageQuality,
                FaceInfo = faceInfo,
                SnapBuffer = snapBuffer,
            };
            result.Data = headLocationInfo;
            result.Success = true;
            if (SystemConfig.IsSaveImg)
            {
                await Helper.Util.SaveTempPic(result.Data.SnapBuffer);
            }
            Logger.LogInformation($"登记照扣脸校验完毕");
            image.Dispose();
            return result;
        }

        /// <summary>
        /// </summary>
        public HttpResult<double> PicCompare(byte[] picBuffer1, byte[] picBuffer2)
        {
            if (!picBuffer1.IsFacePhoto()
                || !picBuffer2.IsFacePhoto())
            {
                throw new PhotoException("只支持png或jpg或bmp图片");
            }
            var result = new HttpResult<double>();
            if (!SystemConfig.IsReal)
            {
                Logger.LogInformation($"不校验 直接返回成功数据");
                result.Message = string.Empty;
                result.Data = 0.9;
                result.Success = true;
                return result;
            }

            if (picBuffer1 == null
                || picBuffer1.Length == 0
                || picBuffer2 == null
                || picBuffer2.Length == 0)
            {
                Logger.LogInformation($"未接到照片流");
                result.Data = -1;
                result.Success = false;
                return result;
            }

            var engine1 = FaceONNXUtil.GetFreeEngine()
                        ?? throw new EngineException("系统忙，稍后重试");
            var engine2 = engine1;
            if (CheckPicConfig.EngineNum > 1)
            {
                engine2 = FaceONNXUtil.GetFreeEngine();
                engine2 ??= engine1;
            }

            FaceInfo faceInfo1 = null;
            FaceInfo faceInfo2 = null;
            if (!ReferenceEquals(engine1, engine2))
            {
                var task1 = Task.Run(() =>
                {
                    Logger.LogInformation($"准备获取第一张照片的人脸数据");
                    faceInfo1 = FaceONNXUtil.GetFaceInfo(engine1, picBuffer1, needFaceInfo: false, needFeatures: true);
                    Logger.LogInformation($"获取第一张照片的人脸数据结束");
                });
                var task2 = Task.Run(() =>
                {
                    Logger.LogInformation($"准备获取第二张照片的人脸数据");
                    faceInfo2 = FaceONNXUtil.GetFaceInfo(engine2, picBuffer2, needFaceInfo: false, needFeatures: true);
                    Logger.LogInformation($"获取第二张照片的人脸数据结束");
                });
                Task.WaitAll(task1, task2);
            }
            else
            {
                Logger.LogInformation($"准备获取第一张照片的人脸数据");
                faceInfo1 = FaceONNXUtil.GetFaceInfo(engine1, picBuffer1, needFaceInfo: false, needFeatures: true);
                Logger.LogInformation($"获取第一张照片的人脸数据结束");

                Logger.LogInformation($"准备获取第二张照片的人脸数据");
                faceInfo2 = FaceONNXUtil.GetFaceInfo(engine2, picBuffer2, needFaceInfo: false, needFeatures: true);
                Logger.LogInformation($"获取第二张照片的人脸数据结束");
            }

            string errMsg = string.Empty;
            if (faceInfo1 == null)
            {
                errMsg = "第一张照片没有人脸; ";
            }
            if (faceInfo2 == null)
            {
                errMsg += "第二张照片没有人脸; ";
            }
            if (!ReferenceEquals(engine1, engine2))
            {
                FaceONNXUtil.ReturnEngine(engine2);
            }
            FaceONNXUtil.ReturnEngine(engine1);
            if (!string.IsNullOrWhiteSpace(errMsg))
            {
                throw new PhotoException(errMsg);
            }

            var feature1 = FaceONNXUtil.Feature2Vectors(faceInfo1.Feature);
            var feature2 = FaceONNXUtil.Feature2Vectors(faceInfo2.Feature);
            result.Data = FaceONNXUtil.FaceFeatureCompare(feature1, feature2);
            result.Success = result.Data >= CheckPicConfig.Similarity;

            Logger.LogInformation("两张照片相似度为:{Similarity} 对比算法通过:<{Success}>", result.Data, result.Success);
            return result;
        }

        /// <summary>
        /// </summary>
        public async Task ReadyUrlBuffer(SetPicInfo comparePicInfo)
        {
            var info4Log = new SetPicInfo4Log
            {
                Pic1 = comparePicInfo.Pic1 is { Length: > 0 },
                Pic2 = comparePicInfo.Pic2 is { Length: > 0 },
            };
            if (SystemConfig.IsSaveImg)
            {
                // 演示时用 正式使用 关闭保存图片配置
                info4Log.Pic1Path = await Helper.Util.SavePic(comparePicInfo.Pic1, false);
                info4Log.Pic2Path = await Helper.Util.SavePic(comparePicInfo.Pic2, false);
            }
            Logger.LogInformation($"接到数据（转换后） Pic1:<{info4Log.Pic1}> Pic2:<{info4Log.Pic2}> " +
                                                 $"Pic1Path:<{info4Log.Pic1Path}> Pic2Path:<{info4Log.Pic2Path}>");
        }

        /// <summary>
        /// </summary>
        private Model.FaceInfo GetFaceInfo(Image<Rgb24> image)
        {
            var engine = FaceONNXUtil.GetFreeEngine()
                        ?? throw new EngineException("系统忙，稍后重试");
            var faceInfo = FaceONNXUtil.GetFaceInfo(engine, image, needFaceInfo: true, needFeatures: false);
            FaceONNXUtil.ReturnEngine(engine);
            if (faceInfo == null)
            {
                image.Dispose();
                throw new PhotoException("照片提取人脸失败");
            }
            if (CheckPicConfig.ImageQuality > 0
                && faceInfo.ImageQuality < CheckPicConfig.ImageQuality)
            {
                image.Dispose();
                throw new PhotoException("照片质量不合格");
            }
            return faceInfo;
        }

        /// <summary>
        /// 获取外扩大小
        /// </summary>
        private void GetOutSize(int left, int right, int top, int bottom, ref int headOutWidth, ref int headOutHeight, bool isSwapWidthHeight)
        {
            // 外扩宽度
            headOutWidth = (right - left) / CutPhotoConfig.OutwardScale;
            // 外扩高度
            headOutHeight = (bottom - top) / CutPhotoConfig.OutwardScale;
            if (headOutWidth < CutPhotoConfig.MinOutwardPix)
            {
                headOutWidth = CutPhotoConfig.MinOutwardPix;
            }
            if (headOutHeight < CutPhotoConfig.MinOutwardPix)
            {
                headOutHeight = CutPhotoConfig.MinOutwardPix;
            }
            if (Math.Abs(CutPhotoConfig.ScaleWidth - CutPhotoConfig.ScaleHeight) > double.Epsilon)
            {
                var oldWidth = right - left;
                var oldHeight = bottom - top;
                var newWidth = oldWidth + headOutWidth * 2;
                var newHeight = oldHeight + headOutHeight * 2;
                if (isSwapWidthHeight)
                {
                    if (CutPhotoConfig.ScaleWidth < CutPhotoConfig.ScaleHeight)
                    {
                        headOutWidth = Convert.ToInt32((newWidth * CutPhotoConfig.ScaleHeight / CutPhotoConfig.ScaleWidth - oldHeight) / 2);
                    }
                    else
                    {
                        headOutHeight = Convert.ToInt32((newHeight * CutPhotoConfig.ScaleWidth / CutPhotoConfig.ScaleHeight - oldWidth) / 2);
                    }
                }
                else
                {
                    if (CutPhotoConfig.ScaleWidth < CutPhotoConfig.ScaleHeight)
                    {
                        headOutHeight = Convert.ToInt32((newWidth * CutPhotoConfig.ScaleHeight / CutPhotoConfig.ScaleWidth - oldHeight) / 2);
                    }
                    else
                    {
                        headOutWidth = Convert.ToInt32((newHeight * CutPhotoConfig.ScaleWidth / CutPhotoConfig.ScaleHeight - oldWidth) / 2);
                    }
                }
            }
        }

        /// <summary>
        /// </summary>
        private static bool IsSwapWidthHeight(RotateMode rotateMode)
        {
            if (rotateMode == RotateMode.Rotate90
                || rotateMode == RotateMode.Rotate270)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// </summary>
        private static RotateMode GetRotateMode(float faceOrient)
        {
            var result = RotateMode.None;
            if (faceOrient < 0)
            {
                faceOrient = 360 + faceOrient;
            }
            //if (faceOrient <= -60 && faceOrient >= -120)
            //{
            //    result = RotateMode.Rotate270;
            //}
            if (faceOrient >= 45 && faceOrient < 135)
            {
                result = RotateMode.Rotate90;
            }
            else if (faceOrient >= 135 && faceOrient < 225)
            {
                result = RotateMode.Rotate180;
            }
            else if(faceOrient >= 225 && faceOrient < 315)
            {
                result = RotateMode.Rotate270;
            }
            
            return result;
        }

        /// <summary>
        /// </summary>
        /// <param name="image"></param>
        /// <param name="maxFaceInfo"></param>
        /// <param name="only4Id">只是用于提取证件照(证件照不校验大小、活体、质量等人脸参数)</param>
        private void GetMaxFaceImage(Image<Rgb24> image, FaceInfo maxFaceInfo, bool only4Id = false)
        {
            int outWidth = 0;
            int outHeight = 0;
            var rotateMode = GetRotateMode(maxFaceInfo.FaceOrient);
            GetOutSize(maxFaceInfo.Rectangle.Left, maxFaceInfo.Rectangle.Right, maxFaceInfo.Rectangle.Top, maxFaceInfo.Rectangle.Bottom
                    , ref outWidth, ref outHeight, IsSwapWidthHeight(rotateMode));

            var rect = GetJHeadRect(image, maxFaceInfo.Rectangle.Left, maxFaceInfo.Rectangle.Right, maxFaceInfo.Rectangle.Top, maxFaceInfo.Rectangle.Bottom
                , outWidth, outHeight);

            CutImage(image, rect, rotateMode);

            SetBackgroundColor(ref image);

            // 照片是否过大 true 太大 要缩小 false 太小 要放大 null 不用缩放
            bool? isToobig = null;

            if (!only4Id)
            {
                if (image.Width > CutPhotoConfig.MaxWidth
                    || image.Height > CutPhotoConfig.MaxWidth)
                {
                    isToobig = true;
                }
                else if (image.Width < CutPhotoConfig.MinWidth
                    && image.Height < CutPhotoConfig.MinWidth)
                {
                    isToobig = false;
                }
            }
            // 缩放尺寸
            if (isToobig.HasValue)
            {
                var scale1 = Convert.ToSingle(image.Width) / Convert.ToSingle(isToobig.Value ? CutPhotoConfig.MaxWidth : CutPhotoConfig.MinWidth);
                var scale2 = Convert.ToSingle(image.Height) / Convert.ToSingle(isToobig.Value ? CutPhotoConfig.MaxWidth : CutPhotoConfig.MinWidth);
                var scale = scale1 < scale2 ? scale1 : scale2;
                if (Math.Abs(scale - 1f) > float.Epsilon)
                {
                    var newWidth = (int)Math.Floor(image.Width / scale);
                    var newHeight = (int)Math.Floor(image.Height / scale);
                    {
                        Logger.LogInformation($"照片尺寸不合适: {image.Width}*{image.Height} 进行自动缩放 {newWidth}*{newHeight}");
                        ScaleImage(image, newWidth, newHeight);
                    }
                }
            }
        }

        private static void CutImage(Image<Rgb24> image, Model.Rectangle rect, RotateMode rotateMode = RotateMode.None)
        {
            var cropArea = new SixLabors.ImageSharp.Rectangle(rect.X, rect.Y, rect.Width, rect.Height);

            image.Mutate(x => x.Crop(cropArea).Rotate(rotateMode));
        }

        /// <summary>
        /// 按指定宽高缩放图片
        /// </summary>
        /// <param name="image">原图片</param>
        /// <param name="dstWidth">目标图片宽</param>
        /// <param name="dstHeight">目标图片高</param>
        /// <returns></returns>
        private static void ScaleImage(Image<Rgb24> image, int dstWidth, int dstHeight)
        {
            try
            {
                ////按比例缩放
                //float scaleRate = GetWidthAndHeight(image.Width, image.Height, dstWidth, dstHeight);
                //int width = (int)(image.Width * scaleRate);
                //int height = (int)(image.Height * scaleRate);

                var width = dstWidth;
                var height = dstHeight;

                //将宽度调整为4的整数倍
                if (width % 4 != 0)
                {
                    width -= width % 4;
                }
                image.Mutate(x => x.Resize(width, height));
            }
            catch (Exception)
            {
                // no use
            }
        }

        /// <summary>
        /// 按长宽1:1填充背景色
        /// </summary>
        /// <param name="image"> </param>
        private void SetBackgroundColor(ref Image<Rgb24> image)
        {
            if (image != null
                && image.Width != image.Height
                && CutPhotoConfig.NewBg.Need)
            {
                Logger.LogInformation("填充图片背景色");
                var isWidthLonger = image.Width > image.Height;
                //构造最终的图片白板
                var rotatedBitmap = new Image<Rgb24>(isWidthLonger ? image.Width : image.Height, isWidthLonger ? image.Width : image.Height);

                var oldimage = image;
                rotatedBitmap.Mutate(x =>
                {
                    x.BackgroundColor(Color.FromRgb(CutPhotoConfig.NewBg.R, CutPhotoConfig.NewBg.G, CutPhotoConfig.NewBg.B));
                    x.DrawImage(oldimage, new Point((rotatedBitmap.Width - oldimage.Width) / 2, (rotatedBitmap.Height - oldimage.Height) / 2), 1);
                });

                image.Dispose();
                image = rotatedBitmap;
            }
        }

        /// <summary>
        /// 矫正剪裁图片范围
        /// </summary>
        /// <param name="image"> 原图片 </param>
        /// <param name="rectLeft"></param>
        /// <param name="rectRight"></param>
        /// <param name="rectTop"></param>
        /// <param name="rectBottom"></param>
        /// <param name="headOutWidth"></param>
        /// <param name="headOutHeight"></param>
        /// <returns> 剪裁后的图片 </returns>
        public static Model.Rectangle GetJHeadRect(Image<Rgb24> image, int rectLeft, int rectRight, int rectTop, int rectBottom, int headOutWidth, int headOutHeight)
        {
            var leftChange = rectLeft > headOutWidth ? rectLeft - headOutWidth : 0;
            var topChange = rectTop > headOutHeight ? rectTop - headOutHeight : 0;
            var rightChange = rectRight + headOutWidth;
            var bottomChange = rectBottom + headOutHeight;

            if (rightChange > image.Width)
            {
                rightChange = image.Width;
            }
            if (bottomChange > image.Height)
            {
                bottomChange = image.Height;
            }
            return new Model.Rectangle { X = leftChange, Y = topChange, Width = rightChange - leftChange, Height = bottomChange - topChange };
        }

        /// <summary>
        /// </summary>
        public static byte[] ImageToBytes(Image<Rgb24> image)
        {
            using var memStream = new MemoryStream();
            image.SaveAsJpeg(memStream, new JpegEncoder() { Quality = 80, SkipMetadata = true, ColorType = JpegEncodingColor.Rgb });
            var buffer = memStream.ToArray();
            return buffer;
        }

        /// <summary>
        /// </summary>
        public static void ClearImageOrient(ref Image<Rgb24> image)
        {
            IExifValue<ushort> orientation = null;
            image.Metadata?.ExifProfile?.TryGetValue(ExifTag.Orientation, out orientation);
            if (orientation != null)
            {
                var rr = orientation.GetValue();
                if (rr + string.Empty != "1")
                {
                    image.Mutate(x => x.AutoOrient());
                }
                image.Metadata.ExifProfile.RemoveValue(ExifTag.Orientation);
            }
        }
    }
}