using FaceCheck.Server.Model;
using FaceCheck.Server.Util;
using FaceONNX;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace FaceCheck.Server.Controllers
{
    /// <summary>
    /// </summary>
    [ApiController]
    //[Route("[controller]")]
    //[Route("[action]")]
    [Produces(MediaTypeNames.Application.Json)]
    public class FaceController : ControllerBase
    {
        private ILogger<FaceController> Logger { get; }
        private PhotoCheck PhotoCheck { get; }

        /// <summary>
        /// </summary>
        public FaceController(ILogger<FaceController> logger, PhotoCheck photoCheck)
        {
            Logger = logger;
            PhotoCheck = photoCheck;
        }

        /// <summary>
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(HttpResult<byte[]>), StatusCodes.Status200OK)]
        [Route("")]
        public async Task<IActionResult> CheckPicx(SetPicInfo comparePicInfo)
        {
            return await CheckPic(comparePicInfo);
        }

        /// <summary>
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(HttpResult<byte[]>), StatusCodes.Status200OK)]
        [Route("[action]")]
        public async Task<IActionResult> CheckPic(SetPicInfo comparePicInfo)
        {
            await PhotoCheck.ReadyUrlBuffer(comparePicInfo);

            if (comparePicInfo != null
                && comparePicInfo.Pic1 != null
                && comparePicInfo.Pic1.Length > 0)
            {
                if (comparePicInfo.Pic2 == null
                || comparePicInfo.Pic2.Length == 0)
                {
                    var result = await PhotoCheck.CheckFace(comparePicInfo.Pic1);
                    return Ok(result);
                }
                else
                {
                    var result = PhotoCheck.PicCompare(comparePicInfo.Pic1, comparePicInfo.Pic2);
                    return Ok(result);
                }
            }
            return Ok(new HttpResult { Success = false, Message = "数据不正确" });
        }

        /// <summary>
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(HttpResult<byte[]>), StatusCodes.Status200OK)]
        [Route("[action]")]
        public async Task<IActionResult> CheckPicByFlile(IFormFile file, IFormFile file2, CancellationToken cancellationToken)
        {
            if (file == null)
            {
                return Ok(new HttpResult
                {
                    Success = false,
                    Message = "必须上传第一张照片"
                });
            }

            var comparePicInfo = new SetPicInfo();

            {
                await using var stream = new BufferedStream(file.OpenReadStream());
                comparePicInfo.Pic1 = new byte[stream.Length];
                await stream.ReadAsync(comparePicInfo.Pic1, 0, (int)stream.Length, cancellationToken);
            }

            if (file2 != null)
            {
                await using var stream = new BufferedStream(file2.OpenReadStream());
                comparePicInfo.Pic2 = new byte[stream.Length];
                await stream.ReadAsync(comparePicInfo.Pic2, 0, (int)stream.Length, cancellationToken);
            }

            await PhotoCheck.ReadyUrlBuffer(comparePicInfo);

            if (comparePicInfo != null
                && comparePicInfo.Pic1 != null
                && comparePicInfo.Pic1.Length > 0)
            {
                if (comparePicInfo.Pic2 == null
                || comparePicInfo.Pic2.Length == 0)
                {
                    var result = await PhotoCheck.CheckFace(comparePicInfo.Pic1);
                    return Ok(result);
                }
                else
                {
                    var result = PhotoCheck.PicCompare(comparePicInfo.Pic1, comparePicInfo.Pic2);
                    return Ok(result);
                }
            }
            return Ok(new HttpResult { Success = false, Message = "数据不正确" });
        }

        /// <summary>
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(HttpResult<HeadLocationInfoBase>), StatusCodes.Status200OK)]
        [Route("[action]")]
        public async Task<IActionResult> CheckPic2(SetPicInfo comparePicInfo)
        {
            await PhotoCheck.ReadyUrlBuffer(comparePicInfo);
            if (comparePicInfo != null
                && comparePicInfo.Pic1 != null
                && comparePicInfo.Pic1.Length > 0)
            {
                if (comparePicInfo.Pic2 == null
                || comparePicInfo.Pic2.Length == 0)
                {
                    var result = await PhotoCheck.CheckFace2(comparePicInfo.Pic1);
                    return Ok(result);
                }
                else
                {
                    var result = PhotoCheck.PicCompare(comparePicInfo.Pic1, comparePicInfo.Pic2);
                    return Ok(result);
                }
            }
            return Ok(new HttpResult { Success = false, Message = "数据不正确" });
        }

        /// <summary>
        /// </summary>
        [HttpPost]
        [RequestSizeLimit(1024L * 1024L * 1024L * 5L)]
        [ProducesResponseType(typeof(HttpResult<HeadLocationInfoBase>), StatusCodes.Status200OK)]
        [Route("[action]")]
        public async Task<IActionResult> CheckPic2ByFile(IFormFile file, IFormFile file2, CancellationToken cancellationToken)
        {
            if (file == null)
            {
                return Ok(new HttpResult
                {
                    Success = false,
                    Message = "必须上传第一张照片"
                });
            }
            try
            {
                var comparePicInfo = new SetPicInfo();

                {
                    await using var stream = new BufferedStream(file.OpenReadStream());
                    comparePicInfo.Pic1 = new byte[stream.Length];
                    await stream.ReadAsync(comparePicInfo.Pic1, 0, (int)stream.Length, cancellationToken);
                }

                if (file2 != null)
                {
                    await using var stream = new BufferedStream(file2.OpenReadStream());
                    comparePicInfo.Pic2 = new byte[stream.Length];
                    await stream.ReadAsync(comparePicInfo.Pic2, 0, (int)stream.Length, cancellationToken);
                }

                await PhotoCheck.ReadyUrlBuffer(comparePicInfo);

                if (comparePicInfo != null
                    && comparePicInfo.Pic1 != null
                    && comparePicInfo.Pic1.Length > 0)
                {
                    if (comparePicInfo.Pic2 == null
                    || comparePicInfo.Pic2.Length == 0)
                    {
                        var result = await PhotoCheck.CheckFace2(comparePicInfo.Pic1);
                        return Ok(result);
                    }
                    else
                    {
                        var result = PhotoCheck.PicCompare(comparePicInfo.Pic1, comparePicInfo.Pic2);
                        return Ok(result);
                    }
                }
                return Ok(new HttpResult { Success = false, Message = "数据不正确" });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "接收文件失败");
                return Ok(new HttpResult<FaceDetectionResult[]>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }
    }
}