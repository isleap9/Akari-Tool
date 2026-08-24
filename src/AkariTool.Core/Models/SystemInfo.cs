namespace AkariTool.Core.Models
{
    public readonly record struct SystemInfo(
        string Edition,   // "Windows 11 Pro"
        string Version,   // "Version 25H2 • Build 26200.8037"
        string Cpu,       // "AMD Ryzen 9 9950X3D"
        string Gpu,       // "NVIDIA GeForce RTX 4080"
        string Memory);   // "32 GB @ 6000 MHz"
}
