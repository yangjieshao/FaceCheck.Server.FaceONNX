using FaceCheck.Server.Configs;
using FaceCheck.Server.Model;
using FaceCheck.Server.Util;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace FaceCheck.Server.Helper
{
    /// <summary>
    /// </summary>
    public interface IConfig
    { }

    /// <summary>
    /// </summary>
    public static class Extension
    {
        /// <summary>
        /// 判断是否为人脸图片 (png jpg bmp)
        /// </summary>
        /// <param name="imageBuffer"> </param>
        /// <returns> </returns>
        public static bool IsFacePhoto(this byte[] imageBuffer)
        {
            bool result = false;
            if (imageBuffer != null
                && imageBuffer.Length > 8)
            {
                var header = imageBuffer.Take(8);

                result = PicHeader.PngHeader.SequenceEqual(header)
                    || PicHeader.BmpHeader.SequenceEqual(header.Take(PicHeader.BmpHeader.Length))
                    || PicHeader.JpgHeader.SequenceEqual(header.Take(PicHeader.JpgHeader.Length))
                    || PicHeader.GifHeader1.SequenceEqual(header.Take(PicHeader.GifHeader1.Length))
                    || PicHeader.GifHeader2.SequenceEqual(header.Take(PicHeader.GifHeader2.Length));
            }
            return result;
        }

        /// <summary>
        /// 获取图片文件类型 只支持 png gif bmp tiff icon jpg
        /// </summary>
        /// <param name="imageBuffer"> </param>
        /// <returns> </returns>
        public static string GetPicExtention(this byte[] imageBuffer)
        {
            string result = string.Empty;
            if (imageBuffer != null
                && imageBuffer.Length > 8)
            {
                var header = imageBuffer.Take(8);
                if (PicHeader.TgaHeader1.SequenceEqual(header.Take(PicHeader.TgaHeader1.Length))
                || PicHeader.TgaHeader2.SequenceEqual(header.Take(PicHeader.TgaHeader2.Length)))
                {
                    // result = ImageFormat.TGA;
                }
                else if (PicHeader.CurHeader.SequenceEqual(header))
                {
                    // result = ImageFormat.CUR;
                }
                else if (PicHeader.PngHeader.SequenceEqual(header))
                {
                    result = ".png";
                }
                else if (PicHeader.GifHeader1.SequenceEqual(header.Take(PicHeader.GifHeader1.Length))
                    || PicHeader.GifHeader2.SequenceEqual(header.Take(PicHeader.GifHeader2.Length)))
                {
                    result = ".gif";
                }
                else if (PicHeader.BmpHeader.SequenceEqual(header.Take(PicHeader.BmpHeader.Length)))
                {
                    result = ".bmp";
                }
                else if (PicHeader.TiffHeader1.SequenceEqual(header.Take(PicHeader.TiffHeader1.Length))
                    || PicHeader.TiffHeader2.SequenceEqual(header.Take(PicHeader.TiffHeader2.Length)))
                {
                    result = ".tiff";
                }
                else if (PicHeader.IconHeader.SequenceEqual(header))
                {
                    result = ".icon";
                }
                else if (PicHeader.JpgHeader.SequenceEqual(header.Take(PicHeader.JpgHeader.Length)))
                {
                    result = ".jpg";
                }
            }
            return result;
        }

        /// <summary>
        /// </summary>
        public static T LoadConfig<T>(IConfiguration rootConfiguration) where T : IConfig
        {
            var customAttributes = typeof(T).GetCustomAttributes(typeof(ConfigSectionAttribute), true);
            if (customAttributes is { Length: > 0 }
                && customAttributes[0] is ConfigSectionAttribute configSectionAttribute
                && !string.IsNullOrWhiteSpace(configSectionAttribute.Section))
            {
                return LoadConfig<T>(rootConfiguration, configSectionAttribute.Section);
            }
            return default;
        }

        /// <summary>
        /// </summary>
        public static T LoadConfig<T>(IConfiguration rootConfiguration, string section)
        {
            if (string.IsNullOrWhiteSpace(section)) return default;
            var configuration = rootConfiguration.GetSection(section);
            if (configuration != null)
            {
                return configuration.Get<T>();
            }
            return default;
        }

        /// <summary>
        /// </summary>
        public static IList<TK> LoadConfigs<T, TK>(IConfiguration rootConfiguration) where T : IList<TK> where TK : IConfig
        {
            var customAttributes = typeof(TK).GetCustomAttributes(typeof(ConfigSectionAttribute), true);
            if (customAttributes is { Length: > 0 }
                && customAttributes[0] is ConfigSectionAttribute configSectionAttribute
                && !string.IsNullOrWhiteSpace(configSectionAttribute.Section))
            {
                var configuration = rootConfiguration.GetSection(configSectionAttribute.Section);
                if (configuration != null)
                {
                    var result = new List<TK>();

                    foreach (var childConfiguration in configuration.GetChildren())
                    {
                        result.Add(childConfiguration.Get<TK>());
                    }
                    return result;
                }
            }
            return default;
        }

        /// <summary>
        /// </summary>
        /// <returns> </returns>
        public static IServiceCollection ConfigureServices(this IServiceCollection services, SystemConfig systemConfig)
        {
            services.AddJsonOptions(systemConfig.PrettyPrintingJson)
                    .SetPostConfigure()
                    .AddMemoryCache()
                    .AddHealthChecks(); ;

            services.Configure<FormOptions>(options =>
            {
                options.KeyLengthLimit = int.MaxValue;
                options.ValueLengthLimit = int.MaxValue;
                options.MultipartHeadersLengthLimit = int.MaxValue;
                options.MultipartBodyLengthLimit = long.MaxValue;
                options.MemoryBufferThreshold = int.MaxValue;
            });
            services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = int.MaxValue;
                options.Limits.MaxRequestBufferSize = int.MaxValue;
            });

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            services.AddEndpointsApiExplorer();
            if (systemConfig.UseSwagger)
            {
                services.AddSwaggerGen(c =>
                {
                    var informationalVersion = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                    c.SwaggerDoc("v1", new OpenApiInfo
                    {
                        Version = informationalVersion,
                        Title = systemConfig.Title,
                    });
                    c.UseDateTimeStringConverters();
                    var rootDir = new DirectoryInfo(Environment.CurrentDirectory);
                    foreach (var item in rootDir.GetFiles("*.xml"))
                    {
                        c.IncludeXmlComments(item.FullName, true);
                    }
                });
            }
            //services.AddControllers(options =>
            //{
            //    options.Conventions.Insert(0, new RouteConvention(new RouteAttribute("api")));
            //    options.ReturnHttpNotAcceptable = true;
            //});
            services.AddControllers();
            return services;
        }

        /// <summary>
        /// 错误参数信息拦截
        /// </summary>
        /// <returns> </returns>
        public static WebApplication AppInit(this WebApplication app, SystemConfig systemConfig, IConfiguration configuration)
        {
            #region 解决Ubuntu Nginx 代理不能获取IP问题

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.All
            });
            app.UseStatusCodePagesWithReExecute("/error/{0}.html");

            #endregion 解决Ubuntu Nginx 代理不能获取IP问题

            app.UseHealthChecks("/healthcheck");
            app.UseExceptionHandlingMiddleware();
            app.UseRouting();

            app.Use(async (context, next) =>
            {
                if (!context.WebSockets.IsWebSocketRequest
                   && context.Request.Headers.Accept == "text/plain")
                {
                    context.Request.Headers.Accept = $"{MediaTypeNames.Application.Json},*/*";
                }
                await next();
            });

            if (systemConfig.UseSwagger)
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint($"/swagger/v1/swagger.json", $"{systemConfig.Title}");
                });
            }

            app.UseDefaultFiles();
            if (systemConfig.UseDirectoryBrowser)
            {
                app.UseDirectoryBrowser();
            }

            // 提供静态文件
            var staticfile = new StaticFileOptions
            {
                ServeUnknownFileTypes = true,
                DefaultContentType = "application/octet-stream"//设置默认  MIME
            };
            var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();

            foreach (var item in provider.Mappings)
            {
                if (item.Value.StartsWith("text/")
                    || item.Value.StartsWith(MediaTypeNames.Application.Json))
                {
                    provider.Mappings[item.Key] = item.Value + ";charset=utf-8";
                }
            }

            var mimeSection = configuration.GetChildren().FirstOrDefault(record => record.Key == "MIME");
            if (mimeSection != null)
            {
                foreach (var mime in mimeSection.GetChildren())
                {
                    provider.Mappings[mime.Key] = mime.Value;
                }
            }

            staticfile.ContentTypeProvider = provider;
            app.UseStaticFiles(staticfile);

            return app;
        }

        /// <summary>
        /// 错误参数信息拦截
        /// </summary>
        public static IServiceCollection SetPostConfigure(this IServiceCollection services)
        {
            services.PostConfigure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = (context) =>
                {
                    var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
                    var details = factory.CreateValidationProblemDetails(context.HttpContext, context.ModelState);
                    if (details != null)
                    {
                        if (!context.HttpContext.Response.HasStarted)
                        {
                            if (details.Status.HasValue)
                            {
                                context.HttpContext.Response.StatusCode = details.Status.Value;
                            }
                            context.HttpContext.Response.ContentType = MediaTypeNames.Application.Json;
                        }

                        var result = new HttpResult
                        {
                            Success = false,
                            Code = context.HttpContext.Response.StatusCode,
                        };
                        if (!string.IsNullOrWhiteSpace(details.Detail))
                        {
                            result.Message = details.Detail;
                        }
                        else
                        {
                            if (details.Errors.Count > 0)
                            {
                                foreach (var error in details.Errors)
                                {
                                    result.Message += $"{error.Key}:{string.Join(" ", error.Value)} ";
                                }
                            }
                        }
                        return new JsonResult(result);
                    }
                    return null;
                };
            });
            return services;
        }

        /// <summary>
        /// </summary>
        public static IServiceCollection AddJsonOptions(this IServiceCollection services, bool prettyPrintingJson)
        {
            services.Configure(delegate (JsonOptions options)
            {
                options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
                // 首字母小写驼峰命名
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                // 允许注释
                options.JsonSerializerOptions.ReadCommentHandling = JsonCommentHandling.Skip;
                options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString;
                // 允许尾随逗号
                options.JsonSerializerOptions.AllowTrailingCommas = true;
                options.JsonSerializerOptions.WriteIndented = prettyPrintingJson;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(new UpperCaseNameingPolicy()));
            });
            services.Configure(delegate (Microsoft.AspNetCore.Http.Json.JsonOptions options)
            {
                options.SerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
                // 首字母小写驼峰命名
                options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                // 允许注释
                options.SerializerOptions.ReadCommentHandling = JsonCommentHandling.Skip;
                options.SerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString;
                // 允许尾随逗号
                options.SerializerOptions.AllowTrailingCommas = true;
                options.SerializerOptions.WriteIndented = prettyPrintingJson;
                options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(new UpperCaseNameingPolicy()));
            });
            return services;
        }

        /// <summary>
        /// </summary>
        /// <param name="options"> </param>
        public static void UseDateTimeStringConverters(this SwaggerGenOptions options)
        {
            options.MapType<DateTime>(() => new OpenApiSchema
            {
                Type = "string",
                Format = "dateTime",
                Example = OpenApiAnyFactory.CreateFromJson("\"2022/10/10 13:31:14\"")
            });
            options.MapType<DateTime?>(() => new OpenApiSchema
            {
                Type = "string",
                Format = "dateTime?",
                Example = OpenApiAnyFactory.CreateFromJson("\"2022/10/10 13:31:14\"")
            });
            options.MapType<object>(() => new OpenApiSchema
            {
                Type = "object",
                Format = "object",
                Example = OpenApiAnyFactory.CreateFromJson("{}")
            });
        }
    }

    internal static class PicHeader
    {
        /// <summary>
        /// </summary>
        public static byte[] TgaHeader1 { get; } = [0x00, 0x00, 0x02, 0x00, 0x00];

        /// <summary>
        /// </summary>
        public static byte[] TgaHeader2 { get; } = [0x00, 0x00, 0x10, 0x00, 0x00];

        /// <summary>
        /// </summary>
        public static byte[] CurHeader { get; } = [0x00, 0x00, 0x02, 0x00, 0x01, 0x00, 0x20, 0x20];

        /// <summary>
        /// </summary>
        public static byte[] PngHeader { get; } = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        /// <summary>
        /// </summary>
        public static byte[] GifHeader1 { get; } = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61];

        /// <summary>
        /// </summary>
        public static byte[] GifHeader2 { get; } = [0x47, 0x49, 0x46, 0x38, 0x37, 0x61];

        /// <summary>
        /// </summary>
        public static byte[] BmpHeader { get; } = [0x42, 0x4D];

        /// <summary>
        /// </summary>
        public static byte[] TiffHeader1 { get; } = [0x4D, 0x4D];

        /// <summary>
        /// </summary>
        public static byte[] TiffHeader2 { get; } = [0x49, 0x49];

        /// <summary>
        /// </summary>
        public static byte[] IconHeader { get; } = [0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x20, 0x20];

        /// <summary>
        /// </summary>
        public static byte[] JpgHeader { get; } = [0xff, 0xd8];
    }
}