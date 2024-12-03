using FaceCheck.Server.Configs;
using FaceCheck.Server.Helper;
using FaceCheck.Server.Util;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Vanara.PInvoke;

namespace FaceCheck
{
    /// <summary>
    /// </summary>
    public class Program
    {
        private static SystemConfig SystemConfig { set; get; }

        /// <summary>
        /// </summary>
        private static Microsoft.Extensions.Logging.ILogger Logger { set; get; }

        /// <summary>
        /// </summary>
        public static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var builder = WebApplication.CreateBuilder(args);
            SystemConfig = FaceCheck.Server.Helper.Extension.LoadConfig<SystemConfig>(builder.Configuration) ?? new();
            if (!string.IsNullOrWhiteSpace(SystemConfig.Title))
            {
                Console.Title = SystemConfig.Title;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                #region DisbleQuickEditMode

                var h = Kernel32.GetStdHandle(Kernel32.StdHandleType.STD_INPUT_HANDLE);
                Kernel32.GetConsoleMode(h, out Kernel32.CONSOLE_INPUT_MODE mode);
                mode &= ~Kernel32.CONSOLE_INPUT_MODE.ENABLE_QUICK_EDIT_MODE;
                Kernel32.SetConsoleMode(h, mode);

                #endregion DisbleQuickEditMode

                if (!Kernel32.SetConsoleCtrlHandler(HandlerRoutine, true))
                {
                    var defaultColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("抱歉，API注入失败，按任意键退出！");
                    Console.ForegroundColor = defaultColor;
                    Console.ReadKey();
                    return;
                }
                if (SystemConfig.Hide)
                {
                    User32.ShowWindow(Kernel32.GetConsoleWindow(), ShowWindowCommand.SW_HIDE);
                }
            }

            builder.Services.AddLogging(r =>
            {
#if DEBUG
                r.AddDebug();
#endif
                r.ClearProviders()
                .SetMinimumLevel(LogLevel.Trace);
                r.AddConsole();
            })
                .AddSingleton<PhotoCheck>()
                .AddSingleton<FaceONNXUtil>()
                .ConfigureServices(SystemConfig)
                .AddSerilog((services, lc) => lc
                            .ReadFrom.Configuration(builder.Configuration)
                            .ReadFrom.Services(services)
                            .Enrich.FromLogContext()
                            )
                .AddHealthChecks();
            var app = builder.Build();

            Logger = app.Logger;

            var faceONNXUtil = app.Services.GetService<FaceONNXUtil>();
            faceONNXUtil.Activation();

            var informationalVersion = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            Logger.LogInformation("Version: {informationalVersion}", informationalVersion);
            SetThreadPool();
            // Write streamlined request completion events, instead of the more verbose ones from the framework.
            // To use the default framework request logging instead, remove this line and set the "Microsoft"
            // level in appsettings.json to "Information".
            app.UseSerilogRequestLogging();

            app.AppInit(SystemConfig, builder.Configuration);

            app.MapControllers();

            app.Run();
        }

        private static void SetThreadPool()
        {
            ThreadPool.GetMinThreads(out int workerThreadsMin, out int completionPortThreadsMin);
            Logger.LogDebug("获取当前 workerThreadsMin:{workerThreadsMin} completionPortThreadsMin:{completionPortThreadsMin}", workerThreadsMin, completionPortThreadsMin);
            ThreadPool.GetMaxThreads(out int workerThreadsMax, out int completionPortThreadsMax);
            Logger.LogDebug("获取当前 workerThreadsMax:{workerThreadsMax} completionPortThreadsMax:{completionPortThreadsMax}", workerThreadsMax, completionPortThreadsMax);
            ThreadPool.SetMinThreads(workerThreadsMax, completionPortThreadsMax);
            ThreadPool.GetMinThreads(out workerThreadsMin, out completionPortThreadsMin);
            Logger.LogDebug("获取设置后 workerThreadsMin:{workerThreadsMin} completionPortThreadsMin:{completionPortThreadsMin}", workerThreadsMin, completionPortThreadsMin);
        }

        private static bool HandlerRoutine(Kernel32.CTRL_EVENT dwCtrlType)
        {
            Logger?.LogInformation("HandlerRoutine: {dwCtrlType}", dwCtrlType);
            return true;
        }
    }
}