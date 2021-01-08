using System;
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
        
        private UIntPtr _grfPointer = UIntPtr.Zero;
        private UIntPtr _pmuGrfPointer = UIntPtr.Zero;
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

            int modeAddress;
            uint* modePointer;
            uint modeValue;

            if (unmapped.GpioNumber <= 1)
            {
                modeAddress = (int)((PmuGeneralRegisterFiles + _grfOffsets[unmapped.GpioNumber * 4 + unmapped.Port]) & _mapMask);
                modePointer = (uint*)(_pmuGrfPointer + modeAddress);
                modeValue = *modePointer;
                // software write enable
                modeValue |= 0xFFFF_0000;
                // set pull-up/pull-down: pull-up is 0b11; pull-down is 0b01; default is 0b00/0b10
                modeValue &= (uint)~(0b11 << (unmapped.PortNumber * 2));

                switch (mode)
                {
                    case PinMode.InputPullDown:
                        modeValue |= (uint)(0b01 << (unmapped.PortNumber * 2));
                        break;
                    case PinMode.InputPullUp:
                        modeValue |= (uint)(0b11 << (unmapped.PortNumber * 2));
                        break;
                    default:
                        break;
                }
            }
            else
            {
                modeAddress = (int)((GeneralRegisterFiles + _grfOffsets[unmapped.GpioNumber * 4 + unmapped.Port]) & _mapMask);
                modePointer = (uint*)(_grfPointer + modeAddress);
                modeValue = *modePointer;
                Console.WriteLine(Convert.ToString(modeValue, 16));
                // software write enable
                modeValue |= 0xFFFF_0000;
                // set pull-up/pull-down: pull-up is 0b01; pull-down is 0b10; default is 0b00/0b11
                modeValue &= (uint)~(0b11 << (unmapped.PortNumber * 2));

                switch (mode)
                {
                    case PinMode.InputPullDown:
                        modeValue |= (uint)(0b10 << (unmapped.PortNumber * 2));
                        break;
                    case PinMode.InputPullUp:
                        modeValue |= (uint)(0b01 << (unmapped.PortNumber * 2));
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
            if (_grfPointer != UIntPtr.Zero)
            {
                Interop.munmap(_grfPointer, 0);
                _grfPointer = UIntPtr.Zero;
            }

            if (_pmuGrfPointer != UIntPtr.Zero)
            {
                Interop.munmap(_pmuGrfPointer, 0);
                _pmuGrfPointer = UIntPtr.Zero;
            }

            base.Dispose(disposing);
        }

        private void InitializeGeneralRegisterFiles()
        {
            if (_grfPointer != UIntPtr.Zero)
            {
                return;
            }

            lock (s_initializationLock)
            {
                if (_grfPointer != UIntPtr.Zero)
                {
                    return;
                }

                int fileDescriptor = Interop.open(GpioMemoryFilePath, FileOpenFlags.O_RDWR | FileOpenFlags.O_SYNC);
                if (fileDescriptor == -1)
                {
                    throw new IOException($"Error {Marshal.GetLastWin32Error()} initializing the Gpio driver.");
                }

                UIntPtr pmuGrfMap = Interop.mmap(IntPtr.Zero, Environment.SystemPageSize, MemoryMappedProtections.PROT_READ | MemoryMappedProtections.PROT_WRITE, MemoryMappedFlags.MAP_SHARED, fileDescriptor, PmuGeneralRegisterFiles & ~_mapMask);
                UIntPtr grfMap = Interop.mmap(IntPtr.Zero, Environment.SystemPageSize, MemoryMappedProtections.PROT_READ | MemoryMappedProtections.PROT_WRITE, MemoryMappedFlags.MAP_SHARED, fileDescriptor, GeneralRegisterFiles & ~_mapMask);

                Console.WriteLine(GeneralRegisterFiles & ~_mapMask);

                if (pmuGrfMap.ToUInt64() == 0)
                {
                    Interop.munmap(pmuGrfMap, 0);
                    throw new IOException($"Error {Marshal.GetLastWin32Error()} initializing the Gpio driver (PMU GRF initialize error).");
                }

                if (grfMap.ToUInt64() == 0)
                {
                    Interop.munmap(grfMap, 0);
                    throw new IOException($"Error {Marshal.GetLastWin32Error()} initializing the Gpio driver (GRF initialize error).");
                }

                _pmuGrfPointer = pmuGrfMap;
                _grfPointer = grfMap;

                Interop.close(fileDescriptor);
            }
        }
    }
}
