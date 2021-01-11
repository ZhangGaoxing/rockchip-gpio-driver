// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Iot.Device.Gpio.Drivers.Rockchip.Rk3399Driver
{
    [StructLayout(LayoutKind.Explicit)]
    internal unsafe struct PmuGrfRegisterView
    {
        [FieldOffset(0x00040)]
        public uint GRF_GPIO0A_P;

        [FieldOffset(0x00044)]
        public uint GRF_GPIO0B_P;

        [FieldOffset(0x00048)]
        public uint GRF_GPIO0C_P;

        [FieldOffset(0x0004C)]
        public uint GRF_GPIO0D_P;

        [FieldOffset(0x00050)]
        public uint GRF_GPIO1A_P;

        [FieldOffset(0x00054)]
        public uint GRF_GPIO1B_P;

        [FieldOffset(0x00058)]
        public uint GRF_GPIO1C_P;

        [FieldOffset(0x0005C)]
        public uint GRF_GPIO1D_P;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal unsafe struct GrfRegisterView
    {
        [FieldOffset(0x0E040)]
        public uint GRF_GPIO2A_P;

        [FieldOffset(0x0E044)]
        public uint GRF_GPIO2B_P;

        [FieldOffset(0x0E048)]
        public uint GRF_GPIO2C_P;

        [FieldOffset(0x0E04C)]
        public uint GRF_GPIO2D_P;

        [FieldOffset(0x0E050)]
        public uint GRF_GPIO3A_P;

        [FieldOffset(0x0E054)]
        public uint GRF_GPIO3B_P;

        [FieldOffset(0x0E058)]
        public uint GRF_GPIO3C_P;

        [FieldOffset(0x0E05C)]
        public uint GRF_GPIO3D_P;

        [FieldOffset(0x0E060)]
        public uint GRF_GPIO4A_P;

        [FieldOffset(0x0E064)]
        public uint GRF_GPIO4B_P;

        [FieldOffset(0x0E068)]
        public uint GRF_GPIO4C_P;

        [FieldOffset(0x0E06C)]
        public uint GRF_GPIO4D_P;
    }
}
