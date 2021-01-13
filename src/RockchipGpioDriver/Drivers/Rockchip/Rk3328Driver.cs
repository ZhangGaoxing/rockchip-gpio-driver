﻿using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Iot.Device.Gpio.Drivers
{
    /// <summary>
    /// A GPIO driver for Rockchip RK3328
    /// </summary>
    public unsafe class Rk3328Driver : RockchipDriver
    {
        /// <inheritdoc/>
        protected override uint[] GpioRegisterAddresses =>
            new[] { 0xFF21_0000, 0xFF22_0000, 0xFF23_0000, 0xFF24_8000 };

        /// <summary>
        /// GRF, used for general non-secure system.
        /// </summary>
        protected uint GeneralRegisterFiles => 0xFF10_0000;

        private IntPtr _grfPointer = IntPtr.Zero;
        private static readonly int[] _grfOffsets = new[]
        {
            0x0100, 0x0104, 0x0108, 0x010C,  // GPIO0 PU/PD control
            0x0110, 0x0114, 0x0118, 0x011C,  // GPIO1 PU/PD control
            0x0120, 0x0124, 0x0128, 0x012C,  // GPIO2 PU/PD control
            0x0130, 0x0134, 0x0138, 0x013C  // GPIO3 PU/PD control
        };
        private static readonly int[] _iomuxOffsets = new[]
        {
            0x0000, -1, 0x0004, -1, 0x0008, -1, 0x000C, -1,  // GPIO0 iomux control
            0x0010, -1, 0x0014, -1, 0x0018, -1, 0x001C, -1,  // GPIO1 iomux control
            // H: GPIO0-4; L: GPIO5-7; value length is 3 bit
            // 2A       2BL     2BH     2CL     2CH     2D
            0x0020, -1, 0x0024, 0x0028, 0x002C, 0x0030, 0x0034, -1,  // GPIO2 iomux control
            // 3AL  3AH     3BL     3BH     3C          3D
            0x0038, 0x003C, 0x0040, 0x0044, 0x0048, -1, 0x004C, -1  // GPIO3 iomux control
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="Rk3328Driver"/> class.
        /// </summary>
        public Rk3328Driver()
        {
            InitializeGeneralRegisterFiles();
        }

        /// <inheritdoc/>
        protected override void SetPinMode(int pinNumber, PinMode mode)
        {
            (int GpioNumber, int Port, int PortNumber) unmapped = UnmapPinNumber(pinNumber);
            int bitOffset = unmapped.PortNumber * 2;

            // set GPIO direction
            // data register (GPIO_SWPORT_DDR) offset is 0x0004
            uint* dirPointer = (uint*)(_gpioPointers[unmapped.GpioNumber] + 0x0004);
            uint dirValue = *dirPointer;

            switch (mode)
            {
                case PinMode.Input:
                case PinMode.InputPullDown:
                case PinMode.InputPullUp:
                    // set direction: input is 0; output is 1
                    dirValue &= (uint)~(1 << (unmapped.Port * 8 + unmapped.PortNumber));
                    break;
                case PinMode.Output:
                    dirValue |= (uint)(1 << (unmapped.Port * 8 + unmapped.PortNumber));
                    break;
                default:
                    break;
            }

            uint* modePointer, iomuxPointer;
            uint modeValue, iomuxValue;

            if (_iomuxOffsets[unmapped.GpioNumber * 8 + unmapped.Port * 2 + 1] == -1)
            {
                // high and low registers are not distinguished
                // set pin to GPIO mode
                iomuxPointer = (uint*)(_grfPointer + _iomuxOffsets[unmapped.GpioNumber * 8 + unmapped.Port * 2]);
                iomuxValue = *iomuxPointer;
                // software write enable
                iomuxValue |= (uint)(0b11 << (16 + bitOffset));
                // GPIO mode is 0x00
                iomuxValue &= (uint)~(0b11 << bitOffset);
            }
            else
            {
                int iomuxBitOffset = unmapped.PortNumber * 3;

                if (unmapped.PortNumber <= 4)
                {
                    // low register
                    iomuxPointer = (uint*)(_grfPointer + _iomuxOffsets[unmapped.GpioNumber * 8 + unmapped.Port * 2]);
                }
                else
                {
                    // high register
                    iomuxPointer = (uint*)(_grfPointer + _iomuxOffsets[unmapped.GpioNumber * 8 + unmapped.Port * 2 + 1]);
                }

                iomuxValue = *iomuxPointer;
                // software write enable
                iomuxValue |= (uint)(0b111 << (16 + iomuxBitOffset));
                // GPIO mode is 0x000
                iomuxValue &= (uint)~(0b111 << iomuxBitOffset);
            }

            // set GPIO pull-up/down mode
            modePointer = (uint*)(_grfPointer + _grfOffsets[unmapped.GpioNumber * 4 + unmapped.Port]);
            modeValue = *modePointer;
            // software write enable
            modeValue |= (uint)(0b11 << (16 + bitOffset));
            // pull-up is 0b01; pull-down is 0b10; default is 0b00
            modeValue &= (uint)~(0b11 << bitOffset);

            switch (mode)
            {
                case PinMode.InputPullDown:
                    modeValue |= (uint)(0b10 << bitOffset);
                    break;
                case PinMode.InputPullUp:
                    modeValue |= (uint)(0b01 << bitOffset);
                    break;
                default:
                    break;
            }

            *iomuxPointer = iomuxValue;
            *modePointer = modeValue;
            *dirPointer = dirValue;

            if (_pinModes.ContainsKey(pinNumber))
            {
                _pinModes[pinNumber].CurrentPinMode = mode;
            }
            else
            {
                _pinModes.Add(pinNumber, new PinState(mode));
            }
        }

        /// <inheritdoc/>
        protected override bool IsPinModeSupported(int pinNumber, PinMode mode)
        {
            return mode switch
            {
                PinMode.Input or PinMode.Output or PinMode.InputPullUp or PinMode.InputPullDown => true,
                _ => false,
            };
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (_grfPointer != IntPtr.Zero)
            {
                Interop.munmap(_grfPointer, 0);
                _grfPointer = IntPtr.Zero;
            }

            base.Dispose(disposing);
        }

        private void InitializeGeneralRegisterFiles()
        {
            if (_grfPointer != IntPtr.Zero)
            {
                return;
            }

            lock (s_initializationLock)
            {
                if (_grfPointer != IntPtr.Zero)
                {
                    return;
                }

                int fileDescriptor = Interop.open(GpioMemoryFilePath, FileOpenFlags.O_RDWR | FileOpenFlags.O_SYNC);
                if (fileDescriptor == -1)
                {
                    throw new IOException($"Error {Marshal.GetLastWin32Error()} initializing the Gpio driver.");
                }

                // register size is 64kb
                IntPtr grfMap = Interop.mmap(IntPtr.Zero, Environment.SystemPageSize * 16, MemoryMappedProtections.PROT_READ | MemoryMappedProtections.PROT_WRITE, MemoryMappedFlags.MAP_SHARED, fileDescriptor, (int)(GeneralRegisterFiles & ~_mapMask));

                if (grfMap.ToInt64() < 0)
                {
                    Interop.munmap(grfMap, 0);
                    throw new IOException($"Error {Marshal.GetLastWin32Error()} initializing the Gpio driver (GRF initialize error).");
                }

                _grfPointer = grfMap;

                Interop.close(fileDescriptor);
            }
        }
    }
}
