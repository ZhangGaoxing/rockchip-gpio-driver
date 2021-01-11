// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Iot.Device.Gpio.Drivers.Rockchip.Rk3399Driver;
using System;
using System.Device.Gpio;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Iot.Device.Gpio.Drivers
{
    public unsafe class Rk3399Driver : RockchipDriver
    {
        /// <inheritdoc/>
        protected override uint[] GpioRegisterAddresses =>
            new[] { 0xFF72_0000, 0xFF73_0000, 0xFF78_0000, 0xFF78_8000, 0xFF79_0000 };

        /// <summary>
        /// PMUGRF, used for always on sysyem.
        /// </summary>
        protected uint PmuGeneralRegisterFiles => 0xFF32_0000;

        /// <summary>
        /// GRF, used for general non-secure system.
        /// </summary>
        protected uint GeneralRegisterFiles => 0xFF77_0000;

        private PmuGrfRegisterView* _pmuGrfRegisterViewPointer = null;
        private GrfRegisterView* _grfRegisterViewPointer = null;
        private int[] _grfOffsets = new[]
        {
            0x00040, 0x00044, -1, -1,  // GPIO0 PU/PD control
            0x00050, 0x00054, 0x00058, 0x0005C,  // GPIO1 PU/PD control
            0x0E040, 0x0E044, 0x0E048, 0x0E04C,  // GPIO2 PU/PD control
            0x0E050, 0x0E054, 0x0E058, 0x0E05C,  // GPIO3 PU/PD control
            0x0E060, 0x0E064, 0x0E068, 0x0E06C  // GPIO4 PU/PD control
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="Rk3399Driver"/> class.
        /// </summary>
        public Rk3399Driver()
        {
            InitializeGeneralRegisterFiles();
        }

        /// <inheritdoc/>
        protected override void SetPinMode(int pinNumber, PinMode mode)
        {
            (int GpioNumber, int Port, int PortNumber) unmapped = UnmapPinNumber(pinNumber);

            // data register (GPIO_SWPORT_DDR) offset is 0x0004
            int dirAddress = (int)((GpioRegisterAddresses[unmapped.GpioNumber] + 0x0004) & _mapMask);
            uint* dirPointer = (uint*)(_gpioPointers[unmapped.GpioNumber] + dirAddress);
            uint dirValue = *dirPointer;

            uint* modePointer;
            uint modeValue;
            int bitOffset = unmapped.PortNumber * 2;

            if (unmapped.GpioNumber <= 1)
            {
                modePointer = &((uint*)_pmuGrfRegisterViewPointer)
                    [_grfOffsets[unmapped.GpioNumber * 4 + unmapped.Port] / sizeof(uint)];
                modeValue = *modePointer;
                // software write enable
                modeValue |= (uint)(0b11 << (16 + bitOffset));
                // set pull-up/pull-down: pull-up is 0b11; pull-down is 0b01; default is 0b00/0b10
                modeValue &= (uint)~(0b11 << bitOffset);

                switch (mode)
                {
                    case PinMode.InputPullDown:
                        modeValue |= (uint)(0b01 << bitOffset);
                        break;
                    case PinMode.InputPullUp:
                        modeValue |= (uint)(0b11 << bitOffset);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                modePointer = &((uint*)_grfRegisterViewPointer)
                    [_grfOffsets[unmapped.GpioNumber * 4 + unmapped.Port] / sizeof(uint)];
                modeValue = *modePointer;
                // software write enable
                modeValue |= (uint)(0b11 << (16 + bitOffset));
                // set pull-up/pull-down: pull-up is 0b01; pull-down is 0b10; default is 0b00/0b11
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
            }

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

            *modePointer = modeValue;
            Thread.SpinWait(150);
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
            if (_pmuGrfRegisterViewPointer != null)
            {
                Interop.munmap((IntPtr)_pmuGrfRegisterViewPointer, 0);
                _pmuGrfRegisterViewPointer = null;
            }

            if (_grfRegisterViewPointer != null)
            {
                Interop.munmap((IntPtr)_grfRegisterViewPointer, 0);
                _grfRegisterViewPointer = null;
            }

            base.Dispose(disposing);
        }

        private void InitializeGeneralRegisterFiles()
        {
            if (_grfRegisterViewPointer != null)
            {
                return;
            }

            lock (s_initializationLock)
            {
                if (_grfRegisterViewPointer != null)
                {
                    return;
                }

                int fileDescriptor = Interop.open(GpioMemoryFilePath, FileOpenFlags.O_RDWR | FileOpenFlags.O_SYNC);
                if (fileDescriptor == -1)
                {
                    throw new IOException($"Error {Marshal.GetLastWin32Error()} initializing the Gpio driver.");
                }

                // register size is 64kb
                IntPtr pmuGrfMap = Interop.mmap(IntPtr.Zero, Environment.SystemPageSize * 16, MemoryMappedProtections.PROT_READ | MemoryMappedProtections.PROT_WRITE, MemoryMappedFlags.MAP_SHARED, fileDescriptor, (int)(PmuGeneralRegisterFiles & ~_mapMask));
                IntPtr grfMap = Interop.mmap(IntPtr.Zero, Environment.SystemPageSize * 16, MemoryMappedProtections.PROT_READ | MemoryMappedProtections.PROT_WRITE, MemoryMappedFlags.MAP_SHARED, fileDescriptor, (int)(GeneralRegisterFiles & ~_mapMask));

                if (pmuGrfMap.ToInt64() < 0)
                {
                    Interop.munmap(pmuGrfMap, 0);
                    throw new IOException($"Error {Marshal.GetLastWin32Error()} initializing the Gpio driver (PMU GRF initialize error).");
                }

                if (grfMap.ToInt64() < 0)
                {
                    Interop.munmap(grfMap, 0);
                    throw new IOException($"Error {Marshal.GetLastWin32Error()} initializing the Gpio driver (GRF initialize error).");
                }

                _pmuGrfRegisterViewPointer = (PmuGrfRegisterView*)pmuGrfMap;
                _grfRegisterViewPointer = (GrfRegisterView*)grfMap;

                Interop.close(fileDescriptor);
            }
        }
    }
}
