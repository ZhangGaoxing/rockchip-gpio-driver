// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Iot.Device.Gpio.Drivers
{
    /// <summary>
    /// A generic GPIO driver for Rockchip SoCs.
    /// </summary>
    /// <remarks>
    /// This is a generic GPIO driver for Rockchip SoCs.
    /// It can even drive the internal pins that are not drawn out.
    /// Before you operate, you must be clear about what you are doing.
    /// </remarks>
    public unsafe class RockchipDriver : SysFsDriver
    {
        /// <summary>
        /// Gpio register addresses.
        /// </summary>
        protected virtual int[] GpioRegisterAddresses { get; }

        /// <summary>
        /// General register file (GRF) address.
        /// </summary>
        /// <remarks>
        /// GPIO PAD pulldown and pullup control.
        /// </remarks>
        protected virtual int GeneralRegisterFileAddress { get; }

        private const string GpioMemoryFilePath = "/dev/mem";
        private List<IntPtr> _gpioPointers = new List<IntPtr>();
        private IntPtr _grfPointers;
        private readonly IDictionary<int, PinState> _pinModes = new Dictionary<int, PinState>();
        private static readonly object s_initializationLock = new object();
        private static readonly object s_sysFsInitializationLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="RockchipDriver"/> class.
        /// </summary>
        protected RockchipDriver()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RockchipDriver"/>.
        /// </summary>
        /// <param name="gpioRegisterAddresses">Gpio register addresses (This can be find in the corresponding SoC datasheet).</param>
        /// <param name="generalRegisterFileAddress">General register file (GRF) address (This can be find in the corresponding SoC datasheet).</param>
        public RockchipDriver(int[] gpioRegisterAddresses, int generalRegisterFileAddress)
        {
            GpioRegisterAddresses = gpioRegisterAddresses;
            GeneralRegisterFileAddress = generalRegisterFileAddress;
        }

        /// <summary>
        /// The number of pins provided by the driver.
        /// </summary>
        protected override int PinCount => throw new PlatformNotSupportedException("This driver is generic so it can not enumerate how many pins are available.");

        /// <summary>
        /// Converts a board pin number to the driver's logical numbering scheme.
        /// </summary>
        /// <param name="pinNumber">The board pin number to convert.</param>
        /// <returns>The pin number in the driver's logical numbering scheme.</returns>
        protected override int ConvertPinNumberToLogicalNumberingScheme(int pinNumber) => throw new PlatformNotSupportedException("This driver is generic so it can not perform conversions between pin numbering schemes.");

        /// <summary>
        /// Opens a pin in order for it to be ready to use.
        /// </summary>
        /// <param name="pinNumber">The pin number in the driver's logical numbering scheme.</param>
        protected override void OpenPin(int pinNumber)
        {
            Initialize();
            SetPinMode(pinNumber, PinMode.Input);
        }

        /// <summary>
        /// Closes an open pin.
        /// </summary>
        /// <param name="pinNumber">The pin number in the driver's logical numbering scheme.</param>
        protected override void ClosePin(int pinNumber)
        {
            if (_pinModes.ContainsKey(pinNumber))
            {
                if (_pinModes[pinNumber].InUseByInterruptDriver)
                {
                    base.ClosePin(pinNumber);
                }

                if (_pinModes[pinNumber].CurrentPinMode == PinMode.Output)
                {
                    Write(pinNumber, PinValue.Low);
                    SetPinMode(pinNumber, PinMode.Input);
                }

                _pinModes.Remove(pinNumber);
            }
        }

        /// <summary>
        /// Sets the mode to a pin.
        /// </summary>
        /// <param name="pinNumber">The pin number in the driver's logical numbering scheme.</param>
        /// <param name="mode">The mode to be set.</param>
        protected override void SetPinMode(int pinNumber, PinMode mode)
        {
            // different chips have different number of GRFs and offsets
            // this method needs to be overridden in subclasses
            base.SetPinMode(pinNumber, mode);
        }

        /// <summary>
        /// Writes a value to a pin.
        /// </summary>
        /// <param name="pinNumber">The pin number in the driver's logical numbering scheme.</param>
        /// <param name="value">The value to be written to the pin.</param>
        protected override void Write(int pinNumber, PinValue value)
        {
            (int GpioNumber, int Port, int PortNumber) unmapped = UnmapPinNumber(pinNumber);

            int dataAddress;
            uint* dataPointer;

            // data register (GPIO_SWPORTA_DR) offset is 0x0000
            dataAddress = GpioRegisterAddresses[unmapped.GpioNumber];
            dataPointer = (uint*)(_gpioPointers[unmapped.GpioNumber] + dataAddress);

            uint dataValue = *dataPointer;

            if (value == PinValue.High)
            {
                dataValue |= (uint)(1 << (unmapped.Port * 8 + unmapped.PortNumber));
            }
            else
            {
                dataValue &= (uint)~(1 << (unmapped.Port * 8 + unmapped.PortNumber));
            }

            *dataPointer = dataValue;
        }

        /// <summary>
        /// Reads the current value of a pin.
        /// </summary>
        /// <param name="pinNumber">The pin number in the driver's logical numbering scheme.</param>
        /// <returns>The value of the pin.</returns>
        protected unsafe override PinValue Read(int pinNumber)
        {
            (int GpioNumber, int Port, int PortNumber) unmapped = UnmapPinNumber(pinNumber);

            int dataAddress;
            uint* dataPointer;

            // data register (GPIO_SWPORTA_DR) offset is 0x0000
            dataAddress = GpioRegisterAddresses[unmapped.GpioNumber];
            dataPointer = (uint*)(_gpioPointers[unmapped.GpioNumber] + dataAddress);
            uint dataValue = *dataPointer;

            return Convert.ToBoolean((dataValue >> (unmapped.Port * 8 + unmapped.PortNumber)) & 1) ? PinValue.High : PinValue.Low;
        }

        /// <summary>
        /// Adds a handler for a pin value changed event.
        /// </summary>
        /// <param name="pinNumber">The pin number in the driver's logical numbering scheme.</param>
        /// <param name="eventTypes">The event types to wait for.</param>
        /// <param name="callback">Delegate that defines the structure for callbacks when a pin value changed event occurs.</param>
        protected override void AddCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback)
        {
            _pinModes[pinNumber].InUseByInterruptDriver = true;

            base.OpenPin(pinNumber);
            base.AddCallbackForPinValueChangedEvent(pinNumber, eventTypes, callback);
        }

        /// <summary>
        /// Removes a handler for a pin value changed event.
        /// </summary>
        /// <param name="pinNumber">The pin number in the driver's logical numbering scheme.</param>
        /// <param name="callback">Delegate that defines the structure for callbacks when a pin value changed event occurs.</param>
        protected override void RemoveCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback)
        {
            _pinModes[pinNumber].InUseByInterruptDriver = true;

            base.OpenPin(pinNumber);
            base.RemoveCallbackForPinValueChangedEvent(pinNumber, callback);
        }

        /// <summary>
        /// Blocks execution until an event of type eventType is received or a cancellation is requested.
        /// </summary>
        /// <param name="pinNumber">The pin number in the driver's logical numbering scheme.</param>
        /// <param name="eventTypes">The event types to wait for.</param>
        /// <param name="cancellationToken">The cancellation token of when the operation should stop waiting for an event.</param>
        /// <returns>A structure that contains the result of the waiting operation.</returns>
        protected override WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, CancellationToken cancellationToken)
        {
            _pinModes[pinNumber].InUseByInterruptDriver = true;

            base.OpenPin(pinNumber);
            return base.WaitForEvent(pinNumber, eventTypes, cancellationToken);
        }

        /// <summary>
        /// Async call until an event of type eventType is received or a cancellation is requested.
        /// </summary>
        /// <param name="pinNumber">The pin number in the driver's logical numbering scheme.</param>
        /// <param name="eventTypes">The event types to wait for.</param>
        /// <param name="cancellationToken">The cancellation token of when the operation should stop waiting for an event.</param>
        /// <returns>A task representing the operation of getting the structure that contains the result of the waiting operation</returns>
        protected override ValueTask<WaitForEventResult> WaitForEventAsync(int pinNumber, PinEventTypes eventTypes, CancellationToken cancellationToken)
        {
            _pinModes[pinNumber].InUseByInterruptDriver = true;

            base.OpenPin(pinNumber);
            return base.WaitForEventAsync(pinNumber, eventTypes, cancellationToken);
        }

        /// <summary>
        /// Checks if a pin supports a specific mode.
        /// </summary>
        /// <param name="pinNumber">The pin number in the driver's logical numbering scheme.</param>
        /// <param name="mode">The mode to check.</param>
        /// <returns>The status if the pin supports the mode.</returns>
        protected override bool IsPinModeSupported(int pinNumber, PinMode mode)
        {
            switch (mode)
            {
                case PinMode.Input:
                case PinMode.InputPullDown:
                case PinMode.InputPullUp:
                case PinMode.Output:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets the mode of a pin.
        /// </summary>
        /// <param name="pinNumber">The pin number in the driver's logical numbering scheme.</param>
        /// <returns>The mode of the pin.</returns>
        protected override PinMode GetPinMode(int pinNumber)
        {
            if (!_pinModes.ContainsKey(pinNumber))
            {
                throw new InvalidOperationException("Can not get a pin mode of a pin that is not open.");
            }

            return _pinModes[pinNumber].CurrentPinMode;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            foreach (IntPtr pointer in _gpioPointers)
            {
                Interop.munmap(pointer, 0);
            }
            _gpioPointers.Clear();

            Dispose();
        }

        private void Initialize()
        {
            if (_gpioPointers.Count != 0)
            {
                return;
            }

            lock (s_initializationLock)
            {
                if (_gpioPointers.Count != 0)
                {
                    return;
                }

                int fileDescriptor = Interop.open(GpioMemoryFilePath, FileOpenFlags.O_RDWR | FileOpenFlags.O_SYNC);
                if (fileDescriptor == -1)
                {
                    throw new IOException($"Error {Marshal.GetLastWin32Error()} initializing the Gpio driver.");
                }

                foreach (int address in GpioRegisterAddresses)
                {
                    IntPtr map = Interop.mmap(IntPtr.Zero, Environment.SystemPageSize - 1, (MemoryMappedProtections.PROT_READ | MemoryMappedProtections.PROT_WRITE), MemoryMappedFlags.MAP_SHARED, fileDescriptor, address);
                    if (map.ToInt64() == -1)
                    {
                        throw new IOException($"Error {Marshal.GetLastWin32Error()} initializing the Gpio driver.");
                    }

                    _gpioPointers.Add(map);
                }

                IntPtr grfMap = Interop.mmap(IntPtr.Zero, Environment.SystemPageSize - 1, (MemoryMappedProtections.PROT_READ | MemoryMappedProtections.PROT_WRITE), MemoryMappedFlags.MAP_SHARED, fileDescriptor, GeneralRegisterFileAddress);
                if (fileDescriptor == -1)
                {
                    throw new IOException($"Error {Marshal.GetLastWin32Error()} initializing the Gpio driver.");
                }
                _grfPointers = grfMap;

                Interop.close(fileDescriptor);
            }
        }

        /// <summary>
        /// Map pin number with port name to pin number in the driver's logical numbering scheme.
        /// </summary>
        /// <param name="gpioNumber">Number of GPIOs.</param>
        /// <param name="port">Port name, from 'A' to 'D'.</param>
        /// <param name="portNumber">Number of pins.</param>
        /// <returns>Pin number in the driver's logical numbering scheme.</returns>
        public static int MapPinNumber(int gpioNumber, char port, int portNumber)
        {
            // For example, GPIO4_D5 = {4}*32 + {3}*8 + {5} = 157
            // https://wiki.radxa.com/Rockpi4/hardware/gpio
            return 32 * gpioNumber +
                8 * ((port >= 'A' && port <= 'D') ? port - 'A' : throw new Exception()) +
                portNumber;
        }

        private (int GpioNumber, int Port, int PortNumber) UnmapPinNumber(int pinNumber)
        {
            int portNumber = pinNumber % 8;
            int port = (pinNumber - portNumber) % 32 / 8;
            int gpioNumber = (pinNumber - portNumber) / 32;

            return (gpioNumber, port, portNumber);
        }

        private class PinState
        {
            public PinState(PinMode currentMode)
            {
                CurrentPinMode = currentMode;
                InUseByInterruptDriver = false;
            }

            public PinMode CurrentPinMode { get; set; }

            public bool InUseByInterruptDriver { get; set; }
        }
    }
}
