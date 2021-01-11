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
        protected virtual uint[] GpioRegisterAddresses { get; } = Array.Empty<uint>();

        protected const string GpioMemoryFilePath = "/dev/mem";
        protected IntPtr[] _gpioPointers = Array.Empty<IntPtr>();
        protected IDictionary<int, PinState> _pinModes = new Dictionary<int, PinState>();
        protected readonly int _mapMask = Environment.SystemPageSize - 1;
        protected static readonly object s_initializationLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="RockchipDriver"/> class.
        /// </summary>
        protected RockchipDriver()
        {
            Initialize();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RockchipDriver"/>.
        /// </summary>
        /// <param name="gpioRegisterAddresses">Gpio register addresses (This can be found in the corresponding SoC datasheet).</param>
        public RockchipDriver(uint[] gpioRegisterAddresses)
        {
            GpioRegisterAddresses = gpioRegisterAddresses;
            Initialize();
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

                switch (_pinModes[pinNumber].CurrentPinMode)
                {
                    case PinMode.InputPullDown:
                    case PinMode.InputPullUp:
                        SetPinMode(pinNumber, PinMode.Input);
                        break;
                    case PinMode.Output:
                        Write(pinNumber, PinValue.Low);
                        SetPinMode(pinNumber, PinMode.Input);
                        break;
                    default:
                        break;
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
            base.OpenPin(pinNumber);
            base.SetPinMode(pinNumber, mode);

            if (_pinModes.ContainsKey(pinNumber))
            {
                _pinModes[pinNumber].CurrentPinMode = mode;
            }
            else
            {
                _pinModes.Add(pinNumber, new PinState(mode));
            }
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

            // data register (GPIO_SWPORT_DR) offset is 0x0000
            dataAddress = (int)(GpioRegisterAddresses[unmapped.GpioNumber] & _mapMask);
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

            // data register (GPIO_EXT_PORTA) offset is 0x0050
            dataAddress = (int)((GpioRegisterAddresses[unmapped.GpioNumber] + 0x0050) & _mapMask);
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
            if (!_pinModes.ContainsKey(pinNumber))
            {
                throw new InvalidOperationException("Can not add a handler to a pin that is not open.");
            }
            else
            {
                if (_pinModes[pinNumber].CurrentPinMode == PinMode.Output)
                {
                    throw new InvalidOperationException("Can not add a handler to a pin that is output mode.");
                }
            }

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
            if (!_pinModes.ContainsKey(pinNumber))
            {
                throw new InvalidOperationException("Can not add a handler to a pin that is not open.");
            }

            _pinModes[pinNumber].InUseByInterruptDriver = false;

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
            if (!_pinModes.ContainsKey(pinNumber))
            {
                throw new InvalidOperationException("Can not add a block execution to a pin that is not open.");
            }

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
            if (!_pinModes.ContainsKey(pinNumber))
            {
                throw new InvalidOperationException("Can not async call to a pin that is not open.");
            }

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
            return mode switch
            {
                PinMode.Input or PinMode.Output => true,
                _ => false,
            };
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
            Array.Clear(_gpioPointers, 0, _gpioPointers.Length);
        }

        private void Initialize()
        {
            if (_gpioPointers.Length != 0)
            {
                return;
            }

            lock (s_initializationLock)
            {
                if (_gpioPointers.Length != 0)
                {
                    return;
                }

                int fileDescriptor = Interop.open(GpioMemoryFilePath, FileOpenFlags.O_RDWR | FileOpenFlags.O_SYNC);
                if (fileDescriptor == -1)
                {
                    throw new IOException($"Error {Marshal.GetLastWin32Error()} initializing the Gpio driver.");
                }

                _gpioPointers = new IntPtr[GpioRegisterAddresses.Length];

                for (int i = 0; i < GpioRegisterAddresses.Length; i++)
                {
                    IntPtr map = Interop.mmap(IntPtr.Zero, Environment.SystemPageSize * 8, MemoryMappedProtections.PROT_READ | MemoryMappedProtections.PROT_WRITE, MemoryMappedFlags.MAP_SHARED, fileDescriptor, (int)(GpioRegisterAddresses[i] & ~_mapMask));

                    if (map.ToInt64() < 0)
                    {
                        Interop.munmap(map, 0);
                        throw new IOException($"Error {Marshal.GetLastWin32Error()} initializing the Gpio driver (GPIO{i} initialize error).");
                    }

                    _gpioPointers[i] = map;
                }

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

        protected (int GpioNumber, int Port, int PortNumber) UnmapPinNumber(int pinNumber)
        {
            int portNumber = pinNumber % 8;
            int port = (pinNumber - portNumber) % 32 / 8;
            int gpioNumber = (pinNumber - portNumber) / 32;

            return (gpioNumber, port, portNumber);
        }
    }

    public class PinState
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
