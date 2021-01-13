using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Iot.Device.Gpio.Drivers
{
    /// <summary>
    /// A GPIO driver for the NanoPi R2S
    /// </summary>
    /// <remarks>
    /// SoC: Rockchip RK3328
    /// </remarks>
    public class NanoPiR2S : Rk3328Driver
    {
        public static readonly int[] _pinNumberConverter = new int[]
        {
            -1, -1, -1,  MapPinNumber(2, 'D', 1), -1, MapPinNumber(2, 'D', 0), -1,
            MapPinNumber(2, 'A', 2), MapPinNumber(3, 'A', 4), -1, MapPinNumber(3, 'A', 6)
        };

        /// <inheritdoc/>
        protected override int PinCount => _pinNumberConverter.Count(n => n != -1);

        /// <inheritdoc/>
        protected override int ConvertPinNumberToLogicalNumberingScheme(int pinNumber)
        {
            int num = _pinNumberConverter[pinNumber];

            return num != -1 ? num : throw new ArgumentException($"Board (header) pin {pinNumber} is not a GPIO pin on the {GetType().Name} device.", nameof(pinNumber));
        }
    }
}
