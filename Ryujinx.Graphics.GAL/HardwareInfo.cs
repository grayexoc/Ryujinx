﻿namespace Ryujinx.Graphics.GAL
{
    public struct HardwareInfo
    {
        public string GpuVendor { get; }
        public string GpuModel { get; }

        public HardwareInfo(string gpuVendor, string gpuModel)
        {
            GpuVendor = gpuVendor;
            GpuModel = gpuModel;
        }
    }
}
