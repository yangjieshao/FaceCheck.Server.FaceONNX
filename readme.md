使用 FaceONNX 对 照片进行校验和对比

全平台兼容 (windows  linux-x64 linux-arm64 ………………)
已在信创用 linux-arm64 （银河麒麟）上运行成功

# 注意！！
使用了 `Microsoft.ML.OnnxRuntime` 使用默认会开启遥测将一些遥测数据传回微软
需要在配置文件配置为不上传 （配置文件默认已设置为不上传）

发布的时候需要手动从nuget缓存目录复制运行库到发布目录

`C:\Users\yangjieshao\.nuget\packages\microsoft.ml.onnxruntime\1.20.1`

# Linux 启动方式

Net8 依赖 libicu

安装依赖环境：
CentOS 8.2 以上

`yum install libicu`
`yum install glibc `

ubuntu 20.04 不用额外安装依赖

`apt update`
`apt install libicu`
`apt install glibc`

启动：

`nohup ./FaceCheck.Server > /dev/null 2>&1 &`


查看进程id 

 `ps -e -f -w |grep FaceCheck.Server`
 
 结束进程
 `kill 402816`
